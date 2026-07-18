-- =============================================================================
-- 2026-07-16  Almacén: backfill de alm_articulo.tipo_articulo_id desde linea_id
-- =============================================================================
-- Contexto: el seed del mismo día (2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql)
-- dejó alm_tipo_articulo con los 9 grupos (mismos códigos 01..09 que alm_linea)
-- y todos los artículos con tipo_articulo_id = NULL.
-- Este backfill asigna a cada artículo el tipo cuyo código coincide con el de
-- su grupo (linea_id), dentro de la misma company.
--
--  * Solo toca filas con tipo_articulo_id IS NULL: no pisa asignaciones
--    manuales. Idempotente (re-ejecutarlo no cambia nada).
--  * Verificado 2026-07-16 en mirror y SRV: 631 artículos a actualizar en cada
--    base; 0 líneas sin tipo equivalente; quedan sin tipo solo los artículos
--    sin grupo (5 en mirror, 3 en SRV).
-- =============================================================================

BEGIN;

UPDATE alm_articulo a
SET    tipo_articulo_id    = t.id,
       usuariomodificacion = 'system',
       fechamodificacion   = (now() AT TIME ZONE 'utc')
FROM   alm_linea l
JOIN   alm_tipo_articulo t
       ON  t.company_id = l.company_id
       AND t.codigo     = l.codigo
WHERE  a.linea_id          = l.id
  AND  a.company_id        = l.company_id
  AND  a.tipo_articulo_id IS NULL;

COMMIT;
