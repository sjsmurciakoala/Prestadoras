-- =============================================================================
-- Almacén: ubicación manual del artículo por bodega (reemplaza estanterías/estantes)
-- Fecha: 2026-07-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- El módulo de almacén deja de usar el catálogo formal de 3 niveles
-- (bodega -> estantería -> estante). La ubicación física del artículo pasa a ser
-- MANUAL: cinco campos de texto libre de 20 caracteres en alm_articulo_bodega
-- (ubicacion1..ubicacion5), que el usuario captura a mano.
--
-- Cambios:
--   1. alm_articulo_bodega: se elimina estante_id y su FK/índice; se agregan
--      ubicacion1..ubicacion5 VARCHAR(20) NULL.
--   2. Se eliminan las tablas alm_estante y alm_estanteria (ya sin referencias).
-- =============================================================================
BEGIN;

-- 1. Ubicación manual en alm_articulo_bodega -------------------------------------
DROP INDEX IF EXISTS ix_alm_articulo_bodega_estante;

-- Al eliminar la columna se elimina también su FK a alm_estante.
ALTER TABLE alm_articulo_bodega DROP COLUMN IF EXISTS estante_id;

ALTER TABLE alm_articulo_bodega
    ADD COLUMN IF NOT EXISTS ubicacion1 VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS ubicacion2 VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS ubicacion3 VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS ubicacion4 VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS ubicacion5 VARCHAR(20) NULL;

COMMENT ON COLUMN alm_articulo_bodega.ubicacion1 IS 'Ubicación física manual del artículo en la bodega (texto libre, opcional).';
COMMENT ON COLUMN alm_articulo_bodega.ubicacion2 IS 'Ubicación física manual del artículo en la bodega (texto libre, opcional).';
COMMENT ON COLUMN alm_articulo_bodega.ubicacion3 IS 'Ubicación física manual del artículo en la bodega (texto libre, opcional).';
COMMENT ON COLUMN alm_articulo_bodega.ubicacion4 IS 'Ubicación física manual del artículo en la bodega (texto libre, opcional).';
COMMENT ON COLUMN alm_articulo_bodega.ubicacion5 IS 'Ubicación física manual del artículo en la bodega (texto libre, opcional).';

-- 2. Baja del catálogo formal de estanterías/estantes ---------------------------
-- alm_estante referencia a alm_estanteria, por lo que se elimina primero.
DROP TABLE IF EXISTS alm_estante;
DROP TABLE IF EXISTS alm_estanteria;

COMMIT;
