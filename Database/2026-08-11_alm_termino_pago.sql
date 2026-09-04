-- =============================================================================
-- Almacén / Compras: catálogo de términos de pago del proveedor (alm_termino_pago)
-- Fecha: 2026-08-11
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Reemplaza el texto libre de "Términos de pago" en la factura de proveedor
-- (alm_compra_hdr.terminos_pago) por un catálogo controlado con DÍAS de crédito, para
-- poder autocalcular la fecha de vencimiento (paridad con lo que ventas ya hace vía
-- CLN_TERMINOS_PAGO en Centura). Ver docs/centura-flujos/README_terminos_pago_factura_proveedor.md
--
-- ADITIVO / bajo riesgo: CREATE TABLE nueva + ADD COLUMN nullable con FK. No borra ni
-- reescribe datos: las facturas ya capturadas quedan con termino_pago_id NULL y conservan
-- su texto en terminos_pago. SIN seed: el catálogo arranca vacío y lo llena el usuario.
--
-- IDEMPOTENTE: CREATE/ADD IF [NOT] EXISTS.
-- =============================================================================
BEGIN;

-- 1. Catálogo de términos de pago -------------------------------------------
CREATE TABLE IF NOT EXISTS alm_termino_pago (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nombre              VARCHAR(60)   NOT NULL,
    dias                INTEGER       NOT NULL DEFAULT 0,
    es_default          BOOLEAN       NOT NULL DEFAULT false,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_termino_pago_company_nombre UNIQUE (company_id, nombre),
    CONSTRAINT ck_alm_termino_pago_dias CHECK (dias >= 0)
);
CREATE INDEX IF NOT EXISTS ix_alm_termino_pago_company ON alm_termino_pago(company_id);

-- A lo sumo un término predeterminado por empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_termino_pago_default
    ON alm_termino_pago(company_id) WHERE es_default;

COMMENT ON TABLE alm_termino_pago IS 'Catálogo de términos de pago del proveedor: nombre + días de crédito, por empresa. Base para autocalcular el vencimiento de la factura de compra.';
COMMENT ON COLUMN alm_termino_pago.dias IS 'Días de crédito. 0 = contado. Se suma a la fecha de la factura para el vencimiento.';

-- 2. FK termino_pago_id en la cabecera de la factura de compra ----------------
ALTER TABLE alm_compra_hdr
    ADD COLUMN IF NOT EXISTS termino_pago_id INTEGER NULL
        REFERENCES alm_termino_pago(id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_alm_compra_hdr_termino_pago ON alm_compra_hdr(termino_pago_id);

COMMENT ON COLUMN alm_compra_hdr.termino_pago_id IS 'Término de pago elegido del catálogo (alm_termino_pago). El nombre queda como snapshot en terminos_pago y los días en plazo_dias.';

COMMIT;
