-- =============================================================================
-- alm_kardex gana bodega_id (FK a alm_bodega) + backfill de códigos legacy.
-- Fase 3 · Ubicación y existencia por bodega.
-- Fecha: 2026-07-09
-- Regla DB Mirror: aplicar también en siad_v3 (SRV) y siad_v3_restore (localhost).
--
-- Contexto: alm_kardex es histórico migrado (bdsimafi.inventariotra), de sólo
-- lectura en la app. La columna legacy `bodega` es VARCHAR(2) de texto libre
-- (valores observados: '01', '11', ''). Se normaliza a una FK real hacia el
-- catálogo alm_bodega para poder consultar el kardex por bodega. La columna
-- legacy `bodega` se conserva intacta.
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    ADD COLUMN IF NOT EXISTS bodega_id INTEGER NULL REFERENCES alm_bodega(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS ix_alm_kardex_bodega ON alm_kardex(bodega_id);

-- Backfill 1: crear una bodega por cada código de texto legacy no vacío y mapear.
INSERT INTO alm_bodega (company_id, codigo, nombre, usuariocreacion)
SELECT DISTINCT k.company_id, btrim(k.bodega), CONCAT('Bodega ', btrim(k.bodega)), 'backfill'
FROM alm_kardex k
WHERE k.bodega IS NOT NULL AND btrim(k.bodega) <> ''
ON CONFLICT (company_id, codigo) DO NOTHING;

UPDATE alm_kardex k
SET bodega_id = b.id
FROM alm_bodega b
WHERE b.company_id = k.company_id AND b.codigo = btrim(k.bodega) AND k.bodega_id IS NULL;

-- Backfill 2: movimientos sin bodega legacy (NULL o '') → bodega principal (PRIN)
-- de la empresa (sembrada por el backfill de la Fase 2).
UPDATE alm_kardex k
SET bodega_id = b.id
FROM alm_bodega b
WHERE b.company_id = k.company_id AND b.codigo = 'PRIN' AND k.bodega_id IS NULL;

COMMIT;
