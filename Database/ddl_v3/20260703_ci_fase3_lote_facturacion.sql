-- =============================================================================
-- Integración Contable ↔ Comercial — Fase 3: lote manual de partidas de facturación
-- Fecha: 2026-07-03 (v2: correcciones del code review del 2026-07-03)
-- Plan: docs/plans/2026-07-02-plan-integracion-contable-comercial.md §5 Fase 3
--       (D5: la sincronización de lecturas NUNCA postea; el contador genera el
--        lote después, desde la pantalla. D1: solo el motor único postea).
--
-- Contenido:
--   1. Snapshot dimensional en factura (categoria_servicio_id + con_medicion)
--      + backfill + trigger BEFORE INSERT (desvío documentado: mismo efecto
--      que tocar sp_lectura_v3 pero sin redefinir el SP corazón; el trigger
--      NO pisa valores explícitos y solo completa lo que venga NULL).
--   2. AKs tenant-safe: factura (company_id, id) y con_partida_hdr
--      (company_id, poliza_id) para FKs compuestas (regla del repo).
--   3. con_lote_facturacion — historial de lotes.
--   4. con_partida_factura — puente factura ↔ partida (poliza_id NULL =
--      factura procesada sin efecto contable, p.ej. detalle en cero).
--   5. fn_con_periodo_abierto — período OPEN por fecha (semántica del motor).
--   6. fn_con_siguiente_poliza — numeración del motor: correlativo MENSUAL
--      bajo pg_advisory_xact_lock (misma convención que sp_con_generar_comprobante).
--   7. fn_con_candidatas_lote_facturacion / fn_con_lineas_lote_facturacion —
--      selección y resolución compartidas por preview y generación (una sola
--      fuente de verdad; resolución por combo dimensional, no por línea).
--   8. fn_con_preview_partidas_facturacion — preview agregado sin escribir.
--   9. sp_con_generar_partidas_facturacion — genera y postea vía motor único;
--      idempotente (incluida la rama de encolado); pendientes se resuelven por
--      COBERTURA del rango de su payload.
--
-- Idempotente. Producción: aplicar SOLO en ventana de deploy acordada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Snapshot dimensional en factura
-- -----------------------------------------------------------------------------
ALTER TABLE public.factura
    ADD COLUMN IF NOT EXISTS categoria_servicio_id integer,
    ADD COLUMN IF NOT EXISTS con_medicion boolean;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_factura_categoria_servicio'
          AND conrelid = 'public.factura'::regclass
    ) THEN
        ALTER TABLE public.factura
            ADD CONSTRAINT fk_factura_categoria_servicio
            FOREIGN KEY (categoria_servicio_id)
            REFERENCES public.categoria_servicio (categoria_servicio_id);
    END IF;
END $$;

COMMENT ON COLUMN public.factura.categoria_servicio_id IS
'Snapshot de la categoría del cliente AL MOMENTO de facturar (plan F3; los reportes por categoría ERSAPS usan este valor, no el actual del cliente).';
COMMENT ON COLUMN public.factura.con_medicion IS
'Snapshot de si el cliente tenía medidor al facturar (dimensión de la matriz de integración contable).';

UPDATE public.factura f
SET categoria_servicio_id = cm.categoria_servicio_id,
    con_medicion = cm.maestro_cliente_tiene_medidor
FROM public.cliente_maestro cm
WHERE cm.company_id = f.company_id
  AND cm.maestro_cliente_clave = f.clientecodigo
  AND f.categoria_servicio_id IS NULL;

-- Trigger: completa SOLO los campos que vengan NULL y SOLO si el cliente existe
-- (SELECT INTO sin IF FOUND borraría valores explícitos cuando no hay fila).
CREATE OR REPLACE FUNCTION public.fn_factura_snapshot_dimensional()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    v_categoria integer;
    v_medicion boolean;
BEGIN
    IF NEW.categoria_servicio_id IS NULL OR NEW.con_medicion IS NULL THEN
        SELECT cm.categoria_servicio_id, cm.maestro_cliente_tiene_medidor
        INTO v_categoria, v_medicion
        FROM public.cliente_maestro cm
        WHERE cm.company_id = NEW.company_id
          AND cm.maestro_cliente_clave = NEW.clientecodigo
        LIMIT 1;

        IF FOUND THEN
            NEW.categoria_servicio_id := COALESCE(NEW.categoria_servicio_id, v_categoria);
            NEW.con_medicion := COALESCE(NEW.con_medicion, v_medicion);
        END IF;
    END IF;
    RETURN NEW;
END;
$function$;

DROP TRIGGER IF EXISTS trg_factura_snapshot_dimensional ON public.factura;
CREATE TRIGGER trg_factura_snapshot_dimensional
    BEFORE INSERT ON public.factura
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_factura_snapshot_dimensional();

-- -----------------------------------------------------------------------------
-- 2. AKs tenant-safe para FKs compuestas
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conname = 'uq_factura_company_id'
                     AND conrelid = 'public.factura'::regclass) THEN
        ALTER TABLE public.factura
            ADD CONSTRAINT uq_factura_company_id UNIQUE (company_id, id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conname = 'uq_con_partida_hdr_company_poliza'
                     AND conrelid = 'public.con_partida_hdr'::regclass) THEN
        ALTER TABLE public.con_partida_hdr
            ADD CONSTRAINT uq_con_partida_hdr_company_poliza UNIQUE (company_id, poliza_id);
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 3. con_lote_facturacion — historial de lotes
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.con_lote_facturacion (
    lote_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    company_id bigint NOT NULL,

    fecha_desde date NOT NULL,
    fecha_hasta date NOT NULL,
    modo_agrupacion varchar(20) NOT NULL DEFAULT 'DIA',

    facturas integer NOT NULL DEFAULT 0,
    polizas integer NOT NULL DEFAULT 0,
    encoladas integer NOT NULL DEFAULT 0,
    total numeric(18,2) NOT NULL DEFAULT 0,

    -- 1=GENERADO, 2=PARCIAL (algún grupo encolado), 3=ENCOLADO (todo sin período)
    status_id smallint NOT NULL DEFAULT 1,

    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(100) NOT NULL DEFAULT current_user,

    CONSTRAINT fk_con_lote_facturacion_company
        FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id) ON DELETE CASCADE,
    CONSTRAINT ck_con_lote_facturacion_modo
        CHECK (modo_agrupacion IN ('DIA', 'PERIODO')),
    CONSTRAINT ck_con_lote_facturacion_status
        CHECK (status_id IN (1, 2, 3)),
    CONSTRAINT ck_con_lote_facturacion_rango
        CHECK (fecha_desde <= fecha_hasta)
);

CREATE INDEX IF NOT EXISTS ix_con_lote_facturacion_company
    ON public.con_lote_facturacion (company_id, created_at DESC);

COMMENT ON TABLE public.con_lote_facturacion IS
'Historial de lotes de partidas de facturación (plan F3). Solo quedan lotes que hicieron trabajo real (postear, marcar o encolar nuevo). Estados: 1=GENERADO, 2=PARCIAL, 3=ENCOLADO.';

-- -----------------------------------------------------------------------------
-- 4. con_partida_factura — puente factura ↔ partida
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.con_partida_factura (
    partida_factura_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    company_id bigint NOT NULL,
    factura_id integer NOT NULL,
    lote_id bigint NOT NULL,
    -- NULL = factura procesada sin efecto contable (detalle neto en cero)
    poliza_id bigint,

    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(100) NOT NULL DEFAULT current_user,

    CONSTRAINT uq_con_partida_factura_factura UNIQUE (company_id, factura_id),
    CONSTRAINT fk_con_partida_factura_company
        FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id) ON DELETE CASCADE,
    -- FKs compuestas tenant-safe (AKs de la sección 2)
    CONSTRAINT fk_con_partida_factura_factura
        FOREIGN KEY (company_id, factura_id) REFERENCES public.factura (company_id, id),
    CONSTRAINT fk_con_partida_factura_lote
        FOREIGN KEY (lote_id) REFERENCES public.con_lote_facturacion (lote_id) ON DELETE CASCADE,
    CONSTRAINT fk_con_partida_factura_poliza
        FOREIGN KEY (company_id, poliza_id) REFERENCES public.con_partida_hdr (company_id, poliza_id)
);

CREATE INDEX IF NOT EXISTS ix_con_partida_factura_poliza
    ON public.con_partida_factura (company_id, poliza_id);

COMMENT ON TABLE public.con_partida_factura IS
'Puente factura ↔ partida del lote de facturación (plan F3). Una factura solo puede pertenecer a un lote (uq company+factura): correr el lote dos veces no duplica. poliza_id NULL = procesada sin efecto contable (total 0).';

-- Migración de instalaciones que aplicaron la v1 de este script
DO $$
BEGIN
    -- poliza_id pasa a ser NULLABLE
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'con_partida_factura'
                 AND column_name = 'poliza_id' AND is_nullable = 'NO') THEN
        ALTER TABLE public.con_partida_factura ALTER COLUMN poliza_id DROP NOT NULL;
    END IF;

    -- FK de factura: de simple a compuesta tenant-safe
    IF EXISTS (SELECT 1 FROM pg_constraint c
               WHERE c.conname = 'fk_con_partida_factura_factura'
                 AND c.conrelid = 'public.con_partida_factura'::regclass
                 AND array_length(c.conkey, 1) = 1) THEN
        ALTER TABLE public.con_partida_factura DROP CONSTRAINT fk_con_partida_factura_factura;
        ALTER TABLE public.con_partida_factura
            ADD CONSTRAINT fk_con_partida_factura_factura
            FOREIGN KEY (company_id, factura_id) REFERENCES public.factura (company_id, id);
    END IF;

    -- FK de póliza: de simple a compuesta tenant-safe
    IF EXISTS (SELECT 1 FROM pg_constraint c
               WHERE c.conname = 'fk_con_partida_factura_poliza'
                 AND c.conrelid = 'public.con_partida_factura'::regclass
                 AND array_length(c.conkey, 1) = 1) THEN
        ALTER TABLE public.con_partida_factura DROP CONSTRAINT fk_con_partida_factura_poliza;
        ALTER TABLE public.con_partida_factura
            ADD CONSTRAINT fk_con_partida_factura_poliza
            FOREIGN KEY (company_id, poliza_id) REFERENCES public.con_partida_hdr (company_id, poliza_id);
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 5. fn_con_periodo_abierto — período OPEN por fecha (semántica del motor:
--    status_id NULL se considera NO abierto, igual que sp_con_generar_comprobante)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_con_periodo_abierto(
    p_company_id bigint,
    p_fecha date
) RETURNS bigint
LANGUAGE sql
STABLE
AS $function$
    SELECT p.period_id
    FROM public.con_periodo_contable p
    WHERE p.company_id = p_company_id
      AND COALESCE(p.status_id, 2) = 0
      AND p_fecha BETWEEN p.start_date::date AND p.end_date::date
    ORDER BY p.start_date DESC
    LIMIT 1;
$function$;

-- -----------------------------------------------------------------------------
-- 6. fn_con_siguiente_poliza — numeración del motor (correlativo mensual con lock)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_con_siguiente_poliza(
    p_company_id bigint,
    p_fecha date,
    OUT poliza_number text,
    OUT seq bigint
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_prefix text;
BEGIN
    -- Misma convención y lock que sp_con_generar_comprobante:
    -- empresa-YYYY-MM-correlativo, serializado por (empresa, año-mes).
    v_prefix := p_company_id::text || '-' || to_char(p_fecha, 'YYYY-MM') || '-';

    PERFORM pg_advisory_xact_lock(
        ((p_company_id::bigint << 32) #
         ((EXTRACT(YEAR FROM p_fecha)::int * 100 + EXTRACT(MONTH FROM p_fecha)::int)::bigint))
    );

    SELECT COALESCE(MAX(substring(h.poliza_number from length(v_prefix) + 1)::bigint), 0) + 1
    INTO seq
    FROM public.con_partida_hdr h
    WHERE h.company_id = p_company_id
      AND h.poliza_number LIKE v_prefix || '%'
      AND substring(h.poliza_number from length(v_prefix) + 1) ~ '^[0-9]+$';

    poliza_number := v_prefix || lpad(seq::text, 6, '0');
END;
$function$;

COMMENT ON FUNCTION public.fn_con_siguiente_poliza(bigint, date) IS
'Siguiente número de partida con la convención del motor (empresa-YYYY-MM-nnnnnn), serializado con pg_advisory_xact_lock por empresa × mes (mismo lock que sp_con_generar_comprobante).';

-- -----------------------------------------------------------------------------
-- 7. Selección y resolución compartidas (una sola fuente para preview y lote)
-- -----------------------------------------------------------------------------
-- Facturas candidatas del rango: lecturas, no anuladas, sin partida.
-- En modo PERIODO la fecha del grupo es la ÚLTIMA emisión real del rango
-- (no la fecha "hasta" elegida por el usuario, que podría caer en un período
-- distinto al de las facturas).
CREATE OR REPLACE FUNCTION public.fn_con_candidatas_lote_facturacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_modo_agrupacion varchar DEFAULT 'DIA'
) RETURNS TABLE (
    factura_id integer,
    fecha_grupo date,
    categoria_servicio_id integer,
    con_medicion boolean
)
LANGUAGE sql
STABLE
AS $function$
    WITH base AS (
        SELECT f.id, f.fechaemision, f.categoria_servicio_id, f.con_medicion
        FROM public.factura f
        WHERE f.company_id = p_company_id
          AND f.tipofacturacion = 'S'
          AND f.tipofactura = 'F'
          AND COALESCE(f.estado_id, 1) <> 3
          AND COALESCE(f.estado, 'A') <> 'N'
          AND f.fechaemision BETWEEN p_fecha_desde AND p_fecha_hasta
          AND NOT EXISTS (
              SELECT 1 FROM public.con_partida_factura pf
              WHERE pf.company_id = p_company_id AND pf.factura_id = f.id)
    )
    SELECT b.id,
           CASE WHEN upper(p_modo_agrupacion) = 'PERIODO'
                THEN (SELECT MAX(b2.fechaemision) FROM base b2)
                ELSE b.fechaemision END,
           b.categoria_servicio_id,
           b.con_medicion
    FROM base b;
$function$;

-- Líneas resueltas (una por línea de detalle con monto <> 0): la resolución de
-- cuentas se hace UNA VEZ por combinación dimensional distinta (no por línea).
-- Montos negativos van al lado contrario (débito negativo = crédito).
CREATE OR REPLACE FUNCTION public.fn_con_lineas_lote_facturacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_modo_agrupacion varchar DEFAULT 'DIA'
) RETURNS TABLE (
    fecha_grupo date,
    factura_id integer,
    cuenta bigint,
    debe numeric,
    haber numeric
)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_modo_ventas varchar;
    v_modo_cxc varchar;
BEGIN
    SELECT c.modo_ventas, c.modo_cxc
    INTO v_modo_ventas, v_modo_cxc
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La empresa % no tiene configuración de integración contable (pantalla Integración Contable / perfil ERSAPS).', p_company_id;
    END IF;

    RETURN QUERY
    WITH lineas AS (
        SELECT c.fecha_grupo AS fg, c.factura_id AS fid, fd.montovalor,
               s.servicio_id, c.categoria_servicio_id AS cat, c.con_medicion AS med
        FROM public.fn_con_candidatas_lote_facturacion(p_company_id, p_fecha_desde, p_fecha_hasta, p_modo_agrupacion) c
        JOIN public.factura_detalle fd ON fd.factura_id = c.factura_id
        LEFT JOIN public.adm_servicio s
               ON s.company_id = p_company_id
              AND upper(btrim(s.codigo)) = upper(btrim(fd.tiposervicio))
        WHERE COALESCE(fd.montovalor, 0) <> 0
    ),
    combos AS (
        SELECT DISTINCT l.servicio_id, l.cat, l.med FROM lineas l
    ),
    cuentas AS (
        SELECT co.servicio_id, co.cat, co.med,
               public.fn_con_resolver_cuenta_modo(p_company_id, 'INGRESO', v_modo_ventas,
                   co.servicio_id, co.cat, co.med) AS ingreso_account,
               public.fn_con_resolver_cuenta_modo(p_company_id, 'CXC', v_modo_cxc,
                   co.servicio_id, co.cat, co.med) AS cxc_account
        FROM combos co
    ),
    resueltas AS (
        SELECT l.fg, l.fid, l.montovalor, cu.ingreso_account, cu.cxc_account
        FROM lineas l
        JOIN cuentas cu
          ON cu.servicio_id IS NOT DISTINCT FROM l.servicio_id
         AND cu.cat IS NOT DISTINCT FROM l.cat
         AND cu.med IS NOT DISTINCT FROM l.med
    )
    -- Lado CxC: monto positivo al Debe, negativo al Haber; Ingresos al revés.
    SELECT r.fg, r.fid, r.cxc_account,
           CASE WHEN r.montovalor > 0 THEN r.montovalor ELSE 0 END,
           CASE WHEN r.montovalor < 0 THEN -r.montovalor ELSE 0 END
    FROM resueltas r
    UNION ALL
    SELECT r.fg, r.fid, r.ingreso_account,
           CASE WHEN r.montovalor < 0 THEN -r.montovalor ELSE 0 END,
           CASE WHEN r.montovalor > 0 THEN r.montovalor ELSE 0 END
    FROM resueltas r;
END;
$function$;

COMMENT ON FUNCTION public.fn_con_lineas_lote_facturacion(bigint, date, date, varchar) IS
'Líneas contables resueltas del lote de facturación (plan F3): fuente ÚNICA consumida por el preview y por sp_con_generar_partidas_facturacion — el preview muestra por construcción lo que el lote postea.';

-- -----------------------------------------------------------------------------
-- 8. Preview del lote (no escribe nada)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_con_preview_partidas_facturacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_modo_agrupacion varchar DEFAULT 'DIA'
) RETURNS TABLE (
    fecha_partida date,
    uso varchar,
    account_id bigint,
    account_code varchar,
    account_name varchar,
    debe numeric,
    haber numeric,
    facturas bigint
)
LANGUAGE sql
STABLE
AS $function$
    SELECT l.fecha_grupo,
           CASE WHEN SUM(l.debe) >= SUM(l.haber) THEN 'CXC' ELSE 'INGRESO' END::varchar,
           l.cuenta,
           pc.code,
           pc.name,
           round(SUM(l.debe), 2),
           round(SUM(l.haber), 2),
           COUNT(DISTINCT l.factura_id)
    FROM public.fn_con_lineas_lote_facturacion(p_company_id, p_fecha_desde, p_fecha_hasta, p_modo_agrupacion) l
    JOIN public.con_plan_cuentas pc ON pc.account_id = l.cuenta
    GROUP BY l.fecha_grupo, l.cuenta, pc.code, pc.name
    ORDER BY l.fecha_grupo, 2 DESC, pc.code;
$function$;

COMMENT ON FUNCTION public.fn_con_preview_partidas_facturacion(bigint, date, date, varchar) IS
'Preview del lote de partidas de facturación (plan F3): agrega fn_con_lineas_lote_facturacion por fecha × cuenta, sin escribir nada. Lanza la misma excepción que el lote real si alguna combinación no resuelve cuenta.';

-- -----------------------------------------------------------------------------
-- 9. Generación del lote (postea vía motor único)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_con_generar_partidas_facturacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_modo_agrupacion varchar DEFAULT 'DIA',
    p_usuario text DEFAULT current_user
) RETURNS TABLE (
    lote_id bigint,
    polizas integer,
    facturas integer,
    encoladas integer,
    total numeric
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_config record;
    v_asiento record;
    v_lote_id bigint;
    v_grupo record;
    v_period_id bigint;
    v_poliza_id bigint;
    v_numero record;
    v_line smallint;
    v_total_grupo numeric;
    v_polizas integer := 0;
    v_facturas integer := 0;
    v_encoladas integer := 0;
    v_total numeric := 0;
    v_marcadas_cero integer := 0;
    v_linea record;
    v_pend_actualizadas integer;
BEGIN
    IF upper(COALESCE(p_modo_agrupacion, '')) NOT IN ('DIA', 'PERIODO') THEN
        RAISE EXCEPTION 'Modo de agrupación no soportado: %. Use DIA o PERIODO.', p_modo_agrupacion;
    END IF;

    SELECT c.modo_ventas, c.modo_cxc, c.encolar_sin_periodo
    INTO v_config
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La empresa % no tiene configuración de integración contable.', p_company_id;
    END IF;

    SELECT a.journal_id, a.type_id
    INTO v_asiento
    FROM public.con_integracion_asiento a
    WHERE a.company_id = p_company_id AND a.module = 'VENTAS';
    IF NOT FOUND OR v_asiento.journal_id IS NULL OR v_asiento.type_id IS NULL THEN
        RAISE EXCEPTION 'El módulo VENTAS no tiene diario y tipo de partida configurados (pestaña Asientos de Integración Contable).';
    END IF;

    -- Candidatas y líneas resueltas (misma fuente que el preview)
    DROP TABLE IF EXISTS pg_temp.tmp_lote_candidatas;
    CREATE TEMP TABLE tmp_lote_candidatas ON COMMIT DROP AS
    SELECT * FROM public.fn_con_candidatas_lote_facturacion(
        p_company_id, p_fecha_desde, p_fecha_hasta, p_modo_agrupacion);

    IF NOT EXISTS (SELECT 1 FROM tmp_lote_candidatas) THEN
        RETURN QUERY SELECT NULL::bigint, 0, 0, 0, 0::numeric;
        RETURN;
    END IF;

    DROP TABLE IF EXISTS pg_temp.tmp_lote_lineas;
    CREATE TEMP TABLE tmp_lote_lineas ON COMMIT DROP AS
    SELECT * FROM public.fn_con_lineas_lote_facturacion(
        p_company_id, p_fecha_desde, p_fecha_hasta, p_modo_agrupacion);

    INSERT INTO public.con_lote_facturacion
        (company_id, fecha_desde, fecha_hasta, modo_agrupacion, created_by)
    VALUES (p_company_id, p_fecha_desde, p_fecha_hasta, upper(p_modo_agrupacion), p_usuario)
    RETURNING con_lote_facturacion.lote_id INTO v_lote_id;

    FOR v_grupo IN
        SELECT c.fecha_grupo, COUNT(*) AS n_facturas
        FROM tmp_lote_candidatas c
        GROUP BY c.fecha_grupo
        ORDER BY c.fecha_grupo
    LOOP
        v_period_id := public.fn_con_periodo_abierto(p_company_id, v_grupo.fecha_grupo);

        IF v_period_id IS NULL THEN
            IF NOT v_config.encolar_sin_periodo THEN
                RAISE EXCEPTION 'No hay período contable abierto para la fecha % y la configuración no permite encolar.', v_grupo.fecha_grupo;
            END IF;

            -- Dedup del encolado: si ya hay una pendiente viva para esta fecha,
            -- solo incrementa intentos (no duplica cola ni infla contadores).
            UPDATE public.con_partida_pendiente pp
            SET intentos = pp.intentos + 1, updated_at = now(), updated_by = p_usuario
            WHERE pp.company_id = p_company_id
              AND pp.module = 'VENTAS'
              AND pp.origen_tipo = 'LOTE_FACTURACION'
              AND pp.status_id = 1
              AND pp.fecha_documento = v_grupo.fecha_grupo;
            GET DIAGNOSTICS v_pend_actualizadas = ROW_COUNT;

            IF v_pend_actualizadas = 0 THEN
                INSERT INTO public.con_partida_pendiente
                    (company_id, module, origen_tipo, origen_id, fecha_documento,
                     descripcion, payload, motivo, intentos, created_by)
                VALUES
                    (p_company_id, 'VENTAS', 'LOTE_FACTURACION', v_lote_id, v_grupo.fecha_grupo,
                     format('Lote facturación %s..%s: %s factura(s) del %s sin período contable abierto',
                            p_fecha_desde, p_fecha_hasta, v_grupo.n_facturas, v_grupo.fecha_grupo),
                     jsonb_build_object(
                         'fecha_desde', p_fecha_desde,
                         'fecha_hasta', p_fecha_hasta,
                         'fecha_partida', v_grupo.fecha_grupo,
                         'modo_agrupacion', upper(p_modo_agrupacion),
                         'facturas', v_grupo.n_facturas),
                     'SIN_PERIODO_ABIERTO', 1, p_usuario);
                v_encoladas := v_encoladas + v_grupo.n_facturas;
            END IF;
            CONTINUE;
        END IF;

        SELECT round(COALESCE(SUM(l.debe), 0), 2) INTO v_total_grupo
        FROM tmp_lote_lineas l
        WHERE l.fecha_grupo = v_grupo.fecha_grupo;

        IF v_total_grupo = 0 THEN
            -- Sin efecto contable (detalle vacío o neto cero): marcar las
            -- facturas como procesadas SIN póliza para que no queden como
            -- candidatas eternas ni generen lotes vacíos en cada corrida.
            INSERT INTO public.con_partida_factura (company_id, factura_id, lote_id, poliza_id, created_by)
            SELECT p_company_id, c.factura_id, v_lote_id, NULL, p_usuario
            FROM tmp_lote_candidatas c
            WHERE c.fecha_grupo = v_grupo.fecha_grupo;
            v_marcadas_cero := v_marcadas_cero + v_grupo.n_facturas;
            v_facturas := v_facturas + v_grupo.n_facturas;
            CONTINUE;
        END IF;

        -- Encabezado con la numeración del motor (lock + correlativo mensual)
        SELECT * INTO v_numero FROM public.fn_con_siguiente_poliza(p_company_id, v_grupo.fecha_grupo);

        INSERT INTO public.con_partida_hdr (
            company_id, journal_id, period_id, module, document_type,
            document_id, document_number, poliza_number, sequence_number,
            poliza_date, description, status, source_reference,
            created_at, created_by, type_id, total_debit, total_credit
        ) VALUES (
            p_company_id, v_asiento.journal_id, v_period_id, 'VENTAS', 'LOTE_FAC',
            v_lote_id, format('LOTE-%s-%s', v_lote_id, to_char(v_grupo.fecha_grupo, 'YYYYMMDD')),
            v_numero.poliza_number, v_numero.seq, v_grupo.fecha_grupo,
            format('Lote de facturación %s (%s factura(s))', to_char(v_grupo.fecha_grupo, 'DD/MM/YYYY'), v_grupo.n_facturas),
            0, format('LOTE-%s', v_lote_id), now(), p_usuario, v_asiento.type_id, 0, 0
        )
        RETURNING poliza_id INTO v_poliza_id;

        v_line := 0;
        FOR v_linea IN
            SELECT l.cuenta, round(SUM(l.debe), 2) AS debe, round(SUM(l.haber), 2) AS haber
            FROM tmp_lote_lineas l
            WHERE l.fecha_grupo = v_grupo.fecha_grupo
            GROUP BY l.cuenta
            HAVING round(SUM(l.debe), 2) <> 0 OR round(SUM(l.haber), 2) <> 0
            ORDER BY SUM(l.debe) < SUM(l.haber), l.cuenta
        LOOP
            v_line := v_line + 1;
            INSERT INTO public.con_partida_dtl (
                company_id, poliza_id, line_number, account_id,
                debit_amount, credit_amount, description, source_document
            ) VALUES (
                p_company_id, v_poliza_id, v_line, v_linea.cuenta,
                v_linea.debe, v_linea.haber,
                CASE WHEN v_linea.debe >= v_linea.haber THEN 'CxC abonados — lote de facturación'
                     ELSE 'Ingresos por servicios — lote de facturación' END,
                format('LOTE-%s', v_lote_id)
            );
        END LOOP;

        UPDATE public.con_partida_hdr h
        SET total_debit = v_total_grupo, total_credit = v_total_grupo
        WHERE h.poliza_id = v_poliza_id;

        -- Motor único (D1)
        PERFORM public.sp_con_postear_poliza(p_company_id, v_poliza_id, p_usuario);

        INSERT INTO public.con_partida_factura (company_id, factura_id, lote_id, poliza_id, created_by)
        SELECT p_company_id, c.factura_id, v_lote_id, v_poliza_id, p_usuario
        FROM tmp_lote_candidatas c
        WHERE c.fecha_grupo = v_grupo.fecha_grupo;

        v_polizas := v_polizas + 1;
        v_facturas := v_facturas + v_grupo.n_facturas;
        v_total := v_total + v_total_grupo;
    END LOOP;

    -- Pendientes: se resuelven por COBERTURA — una pendiente queda PROCESADA
    -- solo cuando el rango de su payload ya no tiene ninguna factura candidata
    -- (funciona entre modos DIA/PERIODO y para corridas parciales).
    IF v_polizas > 0 OR v_facturas > 0 THEN
        UPDATE public.con_partida_pendiente pp
        SET status_id = 2,
            poliza_id = CASE WHEN v_polizas > 0 THEN v_poliza_id ELSE NULL END,
            procesada_at = now(), procesada_by = p_usuario,
            updated_at = now(), updated_by = p_usuario
        WHERE pp.company_id = p_company_id
          AND pp.module = 'VENTAS'
          AND pp.origen_tipo = 'LOTE_FACTURACION'
          AND pp.status_id = 1
          AND NOT EXISTS (
              SELECT 1 FROM public.fn_con_candidatas_lote_facturacion(
                  p_company_id,
                  COALESCE((pp.payload->>'fecha_desde')::date, pp.fecha_documento),
                  COALESCE((pp.payload->>'fecha_hasta')::date, pp.fecha_documento),
                  'DIA'));
    END IF;

    -- Si la corrida no hizo NADA nuevo (todo dedup de encolado), no dejar
    -- rastro en el historial.
    IF v_polizas = 0 AND v_facturas = 0 AND v_encoladas = 0 THEN
        DELETE FROM public.con_lote_facturacion l WHERE l.lote_id = v_lote_id;
        RETURN QUERY SELECT NULL::bigint, 0, 0, 0, 0::numeric;
        RETURN;
    END IF;

    UPDATE public.con_lote_facturacion l
    SET facturas = v_facturas, polizas = v_polizas, encoladas = v_encoladas, total = v_total,
        status_id = CASE
            WHEN v_polizas = 0 AND v_marcadas_cero = 0 AND v_encoladas > 0 THEN 3
            WHEN v_encoladas > 0 THEN 2
            ELSE 1
        END
    WHERE l.lote_id = v_lote_id;

    RETURN QUERY SELECT v_lote_id, v_polizas, v_facturas, v_encoladas, v_total;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_generar_partidas_facturacion(bigint, date, date, varchar, text) IS
'Lote manual de partidas de facturación (plan F3, D5/D10). Consume fn_con_lineas_lote_facturacion (misma fuente que el preview), postea vía sp_con_postear_poliza (motor único D1) con numeración mensual del motor (fn_con_siguiente_poliza), y marca facturas en con_partida_factura (idempotente, incluida la rama de encolado que deduplica pendientes por fecha e incrementa intentos). Las pendientes se resuelven por cobertura del rango de su payload. Devuelve (lote_id, polizas, facturas, encoladas, total).';
