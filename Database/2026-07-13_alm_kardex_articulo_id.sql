-- =============================================================================
-- Almacén (migración a id, módulo 1/4): kardex referencia al artículo por FK
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Primer módulo del plan docs/PLAN_migracion_articulo_id_almacen_2026-07-13.md.
-- Agrega alm_kardex.articulo_id (FK a alm_articulo) y enlaza los movimientos por
-- (company_id, codigo_articulo). La columna legacy codigo_articulo se conserva como
-- snapshot/referencia. No se borra ni modifica ningún dato existente.
--
-- IDEMPOTENTE: ADD/CREATE IF [NOT] EXISTS + UPDATE solo de filas con articulo_id nulo.
-- Volumen esperado (company 2): ~47k movimientos; el UPDATE usa índice por código.
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    ADD COLUMN IF NOT EXISTS articulo_id INTEGER NULL
        REFERENCES alm_articulo(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_alm_kardex_articulo ON alm_kardex(articulo_id);

-- Backfill: enlazar cada movimiento a su artículo por empresa + código.
UPDATE alm_kardex k
SET articulo_id = a.id
FROM alm_articulo a
WHERE a.company_id = k.company_id
  AND a.codigo_articulo = k.codigo_articulo
  AND k.articulo_id IS NULL
  AND k.codigo_articulo IS NOT NULL
  AND k.codigo_articulo <> '';

COMMIT;
