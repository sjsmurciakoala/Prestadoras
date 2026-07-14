-- =============================================================================
-- Almacén: soft-delete de ubicaciones del artículo (alm_articulo_bodega.activo)
-- Fecha: 2026-07-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Las ubicaciones (bodegas) de un artículo ya NO se eliminan físicamente: se
-- DESHABILITAN (activo = false) para conservar el histórico de dónde estuvo el
-- artículo. El rollup de existencia (alm_articulo.existencia) suma solo las filas
-- activas. Toda ubicación existente al aplicar esta migración queda activa.
-- =============================================================================
BEGIN;

ALTER TABLE alm_articulo_bodega
    ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;

COMMENT ON COLUMN alm_articulo_bodega.activo IS
    'Soft-delete: false = ubicación deshabilitada para el artículo (se conserva para histórico). El rollup de existencia y las alertas de stock consideran solo filas activas.';

-- Índice parcial para filtrar rápido las ubicaciones activas por artículo.
CREATE INDEX IF NOT EXISTS ix_alm_articulo_bodega_articulo_activo
    ON alm_articulo_bodega(articulo_id) WHERE activo;

COMMIT;
