-- Table: public.prv_kardex

-- DROP TABLE IF EXISTS public.prv_kardex;

CREATE TABLE IF NOT EXISTS public.prv_kardex
(
    cod_proveedor character varying(20) COLLATE pg_catalog."default",
    correlativo character varying(20) COLLATE pg_catalog."default" NOT NULL,
    cuenta_debitar character varying(13) COLLATE pg_catalog."default",
    dias_trn_ant smallint,
    fecha_creacion timestamp with time zone,
    fecha_transaccion timestamp with time zone NOT NULL,
    fec_vencimiento timestamp without time zone,
    monto double precision NOT NULL,
    monto_dolares double precision,
    num_cheque character varying(12) COLLATE pg_catalog."default",
    observaciones character varying(254) COLLATE pg_catalog."default",
    pda character varying(1) COLLATE pg_catalog."default",
    referencia1 character varying(25) COLLATE pg_catalog."default" NOT NULL,
    referencia2 character varying(25) COLLATE pg_catalog."default",
    referencia_afecta character varying(30) COLLATE pg_catalog."default",
    saldo double precision,
    saldo_anterior double precision,
    saldo_dolares double precision,
    status_pago bit(1),
    suma_balance double precision,
    tipo_transaccion character varying(3) COLLATE pg_catalog."default" NOT NULL,
    tipo_transaccion2 character varying(3) COLLATE pg_catalog."default",
    ultima_trn character varying(1) COLLATE pg_catalog."default",
    usuario_creo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    rowid character varying(100) COLLATE pg_catalog."default" NOT NULL,
    correlativo_dei character varying(8) COLLATE pg_catalog."default",
    cai character varying(8) COLLATE pg_catalog."default",
    cod_correlativo_dei integer,
    nombre_proveedor_p character varying(50) COLLATE pg_catalog."default",
    saldo_anterior_dol double precision,
    cuenta_acreditar character varying(13) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_kardex
    OWNER to postgres;