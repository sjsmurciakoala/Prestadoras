-- =============================================================================
-- Unificación de cobranza — F2: modelo adm_pago + aplicación + folio de recibo
-- Fecha: 2026-07-26
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md (§3.4, §3.5, §3.8, F2)
--
-- Contenido:
--   1) adm_documento_secuencia + fn_adm_siguiente_correlativo_documento:
--      folio administrable por empresa (patrón fn_adm_siguiente_codigo_cliente:
--      UPDATE…RETURNING atómico, auto-correctivo). El recibo de cobro NO lleva
--      CAI (decisión §3.8 — el CAI queda intacto para FAC/NC/ND).
--   2) adm_pago: cabecera del cobro (el "recibo" como entidad de primera clase),
--      con folio único por empresa e idempotencia por referencia_externa.
--   3) adm_pago_aplicacion: puente pago ↔ documento (lo que nunca existió) —
--      cada lempira cobrado queda amarrado a un documento con FK reales.
--
-- En F2 el motor único (CobroService) escribe este modelo con DUAL-WRITE hacia
-- transaccion_abonado (la fila legacy espejo mantiene cuadrando contabilidad,
-- reportes y arqueo hasta F7).
--
-- Idempotente. Aplicar en local (siad_v3_copia09) y test (siad_v3_test).
-- NO aplicar en 0.9 hasta la ventana de deploy del plan.
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) Folio de documentos por empresa
--    canal_id: 0 = serie general de la empresa; >0 reservado para series por
--    canal/caja si algún día se quieren (1 caja, 2 banco, 3 app).
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_documento_secuencia (
    secuencia_id     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id       bigint NOT NULL REFERENCES public.cfg_company (company_id),
    tipo_documento   varchar(30) NOT NULL,
    canal_id         smallint NOT NULL DEFAULT 0,
    prefijo          varchar(10) NOT NULL DEFAULT '',
    longitud_padding smallint NOT NULL DEFAULT 8
        CHECK (longitud_padding BETWEEN 4 AND 12),
    valor_actual     bigint NOT NULL DEFAULT 0 CHECK (valor_actual >= 0),
    activo           boolean NOT NULL DEFAULT true,
    updated_by       varchar(100),
    updated_at       timestamptz,
    CONSTRAINT uq_adm_documento_secuencia UNIQUE (company_id, tipo_documento, canal_id)
);

COMMENT ON TABLE public.adm_documento_secuencia IS
'Series de folios administrables por empresa (tipo_documento = RECIBO_PAGO, ...).
Consumo atómico vía fn_adm_siguiente_correlativo_documento (UPDATE…RETURNING).
El folio admite huecos ante rollback (mismo criterio que el CAI offline).
Unificación cobranza F2 (2026-07-26).';

-- Seed: serie RECIBO_PAGO por empresa; continúa desde el numrecibo legacy
-- (identity global de factura) para que los folios nuevos no colisionen con
-- los números de recibo que los clientes ya conocen.
INSERT INTO public.adm_documento_secuencia
    (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
SELECT c.company_id, 'RECIBO_PAGO', 0, 'REC-', 8,
       COALESCE((SELECT max(f.numrecibo) FROM public.factura f
                 WHERE f.company_id = c.company_id), 0),
       'seed-uc-f2'
FROM public.cfg_company c
ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING;

CREATE OR REPLACE FUNCTION public.fn_adm_siguiente_correlativo_documento(
    p_company_id     bigint,
    p_tipo_documento varchar,
    p_canal_id       smallint DEFAULT 0
)
RETURNS text
LANGUAGE plpgsql
AS $function$
DECLARE
    v record;
    v_codigo text;
    v_intentos int := 0;
BEGIN
    LOOP
        -- El UPDATE bloquea la fila de la serie: dos cobros concurrentes se
        -- serializan aquí y cada uno recibe un folio distinto.
        UPDATE public.adm_documento_secuencia
        SET valor_actual = valor_actual + 1,
            updated_at   = now()
        WHERE company_id = p_company_id
          AND tipo_documento = p_tipo_documento
          AND canal_id = p_canal_id
          AND activo
        RETURNING prefijo, longitud_padding, valor_actual INTO v;

        IF NOT FOUND THEN
            -- Sin serie propia del canal: caer a la serie general (canal 0).
            IF p_canal_id <> 0 THEN
                RETURN public.fn_adm_siguiente_correlativo_documento(p_company_id, p_tipo_documento, 0::smallint);
            END IF;
            RETURN NULL;  -- sin serie activa: el llamador decide (error de config)
        END IF;

        v_codigo := v.prefijo || lpad(v.valor_actual::text, v.longitud_padding, '0');

        -- Auto-corrección ante colisiones (folios cargados por fuera de la serie).
        EXIT WHEN p_tipo_documento <> 'RECIBO_PAGO' OR NOT EXISTS (
            SELECT 1 FROM public.adm_pago p
            WHERE p.company_id = p_company_id AND p.numero_recibo = v_codigo
        );

        v_intentos := v_intentos + 1;
        IF v_intentos > 100 THEN
            RAISE EXCEPTION 'No se pudo generar un folio libre % para company_id=% tras % intentos.',
                p_tipo_documento, p_company_id, v_intentos;
        END IF;
    END LOOP;

    RETURN v_codigo;
END;
$function$;

COMMENT ON FUNCTION public.fn_adm_siguiente_correlativo_documento(bigint, varchar, smallint) IS
'Consume y devuelve el siguiente folio de la serie (prefijo || lpad(valor, padding)).
Atómico (UPDATE…RETURNING serializa concurrentes); auto-correctivo ante colisiones
con folios existentes en adm_pago. NULL si no hay serie activa. Unificación F2.';

-- ============================================================================
-- 2) adm_pago — cabecera del cobro
--    canal_id: 1 = caja (ventanilla), 2 = banco (WS), 3 = app.
--    estado_id → adm_estado_pago (1 APLICADO, 2 PENDIENTE, 3 ANULADO, 4 REVERSADO).
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_pago (
    pago_id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id              bigint NOT NULL REFERENCES public.cfg_company (company_id),
    numero_recibo           varchar(30) NOT NULL,
    cliente_clave           varchar(30) NOT NULL,
    fecha                   date NOT NULL DEFAULT current_date,
    canal_id                smallint NOT NULL CHECK (canal_id IN (1, 2, 3)),
    tipo_transaccion_id     smallint NOT NULL
        REFERENCES public.adm_tipo_transaccion (tipo_transaccion_id),
    estado_id               smallint NOT NULL DEFAULT 1
        REFERENCES public.adm_estado_pago (estado_pago_id),
    monto_total             numeric(18,2) NOT NULL CHECK (monto_total > 0),
    forma_pago              varchar(20) NOT NULL DEFAULT 'EFECTIVO'
        CHECK (forma_pago IN ('EFECTIVO', 'BANCO')),
    banco_cuenta_id         integer,
    ban_kardex_id           bigint,
    sesion_caja_id          integer REFERENCES public.sesion_caja (id),
    poliza_id               bigint,           -- comprobante contable (referencia laxa, como el resto del repo)
    referencia_externa      varchar(100),     -- idempotencia: referencia bancaria / uuid app
    transaccion_abonado_ide integer,          -- fila legacy espejo del dual-write (F2–F7)
    motivo_reverso          varchar(300),
    usuario                 varchar(100) NOT NULL,
    creado_en               timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz,
    CONSTRAINT uq_adm_pago_numero_recibo UNIQUE (company_id, numero_recibo),
    -- Regla única del motor (plan §4): canal caja exige sesión abierta.
    CONSTRAINT ck_adm_pago_caja_sesion CHECK (canal_id <> 1 OR sesion_caja_id IS NOT NULL)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_pago_referencia_externa
    ON public.adm_pago (company_id, referencia_externa)
    WHERE referencia_externa IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_adm_pago_cliente
    ON public.adm_pago (company_id, cliente_clave, fecha);
CREATE INDEX IF NOT EXISTS ix_adm_pago_sesion
    ON public.adm_pago (company_id, sesion_caja_id)
    WHERE sesion_caja_id IS NOT NULL;

COMMENT ON TABLE public.adm_pago IS
'Cabecera del cobro (el recibo como entidad): folio único por empresa
(numero_recibo, SIN CAI — §3.8 del plan), canal, forma de pago, sesión de caja
obligatoria en ventanilla, idempotencia por referencia_externa y enlace al
espejo legacy transaccion_abonado durante el dual-write. Estados en
adm_estado_pago; el reverso NUNCA borra (estado → ANULADO/REVERSADO).
Unificación cobranza F2 (2026-07-26).';

-- ============================================================================
-- 3) adm_pago_aplicacion — puente pago ↔ documento
--    documento_tipo: 1 = factura (línea a línea), 2 = cuota de plan (F6),
--                    3 = nota de débito (futuro).
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_pago_aplicacion (
    aplicacion_id      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint NOT NULL REFERENCES public.cfg_company (company_id),
    pago_id            bigint NOT NULL REFERENCES public.adm_pago (pago_id),
    documento_tipo     smallint NOT NULL CHECK (documento_tipo IN (1, 2, 3)),
    factura_id         integer REFERENCES public.factura (id),
    factura_detalle_id integer REFERENCES public.factura_detalle (id),
    plan_cuota_id      integer REFERENCES public.cln_plan_pago_dtl (id),
    nota_debito_id     bigint REFERENCES public.adm_nota_debito (nota_debito_id),
    monto_aplicado     numeric(18,2) NOT NULL CHECK (monto_aplicado > 0),
    CONSTRAINT ck_adm_pago_aplicacion_ref CHECK (
        (documento_tipo = 1 AND factura_id IS NOT NULL
             AND plan_cuota_id IS NULL AND nota_debito_id IS NULL)
     OR (documento_tipo = 2 AND plan_cuota_id IS NOT NULL
             AND factura_id IS NULL AND factura_detalle_id IS NULL AND nota_debito_id IS NULL)
     OR (documento_tipo = 3 AND nota_debito_id IS NOT NULL
             AND factura_id IS NULL AND factura_detalle_id IS NULL AND plan_cuota_id IS NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_adm_pago_aplicacion_pago
    ON public.adm_pago_aplicacion (company_id, pago_id);
CREATE INDEX IF NOT EXISTS ix_adm_pago_aplicacion_factura
    ON public.adm_pago_aplicacion (company_id, factura_id)
    WHERE factura_id IS NOT NULL;

COMMENT ON TABLE public.adm_pago_aplicacion IS
'Aplicación del pago por documento (y por línea de factura cuando aplica).
Invariante del motor: SUM(monto_aplicado) por pago = adm_pago.monto_total.
Convierte "cobrar" en un hecho auditable por documento — el saldo del cliente
pasa a ser verificable contra documentos (F4). Unificación cobranza F2.';

COMMIT;

-- =============================================================================
-- SMOKE TESTS (auto-contenidos; revierten sus propios datos)
-- =============================================================================

-- A) Folio: consumo atómico, formato y unicidad
DO $$
DECLARE
    v_f1 text;
    v_f2 text;
    v_base bigint;
BEGIN
    SELECT valor_actual INTO v_base
    FROM public.adm_documento_secuencia
    WHERE company_id = 2 AND tipo_documento = 'RECIBO_PAGO' AND canal_id = 0;

    v_f1 := public.fn_adm_siguiente_correlativo_documento(2, 'RECIBO_PAGO');
    v_f2 := public.fn_adm_siguiente_correlativo_documento(2, 'RECIBO_PAGO');

    IF v_f1 IS NULL OR v_f2 IS NULL OR v_f1 = v_f2 THEN
        RAISE EXCEPTION 'Folios inválidos o repetidos: % / %', v_f1, v_f2;
    END IF;
    IF v_f1 !~ '^REC-[0-9]{8,}$' THEN
        RAISE EXCEPTION 'Formato de folio inesperado: %', v_f1;
    END IF;

    -- Revertir el consumo del smoke (solo para no gastar folios en la prueba).
    UPDATE public.adm_documento_secuencia
    SET valor_actual = v_base
    WHERE company_id = 2 AND tipo_documento = 'RECIBO_PAGO' AND canal_id = 0;

    RAISE NOTICE 'Smoke A OK: folios % y % (serie continúa desde numrecibo=%)', v_f1, v_f2, v_base;
END $$;

-- B) adm_pago: canal caja exige sesión; folio único por empresa
DO $$
DECLARE
    v_pago_id bigint;
    v_fallo boolean := false;
BEGIN
    -- Canal caja sin sesión debe rechazarse (CHECK)
    BEGIN
        INSERT INTO public.adm_pago
            (company_id, numero_recibo, cliente_clave, canal_id, tipo_transaccion_id,
             monto_total, usuario)
        VALUES (2, 'SMOKE-F2-1', 'SMOKE', 1, 2, 100, 'smoke');
    EXCEPTION WHEN check_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_pago aceptó canal caja sin sesión (CHECK no disparó)';
    END IF;

    -- Canal banco sin sesión es válido
    INSERT INTO public.adm_pago
        (company_id, numero_recibo, cliente_clave, canal_id, tipo_transaccion_id,
         monto_total, usuario, referencia_externa)
    VALUES (2, 'SMOKE-F2-2', 'SMOKE', 2, 3, 100, 'smoke', 'SMOKE-REF-1')
    RETURNING pago_id INTO v_pago_id;

    -- Folio duplicado debe rechazarse
    v_fallo := false;
    BEGIN
        INSERT INTO public.adm_pago
            (company_id, numero_recibo, cliente_clave, canal_id, tipo_transaccion_id,
             monto_total, usuario)
        VALUES (2, 'SMOKE-F2-2', 'SMOKE', 2, 3, 100, 'smoke');
    EXCEPTION WHEN unique_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_pago aceptó numero_recibo duplicado';
    END IF;

    -- Referencia externa duplicada debe rechazarse (idempotencia)
    v_fallo := false;
    BEGIN
        INSERT INTO public.adm_pago
            (company_id, numero_recibo, cliente_clave, canal_id, tipo_transaccion_id,
             monto_total, usuario, referencia_externa)
        VALUES (2, 'SMOKE-F2-3', 'SMOKE', 2, 3, 100, 'smoke', 'SMOKE-REF-1');
    EXCEPTION WHEN unique_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_pago aceptó referencia_externa duplicada';
    END IF;

    DELETE FROM public.adm_pago WHERE pago_id = v_pago_id;
    RAISE NOTICE 'Smoke B OK: CHECK de sesión, folio único y referencia única';
END $$;

-- C) adm_pago_aplicacion: CHECK de referencia por tipo de documento
DO $$
DECLARE
    v_pago_id bigint;
    v_factura_id integer;
    v_fallo boolean := false;
BEGIN
    INSERT INTO public.adm_pago
        (company_id, numero_recibo, cliente_clave, canal_id, tipo_transaccion_id,
         monto_total, usuario)
    VALUES (2, 'SMOKE-F2-APL', 'SMOKE', 2, 3, 100, 'smoke')
    RETURNING pago_id INTO v_pago_id;

    INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
        ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
    VALUES (2, 'SMOKE-F2-FAC', 'SMOKE', 'F', '2026', '7', current_date, 'A', 'S', 1)
    RETURNING id INTO v_factura_id;

    -- Aplicación válida a factura
    INSERT INTO public.adm_pago_aplicacion
        (company_id, pago_id, documento_tipo, factura_id, monto_aplicado)
    VALUES (2, v_pago_id, 1, v_factura_id, 100);

    -- Aplicación tipo factura SIN factura_id debe rechazarse
    BEGIN
        INSERT INTO public.adm_pago_aplicacion
            (company_id, pago_id, documento_tipo, monto_aplicado)
        VALUES (2, v_pago_id, 1, 50);
    EXCEPTION WHEN check_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_pago_aplicacion aceptó tipo factura sin factura_id';
    END IF;

    DELETE FROM public.adm_pago_aplicacion WHERE pago_id = v_pago_id;
    DELETE FROM public.factura WHERE id = v_factura_id;
    DELETE FROM public.adm_pago WHERE pago_id = v_pago_id;
    RAISE NOTICE 'Smoke C OK: aplicación amarrada al documento por CHECK';
END $$;
