-- =============================================================================
-- Saneo Fase 2 (unificación línea→tipo): artículos sin tipo_articulo_id
-- Fecha: 2026-07-16
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y SRV
-- (172.16.0.9/siad_v3). Idempotente y re-ejecutable; matchea por descripción
-- + tipo NULL (no por id), así funciona igual en ambas bases.
--
-- Contexto: el seed 2026-07-16 reemplazó los tipos genéricos (OPER/MANT/CONS/
-- SERV) por los 9 grupos reales; los artículos que apuntaban a los genéricos
-- quedaron con tipo_articulo_id NULL (ON DELETE SET NULL). Decisión del
-- 2026-07-16:
--   1) La válvula de prueba (tiene kardex, no se puede borrar) se clasifica
--      en el tipo 01 de su empresa.
--   2) El artículo de prueba "MANTENIMIENTO PREVENTIVO DE BOMBAS - CONTRATADO"
--      (sin ningún movimiento) se elimina, con guardas NOT EXISTS.
--   3) Los artículos legacy sin tipo (DISPONIBLE, 0032, 0037) NO se tocan:
--      la app exigirá elegirles tipo cuando alguien los edite.
-- =============================================================================

BEGIN;

-- 1) Válvula de prueba → tipo 01 (INV. DE MATERIALES Y ACCESORIOS AGUA POTABLE)
UPDATE alm_articulo a
SET    tipo_articulo_id    = t.id,
       usuariomodificacion = 'saneo_fase2_unificacion',
       fechamodificacion   = (NOW() AT TIME ZONE 'UTC')::timestamp
FROM   alm_tipo_articulo t
WHERE  a.tipo_articulo_id IS NULL
  AND  a.descripcion = 'VALVULA COMPUERTA 4in HIERRO DUCTIL - PRUEBA MOTOR'
  AND  t.company_id = a.company_id
  AND  t.codigo = '01';

-- 2) Artículo de prueba de servicios, SOLO si no tiene ningún rastro.
--    (kardex además está protegido por FK RESTRICT; las guardas cubren también
--    los documentos con FK SET NULL para no dejar huérfanos.)
WITH candidato AS (
    SELECT a.id
    FROM   alm_articulo a
    WHERE  a.tipo_articulo_id IS NULL
      AND  a.descripcion = 'MANTENIMIENTO PREVENTIVO DE BOMBAS - CONTRATADO'
      AND  NOT EXISTS (SELECT 1 FROM alm_kardex      k WHERE k.articulo_id = a.id)
      AND  NOT EXISTS (SELECT 1 FROM alm_compra      c WHERE c.articulo_id = a.id)
      AND  NOT EXISTS (SELECT 1 FROM alm_requisicion r WHERE r.articulo_id = a.id)
      AND  NOT EXISTS (SELECT 1 FROM alm_descargo    d WHERE d.articulo_id = a.id)
),
del_bodegas AS (
    DELETE FROM alm_articulo_bodega b
    WHERE b.articulo_id IN (SELECT id FROM candidato)
    RETURNING 1
),
del_proveedores AS (
    DELETE FROM alm_articulo_proveedor p
    WHERE p.articulo_id IN (SELECT id FROM candidato)
    RETURNING 1
)
DELETE FROM alm_articulo a
WHERE a.id IN (SELECT id FROM candidato);

-- Verificación: deben quedar solo los legacy sin tipo (DISPONIBLE, 0032, 0037).
SELECT id, company_id, codigo_articulo, descripcion
FROM   alm_articulo
WHERE  tipo_articulo_id IS NULL
ORDER  BY company_id, id;

COMMIT;
