-- ============================================================
-- Corrige el departamento del tipo de orden de corte (33).
-- El corte masivo crea OT con tipo='33', pero en tipo_d ese tipo
-- estaba fichado bajo depto_appmitrabajo='01' (Agua), por lo que
-- las órdenes no aparecían en la pestaña "Órdenes de Corte" (depto '03').
-- Se reasigna al departamento '03' (Corte).
-- Fecha: 2026-06-23
-- Regla DB Mirror: aplicar en SRV (siad_v3) y en localhost (siad_v3_restore).
-- ============================================================

UPDATE tipo_d
   SET depto_appmitrabajo = '03'
 WHERE tipo = '33'
   AND depto_appmitrabajo IS DISTINCT FROM '03';

-- Verificación:
-- SELECT tipo, descripcion, depto_appmitrabajo FROM tipo_d WHERE tipo='33';
