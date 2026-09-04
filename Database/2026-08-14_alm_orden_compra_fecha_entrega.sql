-- =============================================================================
-- Órdenes de compra — fecha de entrega pactada (cabecera y renglón)
-- Fecha: 2026-08-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- La O/C guarda `fecha`, `fecha_emision` y `fecha_aprobacion`, pero NO cuándo se
-- comprometió el proveedor a entregar. Sin ese dato la puntualidad del proveedor
-- no es medible: lo único comparable contra la recepción sería la fecha de
-- aprobación, que mide el ciclo interno, no el cumplimiento del proveedor.
-- Es el insumo del criterio de mayor peso del scorecard de proveedores
-- (docs/prototipos/2026-08-14-evaluacion-proveedores.html).
--
-- QUÉ SE AGREGA
--   1) alm_orden_compra.fecha_entrega_pactada          -> fecha pactada de la orden.
--   2) alm_orden_compra_detalle.fecha_entrega_pactada  -> fecha pactada del renglón
--      (entregas escalonadas). NULL = rige la de la cabecera.
--
-- DECISIONES (usuario, 2026-08-14):
--   - La fecha vive en la CABECERA y, opcionalmente, POR RENGLÓN.
--   - Es OBLIGATORIA desde el borrador… pero la obligatoriedad la impone el
--     SERVICIO (OrdenCompraService), no un NOT NULL: las órdenes ya emitidas antes
--     de este script no la tienen y deben seguir abriéndose y recibiéndose.
--   - SIN CHECK de "no anterior a la fecha de la orden": esa regla también vive en
--     el servicio, que puede devolver un 400 con mensaje en vez de un 500 de BD.
--   - Sin índice nuevo: el reporte de puntualidad recorre las O/C de un período,
--     que ya están acotadas por ix_alm_orden_compra_company / _estado.
--
-- Cambio ADITIVO y reversible: dos columnas NULL sin DEFAULT. No reescribe filas.
-- Revertir:
--   ALTER TABLE alm_orden_compra         DROP COLUMN fecha_entrega_pactada;
--   ALTER TABLE alm_orden_compra_detalle DROP COLUMN fecha_entrega_pactada;
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Cabecera: fecha pactada de la orden
-- -----------------------------------------------------------------------------
ALTER TABLE alm_orden_compra
    ADD COLUMN IF NOT EXISTS fecha_entrega_pactada DATE NULL;

COMMENT ON COLUMN alm_orden_compra.fecha_entrega_pactada IS
    'Fecha en que el proveedor se compromete a entregar. Obligatoria en la captura (servicio); NULL sólo en las órdenes anteriores al 2026-08-14. Base del criterio de puntualidad del proveedor.';

-- -----------------------------------------------------------------------------
-- 2) Renglón: fecha pactada propia (entregas escalonadas)
-- -----------------------------------------------------------------------------
ALTER TABLE alm_orden_compra_detalle
    ADD COLUMN IF NOT EXISTS fecha_entrega_pactada DATE NULL;

COMMENT ON COLUMN alm_orden_compra_detalle.fecha_entrega_pactada IS
    'Fecha pactada de ESTE renglón cuando la entrega es escalonada. NULL = rige la fecha de la cabecera.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Las dos columnas existen y son DATE NULL:
-- SELECT table_name, column_name, data_type, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE column_name = 'fecha_entrega_pactada'
--    AND table_name IN ('alm_orden_compra','alm_orden_compra_detalle')
--  ORDER BY table_name;
--   -> 2 filas · data_type = date · is_nullable = YES · column_default = NULL
--
-- 2) Ninguna orden existente fue tocada (todas quedan en NULL):
-- SELECT count(*) AS ordenes, count(fecha_entrega_pactada) AS con_fecha
--   FROM alm_orden_compra;
--   -> con_fecha = 0 justo después de aplicar
--
-- 3) Los comentarios quedaron registrados:
-- SELECT col_description('alm_orden_compra'::regclass,
--          (SELECT attnum FROM pg_attribute
--            WHERE attrelid='alm_orden_compra'::regclass AND attname='fecha_entrega_pactada'));
-- =============================================================================
