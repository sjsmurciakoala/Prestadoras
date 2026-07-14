-- =============================================================================
-- Relación proveedor <-> artículo ("UPC") — alm_articulo_proveedor
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Tabla puente artículo <-> proveedor: qué proveedores suministran un artículo.
-- Cada fila guarda el proveedor (cod_proveedor), el código/UPC con que ese
-- proveedor identifica el artículo, el costo de compra y si es el proveedor
-- principal (preferido). Una fila por (artículo, proveedor).
--
-- El proveedor NO lleva FK en BD porque prv_proveedores es keyless y multiempresa
-- (cod_proveedor es único por empresa, no global); su existencia se valida en el
-- servicio. Las relaciones no se eliminan: se DESHABILITAN (activo=false) para
-- conservar el histórico.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS alm_articulo_proveedor (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    articulo_id         INTEGER        NOT NULL REFERENCES alm_articulo(id) ON DELETE CASCADE,
    cod_proveedor       VARCHAR(20)    NOT NULL,
    codigo_upc          VARCHAR(40)    NULL,
    costo               NUMERIC(12,4)  NOT NULL DEFAULT 0,
    principal           BOOLEAN        NOT NULL DEFAULT false,
    activo              BOOLEAN        NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_alm_articulo_proveedor UNIQUE (company_id, articulo_id, cod_proveedor)
);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_proveedor_company   ON alm_articulo_proveedor(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_proveedor_articulo  ON alm_articulo_proveedor(articulo_id);
CREATE INDEX IF NOT EXISTS ix_alm_articulo_proveedor_proveedor ON alm_articulo_proveedor(cod_proveedor);

-- A lo sumo un proveedor principal por artículo.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_articulo_proveedor_principal
    ON alm_articulo_proveedor(company_id, articulo_id) WHERE principal;

COMMENT ON TABLE  alm_articulo_proveedor IS 'Relación proveedor <-> artículo (UPC): proveedores que suministran un artículo. Una fila por (artículo, proveedor).';
COMMENT ON COLUMN alm_articulo_proveedor.cod_proveedor IS 'Código del proveedor (prv_proveedores.cod_proveedor). Sin FK: se valida en el servicio (keyless + multiempresa).';
COMMENT ON COLUMN alm_articulo_proveedor.codigo_upc IS 'Código/UPC con que el proveedor identifica este artículo (opcional).';
COMMENT ON COLUMN alm_articulo_proveedor.costo IS 'Costo de compra del artículo con ese proveedor.';
COMMENT ON COLUMN alm_articulo_proveedor.principal IS 'Marca el proveedor principal/preferido del artículo (a lo sumo uno por artículo).';

COMMIT;
