-- =============================================================================
-- Kardex: ampliar precisiones heredadas de SIMAFI
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ: alm_kardex viene de MySQL (bdsimafi.inventariotra) con NUMERIC(11,2)
-- en todas sus cifras. Eso TRUNCA el asiento respecto de los saldos que alimenta:
--   * alm_articulo_bodega.existencia      es NUMERIC(15,2)
--   * alm_articulo_bodega.costo_promedio  es NUMERIC(12,4)
--   * alm_articulo_bodega.ultimo_costo    es NUMERIC(12,4)
-- Con (11,2) un costo unitario de L 0.0450 se guardaría como 0.05 y el kardex
-- dejaría de cuadrar contra el saldo que él mismo produce. Para un libro mayor
-- inmutable eso es inaceptable: el asiento debe tener al menos la precisión del
-- saldo que deriva de él.
--
-- Se amplía:
--   valor_unitario -> NUMERIC(14,4)  (4 decimales, igual que costo_promedio)
--   cantidad/ingresos/salidas -> NUMERIC(15,2)  (igual que existencia)
--   total/debe/haber -> NUMERIC(17,4)  (cantidad(15,2) x valor_unitario(14,4) sin truncar)
--
-- Es WIDENING puro (misma escala o mayor, misma o mayor precisión): no pierde
-- datos, no requiere backfill y Postgres reescribe la tabla en una sola pasada.
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    ALTER COLUMN valor_unitario TYPE NUMERIC(14,4),
    ALTER COLUMN cantidad       TYPE NUMERIC(15,2),
    ALTER COLUMN ingresos       TYPE NUMERIC(15,2),
    ALTER COLUMN salidas        TYPE NUMERIC(15,2),
    ALTER COLUMN total          TYPE NUMERIC(17,4),
    ALTER COLUMN debe           TYPE NUMERIC(17,4),
    ALTER COLUMN haber          TYPE NUMERIC(17,4);

COMMENT ON COLUMN alm_kardex.valor_unitario IS 'Costo unitario del movimiento. 4 decimales para no truncar contra alm_articulo_bodega.costo_promedio NUMERIC(12,4).';
COMMENT ON COLUMN alm_kardex.total IS 'cantidad x valor_unitario. 4 decimales para no truncar el producto.';

COMMIT;
