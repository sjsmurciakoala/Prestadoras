-- =============================================================================
-- Integración contable de almacén — F0: habilitar module='ALMACEN' en el CHECK
-- Fecha: 2026-08-05
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   F0 de docs/plans/2026-08-05-integracion-contable-almacen-diseno.md.
--   El motor de integración lee el diario + tipo de partida de cada módulo desde
--   con_integracion_asiento (una fila por empresa × módulo). Su columna `module`
--   está acotada por ck_con_integracion_asiento_module a los 6 módulos actuales
--   (VENTAS, CAJA, BANCOS, NOTAS, MISCELANEOS, PROV). Para que una empresa pueda
--   configurar el asiento de almacén hay que permitir el valor 'ALMACEN'.
--
-- WIDENING de un CHECK (superconjunto: solo agrega 'ALMACEN'). Sin DROP de datos,
--   sin DELETE/TRUNCATE. Ninguna fila existente viola el CHECK nuevo.
-- IDEMPOTENTE  (DROP CONSTRAINT IF EXISTS + ADD; re-correrlo deja el mismo CHECK).
-- REVERSIBLE   (bloque de ROLLBACK comentado al final).
--
-- Con binario: IntegracionContableModulos.Todos (C#) y el HasCheckConstraint de
--   EF se amplían con 'ALMACEN' en el mismo commit.
-- =============================================================================
BEGIN;

ALTER TABLE public.con_integracion_asiento
    DROP CONSTRAINT IF EXISTS ck_con_integracion_asiento_module;

ALTER TABLE public.con_integracion_asiento
    ADD CONSTRAINT ck_con_integracion_asiento_module
    CHECK (module IN ('VENTAS','CAJA','BANCOS','NOTAS','MISCELANEOS','PROV','ALMACEN'));

COMMIT;

-- =============================================================================
-- ROLLBACK (ejecutar manualmente SOLO si hay que revertir F0):
--   Requiere que NO existan filas con module='ALMACEN' (bórralas antes).
--   ALTER TABLE public.con_integracion_asiento
--       DROP CONSTRAINT IF EXISTS ck_con_integracion_asiento_module;
--   ALTER TABLE public.con_integracion_asiento
--       ADD CONSTRAINT ck_con_integracion_asiento_module
--       CHECK (module IN ('VENTAS','CAJA','BANCOS','NOTAS','MISCELANEOS','PROV'));
-- =============================================================================
