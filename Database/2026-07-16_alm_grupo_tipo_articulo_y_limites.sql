-- =============================================================================
-- 2026-07-16  Almacén — Fase 1 de la unificación línea→tipo (plan en
--             docs/plans/2026-07-16-unificacion-linea-tipo-articulo-plan.md)
-- =============================================================================
-- Todo aditivo / widening; el sistema sigue funcionando igual tras aplicarlo.
--
--  1. alm_tipo_articulo: nombre 60→100 (el tipo 02 quedó truncado en el seed;
--     aquí se re-sincroniza el nombre íntegro desde alm_linea) y las 5 cuentas
--     contables 20→25 (paridad con alm_linea.cuenta_contable varchar(25)).
--  2. alm_grupo: nueva columna tipo_articulo_id (FK a alm_tipo_articulo,
--     ON DELETE SET NULL) + índice + backfill emparejando el código del grupo
--     (linea) con el código del tipo, por company. linea_id NO se toca aún
--     (se elimina en la Fase 3, junto con alm_linea).
--
-- Verificado 2026-07-16 en mirror y SRV: 164/164 categorías mapean 1:1 a un
-- tipo; longitud máxima de cuentas hoy = 15. Idempotente (IF NOT EXISTS /
-- WHERE tipo_articulo_id IS NULL).
-- =============================================================================

BEGIN;

-- ── 1. Ampliar límites del tipo (widening, sin reescritura de tabla) ─────────
ALTER TABLE alm_tipo_articulo
    ALTER COLUMN nombre              TYPE VARCHAR(100),
    ALTER COLUMN cuenta_inventario   TYPE VARCHAR(25),
    ALTER COLUMN cuenta_costo_ventas TYPE VARCHAR(25),
    ALTER COLUMN cuenta_ventas       TYPE VARCHAR(25),
    ALTER COLUMN cuenta_ajustes      TYPE VARCHAR(25),
    ALTER COLUMN cuenta_devoluciones TYPE VARCHAR(25);

-- Re-sincronizar nombres desde alm_linea (repara el truncado del tipo 02;
-- normaliza espacios múltiples como hizo el seed).
UPDATE alm_tipo_articulo t
SET    nombre = left(regexp_replace(l.nombre, '\s+', ' ', 'g'), 100)
FROM   alm_linea l
WHERE  l.company_id = t.company_id
  AND  l.codigo     = t.codigo
  AND  t.nombre    <> left(regexp_replace(l.nombre, '\s+', ' ', 'g'), 100);

-- ── 2. Colgar las categorías (alm_grupo) del tipo ────────────────────────────
ALTER TABLE alm_grupo
    ADD COLUMN IF NOT EXISTS tipo_articulo_id INTEGER NULL
        REFERENCES alm_tipo_articulo(id) ON DELETE SET NULL;

COMMENT ON COLUMN alm_grupo.tipo_articulo_id IS
    'Tipo de artículo al que pertenece la categoría (sustituye a linea_id; unificación 2026-07-16).';

CREATE INDEX IF NOT EXISTS ix_alm_grupo_tipo_articulo
    ON alm_grupo (tipo_articulo_id);

UPDATE alm_grupo g
SET    tipo_articulo_id = t.id
FROM   alm_linea l
JOIN   alm_tipo_articulo t
       ON  t.company_id = l.company_id
       AND t.codigo     = l.codigo
WHERE  g.linea_id          = l.id
  AND  g.company_id        = l.company_id
  AND  g.tipo_articulo_id IS NULL;

COMMIT;
