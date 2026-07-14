-- =============================================================================
-- Almacén (migración a id, módulos 2-4/4): compras, requisiciones y descargos
-- referencian al artículo por FK (articulo_id)
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Completa docs/PLAN_migracion_articulo_id_almacen_2026-07-13.md (el kardex ya se
-- migró en 2026-07-13_alm_kardex_articulo_id.sql). Agrega articulo_id (FK a
-- alm_articulo) a alm_compra, alm_requisicion y alm_descargo, y enlaza cada fila por
-- (company_id, codigo_articulo). La columna legacy codigo_articulo se conserva como
-- snapshot/referencia. No se borra ni modifica ningún dato existente.
--
-- IDEMPOTENTE: ADD/CREATE IF [NOT] EXISTS + UPDATE solo de filas con articulo_id nulo.
-- El UPDATE usa los índices por codigo_articulo existentes en cada tabla.
-- =============================================================================
BEGIN;

-- ── Compras ─────────────────────────────────────────────────────────────────
ALTER TABLE alm_compra
    ADD COLUMN IF NOT EXISTS articulo_id INTEGER NULL
        REFERENCES alm_articulo(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_alm_compra_articulo ON alm_compra(articulo_id);
UPDATE alm_compra x
SET articulo_id = a.id
FROM alm_articulo a
WHERE a.company_id = x.company_id
  AND a.codigo_articulo = x.codigo_articulo
  AND x.articulo_id IS NULL
  AND x.codigo_articulo IS NOT NULL
  AND x.codigo_articulo <> '';

-- ── Requisiciones ───────────────────────────────────────────────────────────
ALTER TABLE alm_requisicion
    ADD COLUMN IF NOT EXISTS articulo_id INTEGER NULL
        REFERENCES alm_articulo(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_articulo ON alm_requisicion(articulo_id);
UPDATE alm_requisicion x
SET articulo_id = a.id
FROM alm_articulo a
WHERE a.company_id = x.company_id
  AND a.codigo_articulo = x.codigo_articulo
  AND x.articulo_id IS NULL
  AND x.codigo_articulo IS NOT NULL
  AND x.codigo_articulo <> '';

-- ── Descargos ───────────────────────────────────────────────────────────────
ALTER TABLE alm_descargo
    ADD COLUMN IF NOT EXISTS articulo_id INTEGER NULL
        REFERENCES alm_articulo(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_alm_descargo_articulo ON alm_descargo(articulo_id);
UPDATE alm_descargo x
SET articulo_id = a.id
FROM alm_articulo a
WHERE a.company_id = x.company_id
  AND a.codigo_articulo = x.codigo_articulo
  AND x.articulo_id IS NULL
  AND x.codigo_articulo IS NOT NULL
  AND x.codigo_articulo <> '';

COMMIT;
