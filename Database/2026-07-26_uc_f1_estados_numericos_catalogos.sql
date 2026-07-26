-- =============================================================================
-- Unificación de cobranza — F1: catálogos y espejos numéricos completos
-- Fecha: 2026-07-26
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md (§3.1, §3.2, §3.3, F1)
--
-- Contenido:
--   1) adm_tipo_transaccion (catálogo global de tipos de movimiento) + seed
--      + mapa de códigos legacy (adm_tipo_transaccion_codigo_legacy).
--   2) adm_estado_pago (catálogo global de estados de pago, separado del de
--      documentos — resuelve la ambigüedad de la letra 'A').
--   3) cfg_estado_documento_comercial gana 4='B' (Parcialmente abonada).
--   4) transaccion_abonado: columnas tipo_transaccion_id y estado_pago_id
--      (FK, NULL) + backfill.
--   5) Triggers: factura endurece el ELSE (WARNING ante letra desconocida);
--      transaccion_abonado deriva los 3 ids (estado_id como siempre — sin
--      WARNING, las letras de pago son legítimas ahí — más tipo_transaccion_id
--      y estado_pago_id).
--   6) vw_transaccion_abonado_vigente: la parte de pagos pasa a expresarse por
--      estado_pago_id (comportamiento EXACTAMENTE idéntico; verificado por el
--      smoke test del final y por SaldoVigenciaTests ampliados).
--
-- Regla de equivalencia de la vista (crítica):
--   * La inversión "A = anulado" aplica SOLO a tipotransaccion '201'/'202'
--     (convención caja/WS). Los 'PAGO%' migrados de SIMAFI conservan la
--     semántica de cargos (A = activo) → estado_pago_id queda NULL para ellos
--     y los sigue gobernando el filtro por letra.
--
-- Idempotente. Aplicar en local (siad_v3_copia09), test (siad_v3_test) y 0.9.
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) Catálogo de tipos de movimiento (global, sin company_id — como cfg_*)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_tipo_transaccion (
    tipo_transaccion_id smallint PRIMARY KEY,
    codigo              varchar(30) NOT NULL UNIQUE,
    nombre              varchar(120) NOT NULL,
    naturaleza          char(1) NOT NULL CHECK (naturaleza IN ('D', 'C', 'X')), -- X = ambas
    es_pago             boolean NOT NULL DEFAULT false,
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

COMMENT ON TABLE public.adm_tipo_transaccion IS
'Catálogo global de tipos de movimiento comercial (reemplaza el string libre
transaccion_abonado.tipotransaccion). Plan unificación cobranza F1 (2026-07).
La tabla legacy vacía tipo_transaccion (scaffold SIMAFI) se elimina en F7.';

INSERT INTO public.adm_tipo_transaccion
    (tipo_transaccion_id, codigo, nombre, naturaleza, es_pago) VALUES
    (1,  'CARGO_SERVICIO', 'Cargo de servicio facturado',            'D', false),
    (2,  'PAGO_CAJA',      'Pago en caja (legacy 201)',              'C', true),
    (3,  'PAGO_BANCO',     'Pago canal bancario (legacy 202 WS)',    'C', true),
    (4,  'ABONO',          'Abono parcial (legacy 202 caja)',        'C', true),
    (5,  'NOTA_CREDITO',   'Nota de crédito (legacy 205)',           'C', false),
    (6,  'NOTA_DEBITO',    'Nota de débito (legacy 206)',            'D', false),
    (7,  'PLAN_TRASLADO',  'Traslado a plan de pago (legacy PLAN)',  'C', false),
    (8,  'PLAN_PRIMA',     'Prima de plan (legacy PLAN-PR)',         'D', false),
    (9,  'PLAN_CUOTA',     'Cuota de plan (legacy PLAN-CUOTA)',      'D', false),
    (10, 'AJUSTE',         'Ajuste manual',                          'X', false),
    (11, 'SALDO_INICIAL',  'Saldo inicial migrado',                  'D', false)
ON CONFLICT (tipo_transaccion_id) DO NOTHING;

-- Mapa de códigos legacy exactos → tipo. Lo que no esté aquí cae por regla:
--   'PAGO%' (migrados SIMAFI) → 2; '202' con marker WSBANCO → 3; resto → 1.
CREATE TABLE IF NOT EXISTS public.adm_tipo_transaccion_codigo_legacy (
    codigo_legacy       varchar(50) PRIMARY KEY,
    tipo_transaccion_id smallint NOT NULL
        REFERENCES public.adm_tipo_transaccion (tipo_transaccion_id)
);

INSERT INTO public.adm_tipo_transaccion_codigo_legacy VALUES
    ('201', 2), ('202', 4), ('205', 5), ('206', 6),
    ('PLAN', 7), ('PLAN-PR', 8), ('PLAN-CUOTA', 9), ('SALDO_ANTERIOR', 11)
ON CONFLICT (codigo_legacy) DO NOTHING;

-- ============================================================================
-- 2) Catálogo de estados de pago (global)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_estado_pago (
    estado_pago_id smallint PRIMARY KEY,
    codigo         varchar(20) NOT NULL UNIQUE,
    nombre         varchar(80) NOT NULL,
    activo         boolean NOT NULL DEFAULT true
);

COMMENT ON TABLE public.adm_estado_pago IS
'Estados de los movimientos de PAGO (201/202 hoy; adm_pago en F2). Separado de
cfg_estado_documento_comercial porque la letra A significa "activo" en cargos y
"anulado" en pagos — un solo catálogo no puede representar ambos.';

INSERT INTO public.adm_estado_pago (estado_pago_id, codigo, nombre) VALUES
    (1, 'APLICADO',  'Pago aplicado / vigente'),
    (2, 'PENDIENTE', 'Recibo generado, pendiente de pago'),
    (3, 'ANULADO',   'Anulado desde caja'),
    (4, 'REVERSADO', 'Reversado por canal externo')
ON CONFLICT (estado_pago_id) DO NOTHING;

-- ============================================================================
-- 3) cfg_estado_documento_comercial: entra B = Parcialmente abonada
-- ============================================================================
INSERT INTO public.cfg_estado_documento_comercial (estado_id, codigo, descripcion, activo)
VALUES (4, 'B', 'Parcialmente abonada', true)
ON CONFLICT (estado_id) DO NOTHING;

-- Función compartida (factura + transaccion_abonado): gana B→4. Sigue SILENTE
-- (el trigger de transaccion_abonado recibe letras de pago P/A legítimas).
CREATE OR REPLACE FUNCTION public.fn_estado_documento_comercial_id_from_codigo(p_codigo varchar)
RETURNS smallint
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE TRIM(UPPER(COALESCE(p_codigo, '')))
        WHEN 'A' THEN 1::smallint
        WHEN 'C' THEN 2::smallint
        WHEN 'N' THEN 3::smallint
        WHEN 'B' THEN 4::smallint
        ELSE 1::smallint  -- default Activa
    END;
$$;

-- ============================================================================
-- 4) transaccion_abonado: columnas espejo nuevas + backfill
-- ============================================================================
ALTER TABLE public.transaccion_abonado
    ADD COLUMN IF NOT EXISTS tipo_transaccion_id smallint
        REFERENCES public.adm_tipo_transaccion (tipo_transaccion_id),
    ADD COLUMN IF NOT EXISTS estado_pago_id smallint
        REFERENCES public.adm_estado_pago (estado_pago_id);

COMMENT ON COLUMN public.transaccion_abonado.tipo_transaccion_id IS
'Espejo numérico de tipotransaccion (adm_tipo_transaccion). Mantenido por
trigger; la string sigue siendo la fuente hasta F7.';
COMMENT ON COLUMN public.transaccion_abonado.estado_pago_id IS
'Estado del PAGO (adm_estado_pago). SOLO para tipotransaccion 201/202; NULL en
cargos, planes y PAGO% migrados (esos conservan la semántica de cargos en
estado/estado_id). Fuente correcta de vigencia de pagos; estado_id es ambiguo
para pagos (A=anulado colapsa a 1) hasta F7.';

-- Derivación del tipo numérico desde el código legacy (backfill + trigger)
CREATE OR REPLACE FUNCTION public.fn_adm_tipo_transaccion_id_legacy(
    p_tipotransaccion varchar,
    p_trans_aplicar   varchar
)
RETURNS smallint
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_id smallint;
BEGIN
    IF TRIM(COALESCE(p_tipotransaccion, '')) = '202'
       AND COALESCE(p_trans_aplicar, '') LIKE 'WSBANCO:%' THEN
        RETURN 3;  -- PAGO_BANCO
    END IF;

    SELECT m.tipo_transaccion_id INTO v_id
    FROM public.adm_tipo_transaccion_codigo_legacy m
    WHERE m.codigo_legacy = TRIM(COALESCE(p_tipotransaccion, ''));
    IF v_id IS NOT NULL THEN
        RETURN v_id;
    END IF;

    IF TRIM(UPPER(COALESCE(p_tipotransaccion, ''))) LIKE 'PAGO%' THEN
        RETURN 2;  -- PAGO_CAJA (pagos migrados SIMAFI)
    END IF;

    RETURN 1;      -- CARGO_SERVICIO (códigos de servicio, 0998, etc.)
END;
$$;

-- Derivación del estado de pago desde la letra (SOLO llamar para 201/202)
CREATE OR REPLACE FUNCTION public.fn_adm_estado_pago_id_legacy(
    p_estado        varchar,
    p_trans_aplicar varchar
)
RETURNS smallint
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE TRIM(UPPER(COALESCE(p_estado, '')))
        WHEN 'C' THEN 1::smallint  -- aplicado/vigente
        WHEN 'P' THEN 2::smallint  -- recibo pendiente
        WHEN 'A' THEN CASE WHEN COALESCE(p_trans_aplicar, '') LIKE 'WSBANCO:%'
                           THEN 4::smallint   -- reversado por el WS
                           ELSE 3::smallint   -- anulado desde caja
                      END
        WHEN 'R' THEN 4::smallint  -- reversado legacy (letra hoy no escrita)
        ELSE NULL::smallint
    END;
$$;

-- Backfill (idempotente: solo filas sin espejo)
UPDATE public.transaccion_abonado ta
SET tipo_transaccion_id = public.fn_adm_tipo_transaccion_id_legacy(ta.tipotransaccion, ta.trans_aplicar)
WHERE ta.tipo_transaccion_id IS NULL;

UPDATE public.transaccion_abonado ta
SET estado_pago_id = public.fn_adm_estado_pago_id_legacy(ta.estado, ta.trans_aplicar)
WHERE TRIM(COALESCE(ta.tipotransaccion, '')) IN ('201', '202')
  AND ta.estado_pago_id IS NULL;

-- ============================================================================
-- 5) Triggers
-- ============================================================================

-- 5a) factura: B→4 y WARNING ante letra fuera de catálogo (antes colapsaba a 1
--     en silencio). Sigue devolviendo 1 como fallback para no romper inserts.
CREATE OR REPLACE FUNCTION public.fn_factura_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_codigo varchar := TRIM(UPPER(COALESCE(NEW.estado, '')));
BEGIN
    IF TG_OP = 'INSERT'
       OR (TG_OP = 'UPDATE' AND NEW.estado IS DISTINCT FROM OLD.estado) THEN
        NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        IF v_codigo NOT IN ('A', 'B', 'C', 'N') THEN
            RAISE WARNING 'factura.estado "%" fuera de catálogo (A/B/C/N); estado_id colapsado a 1',
                NEW.estado;
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

-- 5b) transaccion_abonado: deriva los 3 espejos. Sin WARNING (las letras de
--     pago P/C/A son legítimas aquí); estado_id conserva el mapeo histórico
--     (ambiguo para pagos — el espejo correcto de pagos es estado_pago_id).
CREATE OR REPLACE FUNCTION public.fn_transaccion_abonado_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        IF NEW.tipo_transaccion_id IS NULL THEN
            NEW.tipo_transaccion_id :=
                public.fn_adm_tipo_transaccion_id_legacy(NEW.tipotransaccion, NEW.trans_aplicar);
        END IF;
        IF TRIM(COALESCE(NEW.tipotransaccion, '')) IN ('201', '202') THEN
            NEW.estado_pago_id :=
                public.fn_adm_estado_pago_id_legacy(NEW.estado, NEW.trans_aplicar);
        END IF;
    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.estado IS DISTINCT FROM OLD.estado THEN
            NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        END IF;
        IF NEW.tipotransaccion IS DISTINCT FROM OLD.tipotransaccion
           OR NEW.tipo_transaccion_id IS NULL THEN
            NEW.tipo_transaccion_id :=
                public.fn_adm_tipo_transaccion_id_legacy(NEW.tipotransaccion, NEW.trans_aplicar);
        END IF;
        IF TRIM(COALESCE(NEW.tipotransaccion, '')) IN ('201', '202')
           AND (NEW.estado IS DISTINCT FROM OLD.estado
                OR NEW.trans_aplicar IS DISTINCT FROM OLD.trans_aplicar
                OR NEW.tipotransaccion IS DISTINCT FROM OLD.tipotransaccion
                OR NEW.estado_pago_id IS NULL) THEN
            NEW.estado_pago_id :=
                public.fn_adm_estado_pago_id_legacy(NEW.estado, NEW.trans_aplicar);
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

-- (Los triggers trg_factura_sync_estado_id y trg_transaccion_abonado_sync_estado_id
--  ya existen y apuntan a estas funciones; CREATE OR REPLACE basta.)

-- ============================================================================
-- 6) Vista de vigencia: pagos por estado_pago_id (equivalencia exacta)
--    Nota: CREATE OR REPLACE agrega las 2 columnas nuevas al final del ta.*.
-- ============================================================================
CREATE OR REPLACE VIEW public.vw_transaccion_abonado_vigente AS
SELECT ta.*
FROM public.transaccion_abonado ta
WHERE COALESCE(ta.estado, '') NOT IN ('N', 'R', 'P')
  AND (ta.estado_pago_id IS NULL OR ta.estado_pago_id = 1);

COMMENT ON VIEW public.vw_transaccion_abonado_vigente IS
'Movimientos vigentes de transaccion_abonado. Cargos/NC/ND/planes/PAGO% migrados
se gobiernan por letra (fuera N anulada, R reversado legacy, P pendiente); los
pagos 201/202 se gobiernan por estado_pago_id (solo 1=APLICADO cuenta) —
equivalente exacto de la regla por letras del fix 2026-07-16, ahora expresada
sobre el catálogo adm_estado_pago. Unificación cobranza F1 (2026-07-26).';

COMMIT;

-- =============================================================================
-- SMOKE TESTS (fuera de la transacción principal; revierten sus propios datos)
-- =============================================================================

-- A) Equivalencia EXACTA vista nueva vs regla por letras del 2026-07-16
DO $$
DECLARE
    v_diff bigint;
BEGIN
    SELECT count(*) INTO v_diff
    FROM (
        SELECT ide FROM public.vw_transaccion_abonado_vigente
        EXCEPT
        SELECT ide FROM public.transaccion_abonado ta
        WHERE COALESCE(ta.estado, '') NOT IN ('N', 'R', 'P')
          AND NOT (ta.estado = 'A' AND COALESCE(ta.tipotransaccion, '') IN ('201', '202'))
    ) sobra;
    IF v_diff > 0 THEN
        RAISE EXCEPTION 'Vista nueva INCLUYE % filas que la regla por letras excluía', v_diff;
    END IF;

    SELECT count(*) INTO v_diff
    FROM (
        SELECT ide FROM public.transaccion_abonado ta
        WHERE COALESCE(ta.estado, '') NOT IN ('N', 'R', 'P')
          AND NOT (ta.estado = 'A' AND COALESCE(ta.tipotransaccion, '') IN ('201', '202'))
        EXCEPT
        SELECT ide FROM public.vw_transaccion_abonado_vigente
    ) falta;
    IF v_diff > 0 THEN
        RAISE EXCEPTION 'Vista nueva EXCLUYE % filas que la regla por letras incluía', v_diff;
    END IF;

    RAISE NOTICE 'Smoke A OK: vista nueva ≡ regla por letras (diff = 0)';
END $$;

-- B) Trigger deriva los 3 ids en un abono 202 y en su anulación/reverso
DO $$
DECLARE
    v_ide integer;
    r     record;
BEGIN
    -- Abono de caja vigente
    INSERT INTO public.transaccion_abonado
        (company_id, cliente_clave, tipotransaccion, estado, creditos, debitos, trans_aplicar)
    VALUES (2, 'SMOKE-F1', '202', 'C', 100, 0, '')
    RETURNING ide INTO v_ide;
    SELECT estado_id, tipo_transaccion_id, estado_pago_id INTO r
    FROM public.transaccion_abonado WHERE ide = v_ide;
    IF r.tipo_transaccion_id <> 4 OR r.estado_pago_id <> 1 THEN
        RAISE EXCEPTION 'Abono 202/C: esperaba tipo=4, estado_pago=1; obtuve tipo=%, estado_pago=%',
            r.tipo_transaccion_id, r.estado_pago_id;
    END IF;

    -- Anulación de caja: estado A → ANULADO(3)
    UPDATE public.transaccion_abonado SET estado = 'A' WHERE ide = v_ide;
    SELECT estado_pago_id INTO r FROM public.transaccion_abonado WHERE ide = v_ide;
    IF r.estado_pago_id <> 3 THEN
        RAISE EXCEPTION 'Anulación caja: esperaba estado_pago=3, obtuve %', r.estado_pago_id;
    END IF;
    DELETE FROM public.transaccion_abonado WHERE ide = v_ide;

    -- Pago WS y su reverso: estado A + marker → REVERSADO(4)
    INSERT INTO public.transaccion_abonado
        (company_id, cliente_clave, tipotransaccion, estado, creditos, debitos, trans_aplicar)
    VALUES (2, 'SMOKE-F1', '202', 'C', 100, 0, 'WSBANCO:999999')
    RETURNING ide INTO v_ide;
    SELECT tipo_transaccion_id INTO r FROM public.transaccion_abonado WHERE ide = v_ide;
    IF r.tipo_transaccion_id <> 3 THEN
        RAISE EXCEPTION 'Pago WS: esperaba tipo=3 (PAGO_BANCO), obtuve %', r.tipo_transaccion_id;
    END IF;
    UPDATE public.transaccion_abonado SET estado = 'A' WHERE ide = v_ide;
    SELECT estado_pago_id INTO r FROM public.transaccion_abonado WHERE ide = v_ide;
    IF r.estado_pago_id <> 4 THEN
        RAISE EXCEPTION 'Reverso WS: esperaba estado_pago=4, obtuve %', r.estado_pago_id;
    END IF;
    DELETE FROM public.transaccion_abonado WHERE ide = v_ide;

    -- Cargo de servicio: sin estado_pago, tipo=1
    INSERT INTO public.transaccion_abonado
        (company_id, cliente_clave, tipotransaccion, estado, creditos, debitos)
    VALUES (2, 'SMOKE-F1', 'AGUA_POTABLE', 'A', 0, 100)
    RETURNING ide INTO v_ide;
    SELECT estado_id, tipo_transaccion_id, estado_pago_id INTO r
    FROM public.transaccion_abonado WHERE ide = v_ide;
    IF r.tipo_transaccion_id <> 1 OR r.estado_pago_id IS NOT NULL OR r.estado_id <> 1 THEN
        RAISE EXCEPTION 'Cargo: esperaba tipo=1, estado_pago NULL, estado_id=1; obtuve %, %, %',
            r.tipo_transaccion_id, r.estado_pago_id, r.estado_id;
    END IF;
    DELETE FROM public.transaccion_abonado WHERE ide = v_ide;

    RAISE NOTICE 'Smoke B OK: trigger deriva tipo_transaccion_id y estado_pago_id';
END $$;

-- C) Factura parcialmente abonada: B → estado_id 4
DO $$
DECLARE
    v_id integer;
    v_estado_id smallint;
BEGIN
    INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
        ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
    VALUES (2, 'SMOKE-F1-B', 'SMOKE-F1', 'F', '2026', '7', current_date, 'B', 'S', 1)
    RETURNING id, estado_id INTO v_id, v_estado_id;
    IF v_estado_id <> 4 THEN
        RAISE EXCEPTION 'Factura B: esperaba estado_id=4, obtuve %', v_estado_id;
    END IF;
    DELETE FROM public.factura WHERE id = v_id;
    RAISE NOTICE 'Smoke C OK: factura B → estado_id=4';
END $$;

-- =============================================================================
-- AUDITORÍA de saldos (solo lectura; correr ANTES y DESPUÉS en cada ambiente).
-- El saldo por cliente NO debe cambiar con esta fase:
--   SELECT ta.company_id, ta.cliente_clave,
--          SUM(COALESCE(ta.debitos,0) - COALESCE(ta.creditos,0)) AS saldo
--   FROM public.vw_transaccion_abonado_vigente ta
--   GROUP BY 1, 2 ORDER BY 1, 2;
-- =============================================================================
