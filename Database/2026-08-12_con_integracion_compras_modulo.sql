-- =============================================================================
-- Contabilidad: módulo propio COMPRAS en la integración contable
-- Fecha: 2026-08-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor (siad_v3).
--
-- Fase 2 de compras a proveedor. Convierte Compras/CxP en un MÓDULO contable independiente
-- (como VENTAS, CAJA, BANCOS, PROV, ALMACEN), separado de inventario. Gatea con su propio flag
-- los DOS asientos del ciclo de compra, ambos con module='COMPRAS':
--   · factura de compra → DEBE inventario / HABER proveedor
--   · pago de la CxP     → DEBE proveedor / HABER banco
--
-- Tres piezas, como todo módulo contable:
--   0) admitir 'COMPRAS' en el CHECK de módulos de con_integracion_asiento (la lista fija no lo tenía)
--   1) flag  con_integracion_config.activo_compras  (apagado por defecto)
--   2) asiento con_integracion_asiento (module='COMPRAS') → diario + tipo de partida
--      (el motor sp_con_generar_comprobante_config EXIGE esta fila; sin ella lanza excepción).
--
-- ADITIVO / bajo riesgo. IDEMPOTENTE: ADD COLUMN IF NOT EXISTS + INSERT … WHERE NOT EXISTS +
-- re-creación del CHECK con la lista AMPLIADA (agrega 'COMPRAS', no quita ninguno).
-- Apagado por defecto: con el flag apagado el flujo sigue sin pólizas, exactamente como hoy.
-- =============================================================================
BEGIN;

-- 0) Ampliar el CHECK de módulos para admitir 'COMPRAS' (agrega, no quita: los 7 módulos previos
--    siguen válidos). El motor valida el module contra esta tabla, así que sin esto el INSERT (2) falla.
ALTER TABLE con_integracion_asiento DROP CONSTRAINT IF EXISTS ck_con_integracion_asiento_module;
ALTER TABLE con_integracion_asiento ADD CONSTRAINT ck_con_integracion_asiento_module
    CHECK (module IN ('VENTAS','CAJA','BANCOS','NOTAS','MISCELANEOS','PROV','ALMACEN','COMPRAS'));

-- 1) Flag del módulo COMPRAS (apagado por defecto).
ALTER TABLE con_integracion_config
    ADD COLUMN IF NOT EXISTS activo_compras BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN con_integracion_config.activo_compras IS
    'Módulo contable COMPRAS: gatea el asiento de la factura de compra y del pago de la CxP (ambos module=COMPRAS). Apagado = flujo sin pólizas, como hoy.';

-- 2) Asiento (diario/tipo) del módulo COMPRAS. Se siembra como default copiando el de ALMACEN
--    (compras es entrada de inventario); el contador lo ajusta en la pantalla de Integración
--    Contable. Sólo para empresas que ya tienen el asiento de ALMACEN y aún no el de COMPRAS.
INSERT INTO con_integracion_asiento (company_id, module, journal_id, type_id)
SELECT a.company_id, 'COMPRAS', a.journal_id, a.type_id
  FROM con_integracion_asiento a
 WHERE a.module = 'ALMACEN'
   AND NOT EXISTS (
        SELECT 1 FROM con_integracion_asiento x
         WHERE x.company_id = a.company_id AND x.module = 'COMPRAS');

COMMIT;

-- =============================================================================
-- Verificación (¿ya aplicado?):
-- =============================================================================
-- SELECT (SELECT count(*) FROM information_schema.columns
--          WHERE table_name='con_integracion_config' AND column_name='activo_compras') AS flag,   -- esperado 1
--        (SELECT count(*) FROM con_integracion_asiento WHERE module='COMPRAS') AS asientos_compras; -- >= 1 por empresa con ALMACEN
