-- =============================================================================
-- Kardex: trazabilidad al documento origen, idempotencia, snapshot de saldos
-- e inmutabilidad
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Prepara alm_kardex para funcionar como LIBRO MAYOR INMUTABLE de inventario
-- (solo INSERT; nunca UPDATE ni DELETE):
--   * uuid                        -> idempotencia por MOVIMIENTO (línea): reintentar no duplica.
--   * documento_tipo/documento_id -> trazabilidad al documento que originó el asiento.
--   * bodega_destino_id           -> referencia informativa en traslados (ver comentario).
--   * existencia_resultante       -> snapshot del saldo DESPUÉS de este asiento.
--   * costo_promedio_resultante   -> snapshot del costo promedio DESPUÉS de este asiento.
--   * usuariocreacion/fechacreacion -> auditoría del asiento.
--
-- Además:
--   * CHECK de vocabulario cerrado en documento_tipo (espejo de
--     SIAD.Core.Constants.TipoDocumentoInventario).
--   * Trigger que rechaza UPDATE/DELETE: la corrección se hace con contra-asiento (reversa).
--   * Índices tenant-first (company_id al frente) + índice caliente de saldo.
--
-- Cambio aditivo: no altera columnas ni datos existentes. Las filas del histórico
-- SIMAFI quedan con las 8 columnas nuevas en NULL (el DEFAULT de fechacreacion se
-- agrega en un statement aparte, DESPUÉS del ADD COLUMN, para no estamparle a las
-- filas viejas la fecha de la migración). Verificado que ningún código actual hace
-- UPDATE ni DELETE sobre alm_kardex (hoy es solo lectura).
--
-- ORDEN DE APLICACIÓN — IMPORTANTE
-- Este script debe aplicarse DESPUÉS de:
--     Database/2026-07-09_alm_kardex_bodega_id.sql
--     Database/2026-07-13_alm_kardex_articulo_id.sql
-- Ambos hacen UPDATE alm_kardex (backfill) y se declaran re-ejecutables; una vez
-- activo trg_alm_kardex_inmutable, re-correrlos FALLA.
--
-- ESCOTILLA DE ESCAPE — corrección puntual de datos con el trigger activo
-- El trigger es el invariante del módulo; deshabilitarlo es excepcional y debe
-- quedar documentado (script fechado en Database/):
--     ALTER TABLE alm_kardex DISABLE TRIGGER trg_alm_kardex_inmutable;
--     -- ... corrección puntual, documentada ...
--     ALTER TABLE alm_kardex ENABLE TRIGGER trg_alm_kardex_inmutable;
-- La vía normal de corrección NO es ésta: es postear un contra-asiento (reversa).
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    ADD COLUMN IF NOT EXISTS uuid                      UUID,
    ADD COLUMN IF NOT EXISTS documento_tipo            VARCHAR(20),
    ADD COLUMN IF NOT EXISTS documento_id              INT,
    ADD COLUMN IF NOT EXISTS bodega_destino_id         INT REFERENCES alm_bodega(id),
    ADD COLUMN IF NOT EXISTS existencia_resultante     NUMERIC(15,2),
    ADD COLUMN IF NOT EXISTS costo_promedio_resultante NUMERIC(12,4),
    ADD COLUMN IF NOT EXISTS usuariocreacion           VARCHAR(100),
    ADD COLUMN IF NOT EXISTS fechacreacion             TIMESTAMP WITHOUT TIME ZONE;

-- El DEFAULT va en un statement APARTE, después del ADD COLUMN: en Postgres 11+,
-- "ADD COLUMN ... DEFAULT <expr>" evalúa la expresión y se la estampa a las filas
-- existentes. Eso le pondría a todo el histórico SIMAFI la fecha de la migración
-- (y el grid del kardex lo mostraría como fecha de creación). Así, las filas viejas
-- quedan NULL — honesto: no sabemos cuándo se crearon — y solo las nuevas reciben
-- el default. Revertirlo después sería imposible sin desactivar el trigger.
ALTER TABLE alm_kardex
    ALTER COLUMN fechacreacion SET DEFAULT (now() AT TIME ZONE 'utc');

-- Vocabulario cerrado del documento origen.
ALTER TABLE alm_kardex
    DROP CONSTRAINT IF EXISTS ck_alm_kardex_documento_tipo;
ALTER TABLE alm_kardex
    ADD CONSTRAINT ck_alm_kardex_documento_tipo CHECK (
        documento_tipo IS NULL OR documento_tipo IN
        ('COMPRA','REQUISICION','DESCARGO','TRASLADO','AJUSTE','CARGA_INICIAL')
    );

-- Idempotencia: un uuid no puede postearse dos veces en la misma empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_kardex_company_uuid
    ON alm_kardex(company_id, uuid) WHERE uuid IS NOT NULL;

-- Trazabilidad: "todos los asientos de la compra 123" (tenant-first).
CREATE INDEX IF NOT EXISTS ix_alm_kardex_documento
    ON alm_kardex(company_id, documento_tipo, documento_id);

-- Índice caliente del motor de posteo y del kardex por artículo/bodega:
-- último saldo y saldo corrido de (articulo, bodega) en orden cronológico.
CREATE INDEX IF NOT EXISTS ix_alm_kardex_saldo
    ON alm_kardex(company_id, articulo_id, bodega_id, fecha, id);

-- FK sin índice.
CREATE INDEX IF NOT EXISTS ix_alm_kardex_bodega_destino
    ON alm_kardex(bodega_destino_id);

-- -----------------------------------------------------------------------------
-- Inmutabilidad: alm_kardex es un libro mayor. Solo INSERT.
-- -----------------------------------------------------------------------------
-- SQLSTATE propio (K0001) para poder distinguir esta violación programáticamente
-- desde Npgsql (PostgresException.SqlState) sin parsear el texto del mensaje.
-- El genérico P0001 lo comparten todos los RAISE EXCEPTION sin ERRCODE.
CREATE OR REPLACE FUNCTION alm_kardex_inmutable() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'alm_kardex es un libro mayor inmutable: solo se permite INSERT. Para corregir, postee un contra-asiento (reversa).'
        USING ERRCODE = 'K0001';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_alm_kardex_inmutable ON alm_kardex;
CREATE TRIGGER trg_alm_kardex_inmutable
    BEFORE UPDATE OR DELETE ON alm_kardex
    FOR EACH ROW EXECUTE FUNCTION alm_kardex_inmutable();

COMMENT ON FUNCTION alm_kardex_inmutable() IS 'Guardián de inmutabilidad de alm_kardex: rechaza UPDATE y DELETE con SQLSTATE K0001. Las correcciones se hacen con contra-asiento (reversa).';

-- -----------------------------------------------------------------------------
-- Comentarios de contrato
-- -----------------------------------------------------------------------------
COMMENT ON TABLE alm_kardex IS 'Libro mayor inmutable de movimientos de inventario. Solo INSERT (protegido por trg_alm_kardex_inmutable); las correcciones se hacen con contra-asiento. Las filas con uuid NULL son el histórico SIMAFI, no posteado por el motor.';
COMMENT ON COLUMN alm_kardex.uuid IS 'Idempotencia por MOVIMIENTO (línea), no por documento. Determinista desde la identidad del movimiento: (documento_tipo, documento_id, línea), o (articulo_id, bodega_id) en CARGA_INICIAL. Reintentar el mismo posteo no duplica el asiento. NULL = histórico SIMAFI.';
COMMENT ON COLUMN alm_kardex.documento_tipo IS 'Tipo del documento que originó el asiento. Vocabulario cerrado: COMPRA, REQUISICION, DESCARGO, TRASLADO, AJUSTE, CARGA_INICIAL.';
COMMENT ON COLUMN alm_kardex.documento_id IS 'Id del documento origen dentro de la tabla que corresponde a documento_tipo. Trazabilidad asiento -> documento.';
COMMENT ON COLUMN alm_kardex.bodega_destino_id IS 'Solo referencia informativa en traslados. La bodega AFECTADA por este asiento es SIEMPRE bodega_id; un traslado se postea como DOS asientos (envío y recepción), uno por bodega.';
COMMENT ON COLUMN alm_kardex.existencia_resultante IS 'Saldo de existencia del par (articulo_id, bodega_id) DESPUÉS de este asiento.';
COMMENT ON COLUMN alm_kardex.costo_promedio_resultante IS 'Costo promedio del par (articulo_id, bodega_id) DESPUÉS de este asiento.';
COMMENT ON COLUMN alm_kardex.usuariocreacion IS 'Usuario que posteó el asiento (auditoría).';
COMMENT ON COLUMN alm_kardex.fechacreacion IS 'Fecha/hora UTC del posteo (auditoría). Distinta de fecha, que es la fecha contable del movimiento.';

COMMIT;
