-- Table: public.bnc_kardex_cuentas

-- DROP TABLE IF EXISTS public.bnc_kardex_cuentas;

CREATE TABLE IF NOT EXISTS public.bnc_kardex_cuentas
(
    cod_banco integer NOT NULL,
    cod_cuenta integer NOT NULL,
    conciliada character(1) COLLATE pg_catalog."default",
    correlativo character varying(10) COLLATE pg_catalog."default",
    disas_tran_ant smallint,
    fecha_creacion timestamp without time zone,
    fecha_transaccion timestamp without time zone,
    monto double precision NOT NULL,
    monto_dolares double precision,
    num_cheque character varying(12) COLLATE pg_catalog."default",
    observaciones character varying(250) COLLATE pg_catalog."default",
    pda character(1) COLLATE pg_catalog."default",
    referencia1 character varying(100) COLLATE pg_catalog."default",
    referencia2 character varying(100) COLLATE pg_catalog."default",
    referencia_afecta character varying(100) COLLATE pg_catalog."default",
    saldo double precision NOT NULL,
    saldo_ant double precision NOT NULL,
    saldo_dol double precision,
    saldo_dol_ant double precision,
    suma_balance double precision,
    tasa double precision,
    tipo_transacion character varying(10) COLLATE pg_catalog."default",
    tipo_transacion2 character varying(10) COLLATE pg_catalog."default",
    ultima_trn character(1) COLLATE pg_catalog."default",
    usuario_creo character varying(100) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid()
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.bnc_kardex_cuentas
    OWNER to postgres;