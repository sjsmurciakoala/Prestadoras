-- =============================================================================
-- Unidades de almacenaje y de salidas en alm_articulo
-- Fecha: 2026-07-07
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Se agregan dos FKs opcionales al catálogo alm_unidad_medida:
--   * unidad_almacenaje_id — unidad en que se almacena el artículo en bodega.
--   * unidad_salida_id      — unidad en que se despacha/consume el artículo.
--
-- Conviven con unidad_medida_id (unidad general) y con el texto libre legacy
-- unidad_medida. Ambas son NULL por defecto; ON DELETE SET NULL como el resto
-- de FKs de clasificación de alm_articulo.
-- =============================================================================

BEGIN;

ALTER TABLE alm_articulo
    ADD COLUMN IF NOT EXISTS unidad_almacenaje_id INTEGER NULL
        REFERENCES alm_unidad_medida(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS unidad_salida_id     INTEGER NULL
        REFERENCES alm_unidad_medida(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_alm_articulo_unidad_almacenaje ON alm_articulo(unidad_almacenaje_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_unidad_salida     ON alm_articulo(unidad_salida_id);

COMMENT ON COLUMN alm_articulo.unidad_almacenaje_id IS 'FK opcional a alm_unidad_medida: unidad en que se almacena el artículo en bodega.';
COMMENT ON COLUMN alm_articulo.unidad_salida_id     IS 'FK opcional a alm_unidad_medida: unidad en que se despacha/consume el artículo.';

COMMIT;
