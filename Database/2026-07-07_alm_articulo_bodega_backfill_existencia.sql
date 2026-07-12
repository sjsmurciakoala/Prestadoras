-- =============================================================================
-- Fase 2 — Backfill de existencia por bodega
-- Fecha: 2026-07-07
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Migra la existencia agregada actual (alm_articulo.existencia / existencia_minima)
-- a una fila alm_articulo_bodega en una bodega por defecto 'PRIN' (Bodega principal),
-- marcada como principal. A partir de aquí alm_articulo.existencia es un ROLLUP
-- (suma de las filas por bodega) que mantiene el servicio.
--
-- Seguro de correr: idempotente. Solo crea la fila PRIN para artículos que aún no
-- tienen ninguna ubicación; el rollup final garantiza consistencia sin cambiar
-- valores (una fila por artículo = mismo total).
-- =============================================================================
BEGIN;

-- 1. Bodega por defecto por empresa.
INSERT INTO alm_bodega (company_id, codigo, nombre, usuariocreacion)
SELECT DISTINCT a.company_id, 'PRIN', 'Bodega principal', 'backfill'
FROM alm_articulo a
ON CONFLICT (company_id, codigo) DO NOTHING;

-- 2. Una fila por artículo sin ubicación, con su existencia/mínimo actuales.
INSERT INTO alm_articulo_bodega (company_id, articulo_id, bodega_id, existencia, existencia_minima, principal, usuariocreacion)
SELECT a.company_id, a.id, b.id, a.existencia, a.existencia_minima, true, 'backfill'
FROM alm_articulo a
JOIN alm_bodega b ON b.company_id = a.company_id AND b.codigo = 'PRIN'
WHERE NOT EXISTS (SELECT 1 FROM alm_articulo_bodega ab WHERE ab.articulo_id = a.id)
ON CONFLICT (company_id, articulo_id, bodega_id) DO NOTHING;

-- 3. Rollup defensivo: alm_articulo.existencia/minima = suma de sus filas por bodega.
UPDATE alm_articulo a
SET existencia = s.total_existencia,
    existencia_minima = s.total_minima
FROM (
    SELECT articulo_id, SUM(existencia) AS total_existencia, SUM(existencia_minima) AS total_minima
    FROM alm_articulo_bodega
    GROUP BY articulo_id
) s
WHERE s.articulo_id = a.id;

COMMIT;
