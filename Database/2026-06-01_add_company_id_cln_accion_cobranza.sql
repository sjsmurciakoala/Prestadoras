-- Agrega company_id a cln_accion_cobranza para soporte multi-tenant
ALTER TABLE cln_accion_cobranza
    ADD COLUMN IF NOT EXISTS company_id BIGINT NOT NULL DEFAULT 1;

ALTER TABLE cln_accion_cobranza
    ALTER COLUMN company_id DROP DEFAULT;

CREATE INDEX IF NOT EXISTS ix_cln_accion_cobranza_company_cliente
    ON cln_accion_cobranza(company_id, codigocliente);
