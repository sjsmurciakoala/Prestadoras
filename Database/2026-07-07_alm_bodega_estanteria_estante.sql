-- =============================================================================
-- Catálogo de ubicaciones de almacén: bodega -> estantería -> estante
-- Fecha: 2026-07-07
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Catálogo formal de 3 niveles para ubicar físicamente los artículos:
--   alm_bodega     (nivel 1)
--   alm_estanteria (nivel 2, pertenece a una bodega)
--   alm_estante    (nivel 3, ubicación direccionable dentro de una estantería)
-- Fase 1 del feature "ubicación + existencia por bodega". No toca existencias
-- ni kardex (eso es Fase 2 y 3).
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS alm_bodega (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(10)   NOT NULL,
    nombre              VARCHAR(100)  NOT NULL,
    direccion           VARCHAR(200)  NULL,
    responsable         VARCHAR(100)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_bodega_company_codigo UNIQUE (company_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_bodega_company ON alm_bodega(company_id);
COMMENT ON TABLE alm_bodega IS 'Catálogo de bodegas de almacén (nivel 1 de ubicación).';

CREATE TABLE IF NOT EXISTS alm_estanteria (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    bodega_id           INTEGER       NOT NULL REFERENCES alm_bodega(id) ON DELETE CASCADE,
    codigo              VARCHAR(10)   NOT NULL,
    nombre              VARCHAR(100)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_estanteria_company_bodega_codigo UNIQUE (company_id, bodega_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_estanteria_company ON alm_estanteria(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_estanteria_bodega ON alm_estanteria(bodega_id);
COMMENT ON TABLE alm_estanteria IS 'Estanterías dentro de una bodega (nivel 2 de ubicación).';

CREATE TABLE IF NOT EXISTS alm_estante (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    estanteria_id       INTEGER       NOT NULL REFERENCES alm_estanteria(id) ON DELETE CASCADE,
    codigo              VARCHAR(10)   NOT NULL,
    descripcion         VARCHAR(150)  NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_estante_company_estanteria_codigo UNIQUE (company_id, estanteria_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_alm_estante_company ON alm_estante(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_estante_estanteria ON alm_estante(estanteria_id);
COMMENT ON TABLE alm_estante IS 'Estantes dentro de una estantería (nivel 3, ubicación direccionable).';

COMMIT;
