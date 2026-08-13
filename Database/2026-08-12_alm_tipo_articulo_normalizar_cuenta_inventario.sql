-- =============================================================================
-- Almacén / Contabilidad: normaliza el formato de alm_tipo_articulo.cuenta_inventario
-- Fecha: 2026-08-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor (siad_v3).
--
-- Prerrequisito de la Fase 2 de compras (integración contable, con_integracion_config.activo_compras).
-- El asiento de la factura resuelve la cuenta de inventario con igualdad EXACTA contra
-- con_plan_cuentas.code. Los tipos migrados guardan el código CON guiones (p.ej. 114-01-01-02-01)
-- mientras el plan lo tiene SIN guiones (11401010201): es el MISMO código, distinto formato, y por
-- eso el binario no lo resuelve y el alta de la factura fallaría con el flag encendido.
--
-- Este script quita los guiones SOLO cuando el código normalizado YA existe y es posteable en el
-- plan de la MISMA empresa (guardia por EXISTS). El tipo cuya cuenta ya está sin guiones no se toca.
--
-- CORRECTIVO de datos · NO destructivo · REVERSIBLE · IDEMPOTENTE. No crea ni borra estructura.
-- No usa company_id fijo → multiempresa (aplica donde exista el desajuste).
-- En el mirror afecta 8 filas (empresa 2, tipos 02–09); el tipo 01 ya estaba bien.
--
-- Mapeo verificado en el mirror (empresa 2) — sirve de referencia y rollback manual:
--   tipo 02  114-01-01-02-01 -> 11401010201   Inv. Tuberia y accesorios Alc. Sanitario
--   tipo 03  114-01-01-03-01 -> 11401010301   Materiales y Utiles de oficina
--   tipo 04  114-09-02-01-01 -> 11409020101   Herramientas menores y otras
--   tipo 05  114-01-01-04-01 -> 11401010401   Materiales en Consignacion
--   tipo 06  114-09-01-01-01 -> 11409010101   Inventario de materiales electricos
--   tipo 07  114-01-02-01-01 -> 11401020101   Inv. Producto quimico
--   tipo 08  114-09-03-01-01 -> 11409030101   Inv. por materiales municipales
--   tipo 09  114-01-01-01-02 -> 11401010102   Inv. de tuberia de polietileno
--
-- RESPALDO recomendado ANTES de aplicar (guardar esta salida por si se necesita revertir):
--   SELECT company_id, id, codigo, nombre, cuenta_inventario
--     FROM alm_tipo_articulo
--    WHERE cuenta_inventario ~ '\D';
-- =============================================================================
BEGIN;

UPDATE alm_tipo_articulo t
   SET cuenta_inventario = regexp_replace(t.cuenta_inventario, '\D', '', 'g')
 WHERE t.cuenta_inventario ~ '\D'
   AND EXISTS (
        SELECT 1
          FROM con_plan_cuentas c
         WHERE c.company_id = t.company_id
           AND c.code = regexp_replace(t.cuenta_inventario, '\D', '', 'g')
           AND c.allows_posting);

COMMIT;

-- =============================================================================
-- Verificación (¿ya aplicado?): debe devolver 0 filas.
--   Lista los tipos cuya cuenta de inventario NO resuelve a una cuenta posteable.
-- =============================================================================
-- SELECT t.company_id, t.codigo, t.nombre, t.cuenta_inventario
--   FROM alm_tipo_articulo t
--  WHERE t.cuenta_inventario IS NOT NULL AND btrim(t.cuenta_inventario) <> ''
--    AND NOT EXISTS (SELECT 1 FROM con_plan_cuentas c
--                      WHERE c.company_id = t.company_id
--                        AND btrim(c.code) = btrim(t.cuenta_inventario)
--                        AND c.allows_posting);

-- =============================================================================
-- ROLLBACK manual (solo si hiciera falta) — restaura el formato con guiones (empresa 2):
-- =============================================================================
-- BEGIN;
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-01-01-02-01' WHERE company_id = 2 AND codigo = '02';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-01-01-03-01' WHERE company_id = 2 AND codigo = '03';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-09-02-01-01' WHERE company_id = 2 AND codigo = '04';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-01-01-04-01' WHERE company_id = 2 AND codigo = '05';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-09-01-01-01' WHERE company_id = 2 AND codigo = '06';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-01-02-01-01' WHERE company_id = 2 AND codigo = '07';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-09-03-01-01' WHERE company_id = 2 AND codigo = '08';
-- UPDATE alm_tipo_articulo SET cuenta_inventario = '114-01-01-01-02' WHERE company_id = 2 AND codigo = '09';
-- COMMIT;
