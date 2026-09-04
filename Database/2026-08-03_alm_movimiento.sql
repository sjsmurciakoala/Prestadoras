-- =============================================================================
-- Documento de movimiento de almacén (entradas y salidas manuales)
-- Fecha: 2026-08-03
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   Fase 2 de docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md.
--   Hoy la ÚNICA forma de sacar mercadería del almacén en el portal es un ajuste
--   de una sola línea desde la ficha del artículo. Esto crea el documento
--   multi-renglón que faltaba: cabecera + detalle + correlativo por empresa.
--
--   Equivalente funcional de dlgTransaccionesGenericasINV de Centura
--   (Casajaar_Final/NEWAPP/GA_IN.APT:14142, menú Inventario → «Transacciones
--   genéricas», Ctrl+G). Ver README_entradas_salidas_almacen.md §4.1b.
--
-- 100% ADITIVO. Tres tablas nuevas; no altera ni borra nada existente.
-- IDEMPOTENTE (CREATE TABLE IF NOT EXISTS / DO blocks guardados).
-- REVERSIBLE (bloque de rollback al final).
-- MULTIEMPRESA real: company_id en las tres, FK compuestas tenant-safe.
--
-- PRERREQUISITOS (el script los verifica y aborta con mensaje claro):
--   · alm_tipo_movimiento con su clave alterna uq_alm_tipo_movimiento_tenant
--     (paso 27 del runbook, 2026-08-01_alm_tipo_movimiento.sql)
--   · alm_bodega   con clave alterna (company_id, id)  — 2026-07-14_alm_fk_compuestas_tenant.sql
--   · alm_articulo con clave alterna (company_id, id)  — idem
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0) Guardas de prerrequisitos
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF to_regclass('public.alm_tipo_movimiento') IS NULL THEN
        RAISE EXCEPTION 'Falta alm_tipo_movimiento. Aplicar antes Database/2026-08-01_alm_tipo_movimiento.sql (paso 27).';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conname = 'uq_alm_tipo_movimiento_tenant'
                      AND conrelid = 'public.alm_tipo_movimiento'::regclass) THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_tipo_movimiento_tenant. Re-aplicar el paso 27.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE contype = 'u' AND conrelid = 'public.alm_bodega'::regclass
                      AND pg_get_constraintdef(oid) ILIKE '%(company_id, id)%') THEN
        RAISE EXCEPTION 'alm_bodega no tiene clave alterna (company_id, id). Aplicar Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE contype = 'u' AND conrelid = 'public.alm_articulo'::regclass
                      AND pg_get_constraintdef(oid) ILIKE '%(company_id, id)%') THEN
        RAISE EXCEPTION 'alm_articulo no tiene clave alterna (company_id, id). Aplicar Database/2026-07-14_alm_fk_compuestas_tenant.sql.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 1) Correlativo por empresa (mismo patrón que alm_compra_correlativo)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_movimiento_correlativo (
    company_id    BIGINT  NOT NULL PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT ck_alm_movimiento_correlativo_no_negativo CHECK (ultimo_numero >= 0)
);

COMMENT ON TABLE public.alm_movimiento_correlativo IS
 'Consecutivo del documento de movimiento de almacén, por empresa. Se incrementa con SELECT ... FOR UPDATE dentro de la transacción de posteo (mismo patrón que alm_compra_correlativo).';

-- -----------------------------------------------------------------------------
-- 2) Cabecera del documento
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_movimiento_hdr (
    id                    SERIAL        PRIMARY KEY,
    company_id            BIGINT        NOT NULL,
    numero                INTEGER       NOT NULL,
    tipo_movimiento_id    INTEGER       NOT NULL,
    bodega_id             INTEGER       NOT NULL,
    fecha                 DATE          NOT NULL,
    motivo                VARCHAR(120)  NOT NULL,
    -- Referencia1 del legacy. En área 'D' Centura lo rotula «#documento»; es la
    -- referencia externa (acta de conteo, memo, orden). Opcional a propósito:
    -- no todo ajuste tiene papel de respaldo.
    documento_referencia  VARCHAR(30)   NULL,
    observaciones         VARCHAR(1000) NULL,
    total                 NUMERIC(14,2) NOT NULL DEFAULT 0,
    estado                SMALLINT      NOT NULL DEFAULT 1,   -- 1 Registrado · 9 Anulado
    posteado              BOOLEAN       NOT NULL DEFAULT false,
    fecha_posteo          TIMESTAMP WITHOUT TIME ZONE NULL,
    anulado_por           VARCHAR(100)  NULL,
    fecha_anulacion       TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_anulacion      VARCHAR(500)  NULL,
    uuid                  UUID          NULL,
    usuariocreacion       VARCHAR(100)  NULL,
    fechacreacion         TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion   VARCHAR(100)  NULL,
    fechamodificacion     TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_movimiento_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT uq_alm_movimiento_hdr_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_movimiento_hdr_estado CHECK (estado IN (1, 9)),
    CONSTRAINT ck_alm_movimiento_hdr_numero_pos CHECK (numero > 0),
    CONSTRAINT ck_alm_movimiento_hdr_total_no_neg CHECK (total >= 0),
    CONSTRAINT ck_alm_movimiento_hdr_motivo CHECK (length(btrim(motivo)) > 0),
    -- Un documento posteado SIEMPRE tiene su huella de idempotencia y su sello de tiempo.
    CONSTRAINT ck_alm_movimiento_hdr_posteo
        CHECK (posteado = false OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL)),
    -- Anulado SIEMPRE con quién, cuándo y por qué. No se anula "en silencio".
    CONSTRAINT ck_alm_movimiento_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL
                               AND motivo_anulacion IS NOT NULL
                               AND length(btrim(motivo_anulacion)) > 0)),

    CONSTRAINT fk_alm_movimiento_hdr_tipo
        FOREIGN KEY (company_id, tipo_movimiento_id)
        REFERENCES public.alm_tipo_movimiento (company_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_alm_movimiento_hdr_bodega
        FOREIGN KEY (company_id, bodega_id)
        REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_movimiento_hdr_company_uuid
    ON public.alm_movimiento_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_alm_movimiento_hdr_company_fecha
    ON public.alm_movimiento_hdr (company_id, fecha DESC);
CREATE INDEX IF NOT EXISTS ix_alm_movimiento_hdr_tipo
    ON public.alm_movimiento_hdr (company_id, tipo_movimiento_id);
CREATE INDEX IF NOT EXISTS ix_alm_movimiento_hdr_bodega
    ON public.alm_movimiento_hdr (company_id, bodega_id);

COMMENT ON TABLE public.alm_movimiento_hdr IS
 'Cabecera del documento de movimiento de almacén (entradas y salidas manuales). Equivalente de dlgTransaccionesGenericasINV de Centura. Se crea y postea en un solo paso: no hay estado Borrador. Se corrige por anulación con reversa, NUNCA por UPDATE del kardex.';
COMMENT ON COLUMN public.alm_movimiento_hdr.documento_referencia IS
 'Referencia externa del documento de respaldo (acta de conteo, memo, orden). Es el Referencia1 del legacy, rotulado «#documento» en el área D. Opcional.';
COMMENT ON COLUMN public.alm_movimiento_hdr.estado IS
 '1 = Registrado (con su asiento en el kardex) · 9 = Anulado (con su reversa). No hay Borrador: el documento nace posteado.';
COMMENT ON COLUMN public.alm_movimiento_hdr.uuid IS
 'Idempotencia del DOCUMENTO: un reintento de la misma petición devuelve el documento ya creado en vez de duplicarlo. La idempotencia del ASIENTO va por alm_movimiento_dtl.uuid.';

-- -----------------------------------------------------------------------------
-- 3) Detalle — la unidad de posteo es el RENGLÓN, no la cabecera
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_movimiento_dtl (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        BIGINT        NOT NULL,
    movimiento_hdr_id INTEGER       NOT NULL,
    articulo_id       INTEGER       NOT NULL,
    codigo_articulo   VARCHAR(20)   NULL,       -- snapshot al capturar
    cantidad          NUMERIC(15,2) NOT NULL DEFAULT 0,
    -- Lo que teclea el usuario. Sólo se usa en clase ENTRADA: en SALIDA el motor
    -- valoriza al costo promedio vigente (es el PIDE_COSTO de INV_TRANSACC_AXL,
    -- que en el legacy vale 0 para TODA salida).
    costo_unitario    NUMERIC(12,4) NOT NULL DEFAULT 0,
    -- Lo que realmente aplicó el motor, copiado tras postear. Evita un JOIN contra
    -- alm_kardex en cada lectura del documento.
    costo_real        NUMERIC(12,4) NULL,
    total             NUMERIC(14,2) NOT NULL DEFAULT 0,
    kardex_id         INTEGER       NULL,
    uuid              UUID          NOT NULL,
    posteado          BOOLEAN       NOT NULL DEFAULT false,

    CONSTRAINT uq_alm_movimiento_dtl_uuid UNIQUE (company_id, uuid),
    CONSTRAINT uq_alm_movimiento_dtl_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_movimiento_dtl_cantidad CHECK (cantidad >= 0),
    CONSTRAINT ck_alm_movimiento_dtl_costo_no_neg CHECK (costo_unitario >= 0),
    CONSTRAINT ck_alm_movimiento_dtl_costo_real_no_neg CHECK (costo_real IS NULL OR costo_real >= 0),
    -- Un renglón posteado tiene su asiento. Sin kardex_id no está posteado.
    CONSTRAINT ck_alm_movimiento_dtl_posteo
        CHECK (posteado = false OR kardex_id IS NOT NULL),

    CONSTRAINT fk_alm_movimiento_dtl_hdr
        FOREIGN KEY (company_id, movimiento_hdr_id)
        REFERENCES public.alm_movimiento_hdr (company_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_alm_movimiento_dtl_articulo
        FOREIGN KEY (company_id, articulo_id)
        REFERENCES public.alm_articulo (company_id, id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_alm_movimiento_dtl_hdr
    ON public.alm_movimiento_dtl (company_id, movimiento_hdr_id);
CREATE INDEX IF NOT EXISTS ix_alm_movimiento_dtl_articulo
    ON public.alm_movimiento_dtl (company_id, articulo_id);
CREATE INDEX IF NOT EXISTS ix_alm_movimiento_dtl_kardex
    ON public.alm_movimiento_dtl (kardex_id) WHERE kardex_id IS NOT NULL;

COMMENT ON TABLE public.alm_movimiento_dtl IS
 'Renglón del movimiento de almacén. Es la UNIDAD DE POSTEO: cada uno deriva su propio uuid v5 determinista, así que un reintento no duplica un renglón ya insertado mientras otro falló. Mismo patrón que alm_compra y alm_descargo.';
COMMENT ON COLUMN public.alm_movimiento_dtl.costo_unitario IS
 'Costo tecleado por el usuario. Sólo aplica en clase ENTRADA; en SALIDA el motor usa el costo promedio vigente del par (artículo, bodega) y este campo queda en 0.';
COMMENT ON COLUMN public.alm_movimiento_dtl.costo_real IS
 'Costo con el que el motor efectivamente valorizó el asiento. En SALIDA es el promedio vigente al postear; en ENTRADA coincide con costo_unitario.';

-- -----------------------------------------------------------------------------
-- 4) El correlativo arranca en 0 para cada empresa con bodegas
-- -----------------------------------------------------------------------------
INSERT INTO public.alm_movimiento_correlativo (company_id, ultimo_numero)
SELECT DISTINCT company_id, 0 FROM public.alm_bodega
ON CONFLICT (company_id) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- V1) Las 3 tablas existen:
-- SELECT to_regclass('public.alm_movimiento_hdr')  AS hdr,
--        to_regclass('public.alm_movimiento_dtl')  AS dtl,
--        to_regclass('public.alm_movimiento_correlativo') AS corr;
--
-- V2) Las 4 FK son COMPUESTAS (tenant-safe). Esperado: 4 filas, todas con (company_id, ...):
-- SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--  WHERE contype = 'f'
--    AND conrelid IN ('public.alm_movimiento_hdr'::regclass, 'public.alm_movimiento_dtl'::regclass)
--  ORDER BY 1, 2;
--
-- V3) Correlativo sembrado (esperado: company_id 2 → 0):
-- SELECT * FROM alm_movimiento_correlativo ORDER BY company_id;
--
-- V4) PRUEBA NEGATIVA — anular sin motivo debe FALLAR (ck_..._anulacion):
-- BEGIN;
--   INSERT INTO alm_movimiento_hdr (company_id, numero, tipo_movimiento_id, bodega_id, fecha, motivo, estado, anulado_por, fecha_anulacion)
--   SELECT 2, 999001, t.id, b.id, current_date, 'prueba', 9, 'tester', now()
--     FROM alm_tipo_movimiento t, alm_bodega b
--    WHERE t.company_id=2 AND t.codigo='AIS' AND b.company_id=2 LIMIT 1;
-- ROLLBACK;   -- espera 23514
--
-- V5) PRUEBA NEGATIVA — posteado sin uuid debe FALLAR (ck_..._posteo):
-- BEGIN;
--   INSERT INTO alm_movimiento_hdr (company_id, numero, tipo_movimiento_id, bodega_id, fecha, motivo, posteado)
--   SELECT 2, 999002, t.id, b.id, current_date, 'prueba', true
--     FROM alm_tipo_movimiento t, alm_bodega b
--    WHERE t.company_id=2 AND t.codigo='AIS' AND b.company_id=2 LIMIT 1;
-- ROLLBACK;   -- espera 23514
--
-- V6) PRUEBA NEGATIVA — fuga de tenant: bodega de otra empresa debe FALLAR (fk compuesta):
-- BEGIN;
--   INSERT INTO alm_movimiento_hdr (company_id, numero, tipo_movimiento_id, bodega_id, fecha, motivo)
--   SELECT 2, 999003, t.id, 999999, current_date, 'prueba'
--     FROM alm_tipo_movimiento t WHERE t.company_id=2 AND t.codigo='AIS' LIMIT 1;
-- ROLLBACK;   -- espera 23503
--
-- V7) PRUEBA NEGATIVA — número duplicado por empresa debe FALLAR (uq_..._numero).
-- V8) Re-ejecutar el script entero NO duplica nada (comparar V1 y V3).
-- =============================================================================

-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- BEGIN;
-- DROP TABLE IF EXISTS public.alm_movimiento_dtl;
-- DROP TABLE IF EXISTS public.alm_movimiento_hdr;
-- DROP TABLE IF EXISTS public.alm_movimiento_correlativo;
-- COMMIT;
-- =============================================================================
