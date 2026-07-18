-- =============================================================================
-- 2026-07-16  Almacén: sembrar alm_tipo_articulo desde los grupos (alm_linea)
-- =============================================================================
-- Pedido: el contenido del combo "Grupo" del artículo (alm_linea)
-- debe verse también en el catálogo "Tipos de artículos" (alm_tipo_articulo).
-- Decisión 2026-07-16: se vacía el catálogo de tipos (los 4 genéricos
-- Operativo / Mantenimiento / Consumo / Servicios no importan) y se copian
-- los 9 grupos. Solo datos: la UI y el combo "Grupo" no cambian.
--
-- Efectos:
--  * DELETE alm_tipo_articulo — 4 filas. La FK alm_articulo.tipo_articulo_id
--    es ON DELETE SET NULL: 4 artículos quedan sin tipo (ids 1, 776, 777, 778;
--    el 778 era tipo Servicios y pierde la marca maneja_inventario = false).
--  * INSERT desde alm_linea, por company_id:
--      codigo            <- codigo del grupo (01..09)
--      nombre            <- nombre del grupo (espacios normalizados, máx. 60;
--                           el grupo 02 mide 62 y queda truncado — el nombre
--                           íntegro se conserva en descripcion)
--      descripcion       <- 'Grupo <codigo> - <nombre original>'
--      cuenta_inventario <- cuenta_contable del grupo (114-xx)
--      maneja_inventario <- true (todos son inventarios físicos)
--      activo            <- activo del grupo
--    Idempotente: ON CONFLICT (company_id, codigo) DO NOTHING.
-- =============================================================================

BEGIN;

DELETE FROM alm_tipo_articulo;

INSERT INTO alm_tipo_articulo
    (company_id, codigo, nombre, descripcion, activo, maneja_inventario,
     cuenta_inventario, usuariocreacion)
SELECT l.company_id,
       l.codigo,
       left(regexp_replace(l.nombre, '\s+', ' ', 'g'), 60),
       left('Grupo ' || l.codigo || ' - ' || l.nombre, 200),
       l.activo,
       true,
       l.cuenta_contable,
       'system'
FROM alm_linea l
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;
