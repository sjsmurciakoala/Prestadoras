-- =============================================================================
-- Compras: cuenta por pagar de la factura + sus abonos (alm_compra_cxp / _abono)
-- Fecha: 2026-08-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Fase 1 de "facturas al crédito → CxP". Toda factura (contado o crédito) genera su cuenta
-- por pagar; todas se pagan en la misma vista. La CxP es un LIBRO AUXILIAR independiente de
-- la contabilidad (el asiento de nacimiento es Fase 2, pendiente del contador — D-1).
-- Ver docs/centura-flujos/README_terminos_pago_factura_proveedor.md §9.
--
-- ADITIVO: dos tablas nuevas. No toca ninguna tabla existente. La CxP se genera HACIA
-- ADELANTE (al registrar cada factura nueva); el histórico no se backfillea aquí.
-- IDEMPOTENTE: CREATE ... IF NOT EXISTS.
-- Depende de: alm_compra_hdr (paso 25) con su clave alterna uq_alm_compra_hdr_tenant.
-- =============================================================================
BEGIN;

-- 1. Cuenta por pagar (1:1 con la factura) -----------------------------------
CREATE TABLE IF NOT EXISTS alm_compra_cxp (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    compra_hdr_id       INTEGER       NOT NULL,
    cod_proveedor       VARCHAR(20)   NOT NULL,
    proveedor           VARCHAR(100)  NULL,
    numero_factura_sar  VARCHAR(30)   NULL,
    fecha               DATE          NOT NULL,
    fecha_vencimiento   DATE          NOT NULL,
    condicion_pago      SMALLINT      NOT NULL DEFAULT 1,   -- 1 Contado, 2 Crédito, 3 Prepagado
    monto               NUMERIC(14,2) NOT NULL,
    saldo               NUMERIC(14,2) NOT NULL,             -- materializado; se mantiene con los abonos bajo lock
    estado_id           SMALLINT      NOT NULL DEFAULT 1,   -- 1 Pendiente, 2 Parcial, 3 Pagada, 9 Anulada
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_alm_compra_cxp_factura  UNIQUE (company_id, compra_hdr_id),
    CONSTRAINT uq_alm_compra_cxp_tenant   UNIQUE (company_id, id),
    CONSTRAINT ck_alm_compra_cxp_condicion CHECK (condicion_pago IN (1, 2, 3)),
    CONSTRAINT ck_alm_compra_cxp_estado    CHECK (estado_id IN (1, 2, 3, 9)),
    CONSTRAINT ck_alm_compra_cxp_saldo     CHECK (saldo >= 0),
    CONSTRAINT fk_alm_compra_cxp_hdr FOREIGN KEY (company_id, compra_hdr_id)
        REFERENCES alm_compra_hdr (company_id, id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_company     ON alm_compra_cxp(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_proveedor   ON alm_compra_cxp(company_id, cod_proveedor);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_vencimiento ON alm_compra_cxp(company_id, fecha_vencimiento);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_estado      ON alm_compra_cxp(company_id, estado_id);

COMMENT ON TABLE alm_compra_cxp IS 'Cuenta por pagar de una factura de compra (1:1 con alm_compra_hdr). Libro auxiliar de saldos por proveedor, independiente de la contabilidad.';
COMMENT ON COLUMN alm_compra_cxp.saldo IS 'Saldo pendiente = monto - suma de abonos vigentes. Materializado; se recalcula con cada abono bajo lock.';

-- 2. Abonos / pagos de la CxP ------------------------------------------------
CREATE TABLE IF NOT EXISTS alm_compra_cxp_abono (
    id                  BIGSERIAL     PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    cxp_id              INTEGER       NOT NULL,
    numero_abono        INTEGER       NOT NULL,             -- 1,2,3... por CxP
    fecha               DATE          NOT NULL,
    monto               NUMERIC(14,2) NOT NULL,
    metodo_pago         VARCHAR(20)   NULL,                 -- efectivo / cheque / transferencia
    banco_cuenta_id     INTEGER       NULL,
    num_cheque          VARCHAR(20)   NULL,
    ban_kardex_id       BIGINT        NULL,                 -- movimiento bancario generado
    ban_kardex_id_reverso BIGINT      NULL,                 -- movimiento bancario de la anulación
    partida_id          BIGINT        NULL,                 -- asiento del pago (cuando aplique)
    partida_reverso_id  BIGINT        NULL,
    estado              CHAR(1)       NOT NULL DEFAULT 'V',  -- V vigente / A anulado
    motivo_anulacion    VARCHAR(300)  NULL,
    observaciones       VARCHAR(300)  NULL,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuarioanulacion    VARCHAR(100)  NULL,
    fechaanulacion      TIMESTAMP     NULL,
    CONSTRAINT uq_alm_compra_cxp_abono_num UNIQUE (company_id, cxp_id, numero_abono),
    CONSTRAINT ck_alm_compra_cxp_abono_monto  CHECK (monto > 0),
    CONSTRAINT ck_alm_compra_cxp_abono_estado CHECK (estado IN ('V', 'A')),
    CONSTRAINT fk_alm_compra_cxp_abono_cxp FOREIGN KEY (company_id, cxp_id)
        REFERENCES alm_compra_cxp (company_id, id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_abono_company ON alm_compra_cxp_abono(company_id);
CREATE INDEX IF NOT EXISTS ix_alm_compra_cxp_abono_cxp     ON alm_compra_cxp_abono(company_id, cxp_id);

COMMENT ON TABLE alm_compra_cxp_abono IS 'Abonos/pagos de una cuenta por pagar de compra. Estado V vigente / A anulado (patrón prv_compromiso_abono).';

-- Idempotencia para bases donde la tabla ya se creó sin esta columna.
ALTER TABLE alm_compra_cxp_abono ADD COLUMN IF NOT EXISTS ban_kardex_id_reverso BIGINT NULL;

COMMIT;
