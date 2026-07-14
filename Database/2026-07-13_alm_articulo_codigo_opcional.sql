-- =============================================================================
-- Almacén: el código del artículo (codigo_articulo) pasa a ser OPCIONAL
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- El identificador de negocio de los artículos nuevos pasa a ser el id (PK,
-- autoincremental). El codigo_articulo queda solo como referencia del sistema
-- anterior (SIMAFI) en los artículos migrados; los nuevos lo dejan en blanco.
--
-- Para permitir varios artículos nuevos sin código, la unicidad de
-- (company_id, codigo_articulo) pasa a ser PARCIAL: solo aplica cuando el código
-- tiene valor. No se borra ni modifica ningún dato existente.
-- =============================================================================
BEGIN;

ALTER TABLE alm_articulo DROP CONSTRAINT IF EXISTS uq_alm_articulo_company_codigo;
DROP INDEX IF EXISTS uq_alm_articulo_company_codigo;

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_articulo_company_codigo
    ON alm_articulo (company_id, codigo_articulo)
    WHERE codigo_articulo IS NOT NULL AND codigo_articulo <> '';

COMMIT;
