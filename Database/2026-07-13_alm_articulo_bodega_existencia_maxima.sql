-- =============================================================================
-- Existencia máxima por bodega — alm_articulo_bodega.existencia_maxima
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Agrega el control de existencia MÁXIMA por bodega, al par de existencia_minima.
-- Mismo tipo/precisión que existencia_minima. Por ahora es informativo (nivel de
-- reorden superior); no dispara lógica automática.
-- =============================================================================
BEGIN;

ALTER TABLE alm_articulo_bodega
    ADD COLUMN IF NOT EXISTS existencia_maxima NUMERIC(11,2) NOT NULL DEFAULT 0;

COMMENT ON COLUMN alm_articulo_bodega.existencia_maxima IS 'Existencia máxima (nivel superior) del artículo en esa bodega. Al par de existencia_minima.';

COMMIT;
