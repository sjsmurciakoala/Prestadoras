-- Tabla para registro de llamadas telefónicas de cobranza
CREATE TABLE IF NOT EXISTS cln_llamada_cobranza (
    id              SERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL,
    codigocliente   VARCHAR(20) NOT NULL,
    fecha           TIMESTAMP NOT NULL DEFAULT NOW(),
    numero_llamado  VARCHAR(30),
    resultado       VARCHAR(50) NOT NULL,
    observacion     TEXT,
    usuario         VARCHAR(100),
    usuariocreacion VARCHAR(100),
    fechacreacion   TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_cln_llamada_cobranza_company_cliente
    ON cln_llamada_cobranza(company_id, codigocliente);
