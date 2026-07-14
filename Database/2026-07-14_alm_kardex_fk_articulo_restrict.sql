-- =============================================================================
-- Kardex: la FK articulo_id pasa de SET NULL a RESTRICT
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ: alm_kardex es un libro mayor. Borrar un artículo que tiene asientos
-- debe ser IMPOSIBLE, no anular la referencia en silencio (que es lo que hace
-- SET NULL: deja los asientos huérfanos y se pierde la trazabilidad).
--
-- Además, SET NULL es hoy una trampa: al borrar un artículo, Postgres ejecuta un
-- UPDATE alm_kardex SET articulo_id = NULL, y ese UPDATE es rechazado por
-- trg_alm_kardex_inmutable (SQLSTATE K0001) — un error críptico en vez de una
-- violación de FK limpia. Con RESTRICT el rechazo es explícito y semánticamente
-- correcto (SQLSTATE 23503).
--
-- Es la contraparte en BD de la guarda de aplicación en
-- SIAD.Services/Almacen/ArticulosService.cs (DeleteAsync), que valida por
-- articulo_id. Defensa en profundidad: la app da el mensaje amable, la BD garantiza.
--
-- ORDEN: aplicar DESPUÉS de Database/2026-07-13_alm_kardex_articulo_id.sql
-- (que crea y backfillea articulo_id; cobertura verificada: 47.203/47.215 = 99,97%.
-- Las 12 filas sin articulo_id tampoco tienen codigo_articulo: son basura de
-- cantidad 0 que no pertenece a ningún artículo, y no bloquean esta constraint).
--
-- No altera datos: solo cambia la regla de borrado de la FK.
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    DROP CONSTRAINT IF EXISTS alm_kardex_articulo_id_fkey;

ALTER TABLE alm_kardex
    ADD CONSTRAINT alm_kardex_articulo_id_fkey
    FOREIGN KEY (articulo_id) REFERENCES alm_articulo(id)
    ON DELETE RESTRICT;

COMMENT ON COLUMN alm_kardex.articulo_id IS 'Artículo del asiento (FK real). RESTRICT: no se puede borrar un artículo que tiene movimientos — el libro mayor no pierde trazabilidad. La columna legacy codigo_articulo es solo un snapshot SIMAFI y NO debe usarse para validar existencia de movimientos (el código es opcional desde 2026-07-13).';

COMMIT;
