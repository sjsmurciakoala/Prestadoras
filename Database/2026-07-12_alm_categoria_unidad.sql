-- =============================================================================
-- Almacén: catálogo de categorías de unidades de medida (alm_categoria_unidad)
-- Fecha: 2026-07-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Reemplaza el campo de texto libre alm_unidad_medida.categoria por una FK a un
-- catálogo controlado (alm_categoria_unidad). Migra las categorías actuales y enlaza
-- cada unidad por su nombre. NO elimina la columna de texto 'categoria' (queda como
-- respaldo, sin uso en el código); se puede eliminar más adelante.
--
-- IDEMPOTENTE: CREATE/ADD IF [NOT] EXISTS + ON CONFLICT DO NOTHING + UPDATE por FK nula.
-- Empresa objetivo del seed/backfill: las unidades ya existentes por company_id.
-- =============================================================================
BEGIN;

-- 1. Tabla catálogo de categorías --------------------------------------------
CREATE TABLE IF NOT EXISTS alm_categoria_unidad (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nombre              VARCHAR(30)   NOT NULL,
    descripcion         VARCHAR(100)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_categoria_unidad_company_nombre UNIQUE (company_id, nombre)
);
CREATE INDEX IF NOT EXISTS ix_alm_categoria_unidad_company ON alm_categoria_unidad(company_id);
COMMENT ON TABLE alm_categoria_unidad IS 'Catálogo de categorías (tipos) de unidad de medida: Peso, Volumen, Longitud, Cantidad, etc.';

-- 2. FK categoria_id en alm_unidad_medida ------------------------------------
ALTER TABLE alm_unidad_medida
    ADD COLUMN IF NOT EXISTS categoria_id INTEGER NULL
        REFERENCES alm_categoria_unidad(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_alm_unidad_medida_categoria ON alm_unidad_medida(categoria_id);

-- 3. Seed: crear las categorías actuales (del texto libre) por empresa --------
INSERT INTO alm_categoria_unidad (company_id, nombre, activo, usuariocreacion, fechacreacion)
SELECT DISTINCT u.company_id, u.categoria, true, 'seed_categoria', (now() AT TIME ZONE 'utc')
FROM alm_unidad_medida u
WHERE u.categoria IS NOT NULL AND btrim(u.categoria) <> ''
ON CONFLICT (company_id, nombre) DO NOTHING;

-- 4. Backfill: enlazar cada unidad a su categoría por nombre ------------------
UPDATE alm_unidad_medida u
SET categoria_id = c.id
FROM alm_categoria_unidad c
WHERE c.company_id = u.company_id
  AND lower(c.nombre) = lower(u.categoria)
  AND u.categoria IS NOT NULL
  AND u.categoria_id IS NULL;

COMMIT;
