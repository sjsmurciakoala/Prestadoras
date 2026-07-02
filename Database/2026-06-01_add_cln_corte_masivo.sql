-- Encabezado del lote de cortes masivos
CREATE TABLE IF NOT EXISTS cln_corte_masivo_hdr (
    id                SERIAL PRIMARY KEY,
    company_id        BIGINT NOT NULL,
    correlativo       VARCHAR(20) NOT NULL,
    fecha_generacion  DATE NOT NULL DEFAULT CURRENT_DATE,
    criterio          VARCHAR(30) NOT NULL,
    ciclo_id          INT,
    barrio_codigo     VARCHAR(20),
    dias_sin_pago     INT,
    valor_minimo      DECIMAL(18,2),
    categoria_id      INT,
    total_clientes    INT NOT NULL DEFAULT 0,
    estado            VARCHAR(20) NOT NULL DEFAULT 'GENERADO',
    usuario           VARCHAR(100),
    usuariocreacion   VARCHAR(100),
    fechacreacion     TIMESTAMP DEFAULT NOW(),
    CONSTRAINT uq_corte_masivo_hdr_correlativo UNIQUE (company_id, correlativo)
);

CREATE INDEX IF NOT EXISTS ix_cln_corte_masivo_hdr_company
    ON cln_corte_masivo_hdr(company_id);

-- Detalle: un cliente por fila
CREATE TABLE IF NOT EXISTS cln_corte_masivo_dtl (
    id              SERIAL PRIMARY KEY,
    hdr_id          INT NOT NULL REFERENCES cln_corte_masivo_hdr(id),
    company_id      BIGINT NOT NULL,
    cliente_clave   VARCHAR(20) NOT NULL,
    nombre_cliente  VARCHAR(250),
    saldo_adeudado  DECIMAL(18,2),
    dias_sin_pago   INT,
    pagado          BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_pago      DATE
);

CREATE INDEX IF NOT EXISTS ix_cln_corte_masivo_dtl_hdr
    ON cln_corte_masivo_dtl(hdr_id);
CREATE INDEX IF NOT EXISTS ix_cln_corte_masivo_dtl_cliente
    ON cln_corte_masivo_dtl(company_id, cliente_clave);
