-- =============================================================================
-- Documentos generados por tipo de acción de cobranza
-- Fecha: 2026-06-29
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- 1. axl_accion_cobranza: marca qué acciones generan documento y cuál.
-- 2. cln_accion_cobranza_documento: snapshot del PDF generado (archivado).
-- 3. Seed: acción 1 (Carta de Cobranza Prejudicial) genera documento.
-- =============================================================================

BEGIN;

-- ── 1. Columnas de configuración en el catálogo ──────────────────────────────
ALTER TABLE axl_accion_cobranza
    ADD COLUMN IF NOT EXISTS genera_documento  BOOLEAN     NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS documento_codigo  VARCHAR(50) NULL;

COMMENT ON COLUMN axl_accion_cobranza.genera_documento
    IS 'Si true, al registrar la acción se genera y archiva un documento.';
COMMENT ON COLUMN axl_accion_cobranza.documento_codigo
    IS 'Código del generador de documento (p. ej. CARTA_COBRANZA_PREJUDICIAL).';

-- ── 2. Tabla de snapshots de documentos ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS cln_accion_cobranza_documento (
    id               SERIAL       PRIMARY KEY,
    company_id       BIGINT       NOT NULL,
    accion_id        INTEGER      NOT NULL,
    documento_codigo VARCHAR(50)  NOT NULL,
    nombre_archivo   VARCHAR(200) NOT NULL,
    contenido        BYTEA        NOT NULL,
    content_type     VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
    generado_en      TIMESTAMP    NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    generado_por     VARCHAR(100) NULL,
    CONSTRAINT cln_accion_cobranza_documento_accion_fkey
        FOREIGN KEY (accion_id) REFERENCES cln_accion_cobranza(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_cln_accion_cobranza_documento_accion_id
    ON cln_accion_cobranza_documento(accion_id);

CREATE INDEX IF NOT EXISTS ix_cln_accion_cobranza_documento_company
    ON cln_accion_cobranza_documento(company_id);

COMMENT ON TABLE cln_accion_cobranza_documento
    IS 'Snapshot (PDF) del documento entregado al cliente por una acción de cobranza.';

-- ── 3. Seed: acción 1 → Carta de Cobranza Prejudicial ────────────────────────
UPDATE axl_accion_cobranza
   SET genera_documento = true,
       documento_codigo = 'CARTA_COBRANZA_PREJUDICIAL'
 WHERE cod_accion = 1;

COMMIT;
