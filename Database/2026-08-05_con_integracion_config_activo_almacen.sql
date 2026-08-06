-- =============================================================================
-- Integración contable de almacén — F0: bandera de activación por empresa
-- Fecha: 2026-08-05
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   F0 de docs/plans/2026-08-05-integracion-contable-almacen-diseno.md.
--   Habilita que el módulo de almacén (movimientos alm_*) genere su partida
--   contable por el MISMO motor de integración que Ventas/Caja/Bancos
--   (sp_con_generar_comprobante_config / IntegracionContableConfigSql).
--   con_integracion_config ya lleva un flag por módulo (activo_facturacion,
--   activo_caja, activo_bancos, activo_notas, activo_miscelaneos,
--   activo_proveedores); solo falta el de almacén.
--
--   NO enciende nada por sí solo: DEFAULT false = comportamiento actual (el
--   almacén NO postea al mayor). El almacén solo contabiliza cuando, por empresa:
--     (1) activo_almacen = true, y
--     (2) existe la fila de diario + tipo de partida en con_integracion_asiento
--         con module = 'ALMACEN' (pantalla de Integración Contable).
--
-- ADITIVO. Sin DROP, sin DELETE/TRUNCATE, sin cambio de datos existentes.
-- IDEMPOTENTE  (ADD COLUMN IF NOT EXISTS; re-correrlo no hace nada).
-- REVERSIBLE   (bloque de ROLLBACK comentado al final).
-- MULTIEMPRESA (con_integracion_config es una fila por empresa; el flag aplica a
--               todas con DEFAULT false, sin encender ninguna).
-- =============================================================================
BEGIN;

ALTER TABLE public.con_integracion_config
    ADD COLUMN IF NOT EXISTS activo_almacen boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN public.con_integracion_config.activo_almacen IS
    'F0 integracion contable de almacen (2026-08-05): si true, los movimientos de almacen (alm_*) generan partida via sp_con_generar_comprobante_config con module=ALMACEN. Requiere ademas la fila de diario+tipo en con_integracion_asiento. DEFAULT false = no postea.';

COMMIT;

-- =============================================================================
-- ROLLBACK (ejecutar manualmente SOLO si hay que revertir F0):
--   ALTER TABLE public.con_integracion_config DROP COLUMN IF EXISTS activo_almacen;
-- =============================================================================
