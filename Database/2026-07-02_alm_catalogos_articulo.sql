-- =============================================================================
-- Catálogos de clasificación de artículo de almacén
-- Fecha: 2026-07-02
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- 1. alm_tipo_articulo — clasificación por USO (operativo/mantenimiento/consumo).
--    NO existía en el legacy MySQL (se confirmó por investigación); es nueva.
-- 2. alm_linea  — rubro de inventario (migra de MySQL bdsimafi.lineas, 9 filas).
-- 3. alm_grupo  — grupo de producto dentro de la línea (migra de grupoinv, 164).
--
-- Se agregan FKs opcionales en alm_articulo: tipo_articulo_id, linea_id, grupo_id.
-- linea_id/grupo_id se rellenan (backfill) por el loader tras cargar los catálogos,
-- emparejando los códigos legacy ya migrados (alm_articulo.linea / .grupo).
-- =============================================================================

BEGIN;

-- ── 1. alm_tipo_articulo (clasificación por uso) ─────────────────────────────
CREATE TABLE IF NOT EXISTS alm_tipo_articulo (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(10)   NOT NULL,
    nombre              VARCHAR(60)   NOT NULL,
    descripcion         VARCHAR(200)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_tipo_articulo_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_tipo_articulo_company ON alm_tipo_articulo(company_id);
COMMENT ON TABLE alm_tipo_articulo IS 'Clasificación de artículos por uso (operativo, mantenimiento, consumo). Nueva; no existía en el legacy.';

INSERT INTO alm_tipo_articulo (company_id, codigo, nombre, descripcion, usuariocreacion)
VALUES
    (2, 'OPER', 'Operativo',     'Artículos para la operación del servicio.', 'system'),
    (2, 'MANT', 'Mantenimiento', 'Artículos para mantenimiento de infraestructura y equipo.', 'system'),
    (2, 'CONS', 'Consumo',       'Artículos de consumo interno / suministros.', 'system')
ON CONFLICT (company_id, codigo) DO NOTHING;

-- ── 2. alm_linea (rubro de inventario) ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_linea (
    id                       SERIAL        PRIMARY KEY,
    company_id               BIGINT        NOT NULL,
    codigo                   VARCHAR(2)    NOT NULL,
    nombre                   VARCHAR(100)  NOT NULL,
    cuenta_contable          VARCHAR(25)   NULL,
    cuenta_contable_anterior VARCHAR(30)   NULL,
    activo                   BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion          VARCHAR(100)  NULL,
    fechacreacion            TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion      VARCHAR(100)  NULL,
    fechamodificacion        TIMESTAMP     NULL,
    CONSTRAINT uq_alm_linea_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_linea_company ON alm_linea(company_id);
COMMENT ON TABLE alm_linea IS 'Rubro/línea de inventario de almacén. Migrado de MySQL bdsimafi.lineas.';

-- ── 3. alm_grupo (grupo de producto dentro de la línea) ──────────────────────
CREATE TABLE IF NOT EXISTS alm_grupo (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(6)    NOT NULL,
    nombre              VARCHAR(100)  NOT NULL,
    linea_codigo        VARCHAR(2)    NULL,
    linea_id            INTEGER       NULL REFERENCES alm_linea(id) ON DELETE SET NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_grupo_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_grupo_company ON alm_grupo(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_grupo_linea ON alm_grupo(linea_id);
COMMENT ON TABLE alm_grupo IS 'Grupo de producto de almacén (pertenece a una línea). Migrado de MySQL bdsimafi.grupoinv.';

-- ── 4. FKs opcionales en alm_articulo ────────────────────────────────────────
ALTER TABLE alm_articulo
    ADD COLUMN IF NOT EXISTS tipo_articulo_id INTEGER NULL REFERENCES alm_tipo_articulo(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS linea_id         INTEGER NULL REFERENCES alm_linea(id)         ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS grupo_id         INTEGER NULL REFERENCES alm_grupo(id)         ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_alm_articulo_tipo_articulo ON alm_articulo(tipo_articulo_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_linea_id      ON alm_articulo(linea_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_grupo_id      ON alm_articulo(grupo_id);

COMMENT ON COLUMN alm_articulo.tipo_articulo_id IS 'FK opcional a alm_tipo_articulo (clasificación por uso).';
COMMENT ON COLUMN alm_articulo.linea_id IS 'FK opcional a alm_linea (convive con el código legacy linea).';
COMMENT ON COLUMN alm_articulo.grupo_id IS 'FK opcional a alm_grupo (convive con el código legacy grupo).';

COMMIT;
