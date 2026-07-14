-- =============================================================================
-- Almacén: punto de reorden por bodega + tipos de artículo que NO manejan inventario
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- 1) alm_articulo_bodega.punto_reorden
--    Umbral que dispara la reposición. Es DISTINTO de existencia_minima:
--      - existencia_minima : stock de seguridad (el colchón que no se debe tocar)
--      - punto_reorden     : cuándo hay que pedir (normalmente > mínimo, porque
--                            debe cubrir el consumo durante el tiempo de entrega)
--      - existencia_maxima : tope superior (cuánto pedir sin sobre-stockear)
--
-- 2) alm_tipo_articulo.maneja_inventario
--    false = el tipo NO maneja existencias (p.ej. 'Servicios'): sus artículos no
--    llevan bodega, no tienen ubicación, no afectan el kardex y no se les postea
--    ningún movimiento.
--    La regla se aplica en la CAPA DE SERVICIO (C#), NO con triggers: es una regla
--    de negocio de la app, no un invariante del dato. (Decisión explícita del
--    usuario: "para estos tipos de datos no se deben utilizar disparadores".)
--    Contraste: el blindaje del kardex y del histórico SIMAFI SÍ usa triggers,
--    porque ahí sí protegemos un invariante contra cualquier vía de escritura.
--
-- Cambio aditivo: no altera columnas ni datos existentes.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Punto de reorden por bodega
-- ---------------------------------------------------------------------------
ALTER TABLE alm_articulo_bodega
    ADD COLUMN IF NOT EXISTS punto_reorden NUMERIC(15,2) NOT NULL DEFAULT 0;

COMMENT ON COLUMN alm_articulo_bodega.punto_reorden IS
    'Umbral que dispara la reposición del artículo en esa bodega. Distinto de existencia_minima (stock de seguridad) y de existencia_maxima (tope). La alerta se calcula sobre el disponible: existencia - comprometida + transito < punto_reorden.';

-- ---------------------------------------------------------------------------
-- 2) Tipos de artículo que no manejan inventario (servicios)
-- ---------------------------------------------------------------------------
-- Sin DEFAULT en el ADD COLUMN (PG11+ lo estamparía a las filas existentes, que
-- es justo lo que queremos aquí: todos los tipos actuales SÍ manejan inventario).
-- Se usa el patrón explícito de tres pasos para que quede legible y re-ejecutable.
ALTER TABLE alm_tipo_articulo
    ADD COLUMN IF NOT EXISTS maneja_inventario BOOLEAN;

UPDATE alm_tipo_articulo SET maneja_inventario = true WHERE maneja_inventario IS NULL;

ALTER TABLE alm_tipo_articulo
    ALTER COLUMN maneja_inventario SET DEFAULT true,
    ALTER COLUMN maneja_inventario SET NOT NULL;

COMMENT ON COLUMN alm_tipo_articulo.maneja_inventario IS
    'false = los artículos de este tipo NO llevan existencias: sin bodega, sin ubicación, sin kardex, sin posteo de movimientos (p.ej. Servicios). La regla se aplica en la capa de servicio (C#), no con triggers.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- SELECT codigo, nombre, maneja_inventario FROM alm_tipo_articulo ORDER BY codigo;
--   -> todos true. Marcar SERV en false se hace desde el mantenimiento de Tipos.
--
-- SELECT count(*) FROM alm_articulo_bodega WHERE punto_reorden <> 0;
--   -> 0 (nace en cero; se captura por bodega en la pestaña Existencias).
-- =============================================================================
