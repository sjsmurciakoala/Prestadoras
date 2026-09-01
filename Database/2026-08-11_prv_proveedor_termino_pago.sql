-- =============================================================================
-- Proveedores: término de pago por defecto del proveedor (prv_proveedores.termino_pago_id)
-- Fecha: 2026-08-11
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Asigna a cada proveedor su término de pago habitual (catálogo alm_termino_pago). Al elegir
-- el proveedor en la factura de compra, la pantalla precarga este término y recalcula el
-- vencimiento. Paridad con lo que ventas hace por cliente (CLN_TERMINOS_CLN en Centura).
-- Ver docs/centura-flujos/README_terminos_pago_factura_proveedor.md §7.3
--
-- ADITIVO / bajo riesgo: ADD COLUMN nullable con FK. No borra ni reescribe datos: los
-- proveedores existentes quedan con termino_pago_id NULL y usan el término predeterminado
-- global en la factura.
--
-- IDEMPOTENTE: ADD/CREATE IF [NOT] EXISTS.
-- Depende de: alm_termino_pago (2026-08-11_alm_termino_pago.sql) — aplicar antes.
-- =============================================================================
BEGIN;

ALTER TABLE prv_proveedores
    ADD COLUMN IF NOT EXISTS termino_pago_id INTEGER NULL
        REFERENCES alm_termino_pago(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_prv_proveedores_termino_pago ON prv_proveedores(termino_pago_id);

COMMENT ON COLUMN prv_proveedores.termino_pago_id IS 'Término de pago habitual del proveedor (alm_termino_pago). Se precarga en la factura de compra al elegir el proveedor. NULL = usar el predeterminado global.';

COMMIT;
