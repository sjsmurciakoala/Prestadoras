-- =============================================================================
-- Cuentas contables por tipo de artículo — alm_tipo_articulo
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Agrega 5 cuentas contables (código del plan de cuentas) al tipo de artículo:
-- inventario, costo de ventas, ventas, ajustes y devoluciones. Se guardan como
-- código (VARCHAR) igual que alm_articulo.cuenta_contable; el selector muestra el
-- catálogo de cuentas. Opcionales (NULL) y sin FK: se validan/seleccionan en la UI.
-- =============================================================================
BEGIN;

ALTER TABLE alm_tipo_articulo
    ADD COLUMN IF NOT EXISTS cuenta_inventario   VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS cuenta_costo_ventas VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS cuenta_ventas       VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS cuenta_ajustes      VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS cuenta_devoluciones VARCHAR(20) NULL;

COMMENT ON COLUMN alm_tipo_articulo.cuenta_inventario   IS 'Cuenta contable de inventario (código del plan de cuentas).';
COMMENT ON COLUMN alm_tipo_articulo.cuenta_costo_ventas IS 'Cuenta contable de costo de ventas (código del plan de cuentas).';
COMMENT ON COLUMN alm_tipo_articulo.cuenta_ventas       IS 'Cuenta contable de ventas (código del plan de cuentas).';
COMMENT ON COLUMN alm_tipo_articulo.cuenta_ajustes      IS 'Cuenta contable de ajustes (código del plan de cuentas).';
COMMENT ON COLUMN alm_tipo_articulo.cuenta_devoluciones IS 'Cuenta contable de devoluciones (código del plan de cuentas).';

COMMIT;
