-- Encabezado del lote de cartas de cobro masivas
CREATE TABLE IF NOT EXISTS cln_carta_cobro_hdr (
    id                SERIAL PRIMARY KEY,
    company_id        BIGINT NOT NULL,
    correlativo       VARCHAR(20) NOT NULL,
    fecha_generacion  DATE NOT NULL DEFAULT CURRENT_DATE,
    total_clientes    INT NOT NULL DEFAULT 0,
    criterio          VARCHAR(60),
    usuario           VARCHAR(100),
    usuariocreacion   VARCHAR(100),
    fechacreacion     TIMESTAMP DEFAULT NOW(),
    CONSTRAINT uq_carta_cobro_hdr_correlativo UNIQUE (company_id, correlativo)
);

CREATE INDEX IF NOT EXISTS ix_cln_carta_cobro_hdr_company
    ON cln_carta_cobro_hdr(company_id);

-- Detalle: un cliente por fila
CREATE TABLE IF NOT EXISTS cln_carta_cobro_dtl (
    id              SERIAL PRIMARY KEY,
    hdr_id          INT NOT NULL REFERENCES cln_carta_cobro_hdr(id),
    company_id      BIGINT NOT NULL,
    cliente_clave   VARCHAR(20) NOT NULL,
    nombre_cliente  VARCHAR(250),
    saldo           DECIMAL(18,2),
    dias_mora       INT
);

CREATE INDEX IF NOT EXISTS ix_cln_carta_cobro_dtl_hdr
    ON cln_carta_cobro_dtl(hdr_id);
CREATE INDEX IF NOT EXISTS ix_cln_carta_cobro_dtl_cliente
    ON cln_carta_cobro_dtl(company_id, cliente_clave);
