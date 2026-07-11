-- =============================================================================
-- Ubicación y existencia por bodega de cada artículo (alm_articulo_bodega)
-- Fecha: 2026-07-07
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Tabla puente artículo <-> bodega. En este paso se usa solo para la UBICACIÓN
-- física del artículo por bodega (bodega + estante + principal). Las columnas de
-- existencia (existencia / existencia_minima) se crean desde ya para no re-alterar
-- la tabla en la Fase 2 (control de existencia por bodega), pero por ahora quedan
-- en su default 0 y no las administra la UI.
--
-- Regla de una ubicación por (artículo, bodega): UNIQUE(company, articulo, bodega).
-- El estante es opcional y su estantería/bodega se validan en el servicio.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS alm_articulo_bodega (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    articulo_id         INTEGER        NOT NULL REFERENCES alm_articulo(id) ON DELETE CASCADE,
    bodega_id           INTEGER        NOT NULL REFERENCES alm_bodega(id)   ON DELETE RESTRICT,
    estante_id          INTEGER        NULL     REFERENCES alm_estante(id)  ON DELETE SET NULL,
    existencia          NUMERIC(15,2)  NOT NULL DEFAULT 0,
    existencia_minima   NUMERIC(11,2)  NOT NULL DEFAULT 0,
    principal           BOOLEAN        NOT NULL DEFAULT false,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_alm_articulo_bodega UNIQUE (company_id, articulo_id, bodega_id)
);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_bodega_company  ON alm_articulo_bodega(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_bodega_articulo ON alm_articulo_bodega(articulo_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_bodega_bodega   ON alm_articulo_bodega(bodega_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_bodega_estante  ON alm_articulo_bodega(estante_id);

-- A lo sumo una bodega principal por artículo.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_articulo_bodega_principal
    ON alm_articulo_bodega(company_id, articulo_id) WHERE principal;

COMMENT ON TABLE  alm_articulo_bodega IS 'Ubicación (y a futuro existencia) de un artículo por bodega. Una fila por (artículo, bodega).';
COMMENT ON COLUMN alm_articulo_bodega.estante_id IS 'Estante donde se ubica el artículo en esa bodega (opcional). Su estantería/bodega se validan en el servicio.';
COMMENT ON COLUMN alm_articulo_bodega.principal IS 'Marca la bodega principal del artículo (a lo sumo una por artículo).';
COMMENT ON COLUMN alm_articulo_bodega.existencia IS 'Existencia por bodega. Reservada para Fase 2 (control de stock por bodega); por ahora 0.';

COMMIT;
