-- =============================================================================
-- Existencia comprometida / en tránsito y costos por bodega — alm_articulo_bodega
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Consolida el modelo de stock a NIVEL BODEGA (fuente de verdad = alm_articulo_bodega;
-- alm_articulo.existencia queda como rollup). Agrega cuatro saldos que el kardex
-- mantiene (no se teclean a mano):
--   - existencia_comprometida : stock reservado por requisiciones aprobadas sin despachar.
--   - existencia_transito     : cantidad en camino (compra abierta o traslado entre bodegas).
--   - costo_promedio          : costo promedio ponderado, recalculado en cada ingreso.
--   - ultimo_costo            : precio de la última compra registrada (referencia).
-- Con ellos, el "disponible" = existencia - comprometida (+ tránsito) se calcula en servicio.
--
-- Precisiones alineadas con lo existente: existencia (15,2) como alm_articulo_bodega.existencia;
-- costos (12,4) como alm_articulo.valor_unitario / alm_articulo_proveedor.costo.
-- Seguro de correr: idempotente (ADD COLUMN IF NOT EXISTS, DEFAULT 0). No borra ni renombra.
-- =============================================================================
BEGIN;

ALTER TABLE alm_articulo_bodega
    ADD COLUMN IF NOT EXISTS existencia_comprometida NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS existencia_transito     NUMERIC(15,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS costo_promedio          NUMERIC(12,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS ultimo_costo            NUMERIC(12,4) NOT NULL DEFAULT 0;

COMMENT ON COLUMN alm_articulo_bodega.existencia_comprometida IS 'Stock físicamente presente pero reservado (requisiciones aprobadas sin despachar). Lo mantiene el servicio, no se edita a mano.';
COMMENT ON COLUMN alm_articulo_bodega.existencia_transito     IS 'Cantidad en camino a esta bodega (orden de compra abierta o traslado desde otra bodega). Ingresa a existencia al recibir.';
COMMENT ON COLUMN alm_articulo_bodega.costo_promedio          IS 'Costo promedio ponderado del artículo en esta bodega; se recalcula en cada ingreso (kardex).';
COMMENT ON COLUMN alm_articulo_bodega.ultimo_costo            IS 'Precio unitario de la última compra registrada del artículo en esta bodega (referencia).';

COMMIT;
