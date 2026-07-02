CREATE TABLE IF NOT EXISTS cln_nota_cobro (
    id              SERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL,
    correlativo     VARCHAR(20) NOT NULL,
    codigocliente   VARCHAR(20) NOT NULL,
    fecha           DATE NOT NULL,
    monto           DECIMAL(18,2) NOT NULL,
    descripcion     TEXT,
    estado          VARCHAR(20) NOT NULL DEFAULT 'EMITIDA',
    usuario         VARCHAR(100),
    usuariocreacion VARCHAR(100),
    fechacreacion   TIMESTAMP DEFAULT NOW(),
    CONSTRAINT uq_nota_cobro_correlativo UNIQUE (company_id, correlativo)
);

CREATE INDEX IF NOT EXISTS ix_cln_nota_cobro_company_cliente
    ON cln_nota_cobro(company_id, codigocliente);
