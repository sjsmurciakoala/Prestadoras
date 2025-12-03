-- Table: public.prv_tipostransacc

-- DROP TABLE IF EXISTS public.prv_tipostransacc;

CREATE TABLE IF NOT EXISTS public.prv_tipostransacc
(
    cod_tipopartida character varying(10) COLLATE pg_catalog."default" NOT NULL,
    correlativo character varying(6) COLLATE pg_catalog."default" NOT NULL,
    cuenta_contable character varying(20) COLLATE pg_catalog."default" NOT NULL,
    del_sistema character varying(1) COLLATE pg_catalog."default" NOT NULL,
    entra_sale character varying(1) COLLATE pg_catalog."default" NOT NULL,
    fecha_creacion timestamp without time zone NOT NULL,
    fecha_modificacion timestamp without time zone,
    nombre character varying(40) COLLATE pg_catalog."default" NOT NULL,
    observaciones text COLLATE pg_catalog."default",
    pda character varying(1) COLLATE pg_catalog."default",
    tipo_transaccion character varying(3) COLLATE pg_catalog."default" NOT NULL,
    usuario_creo character varying(8) COLLATE pg_catalog."default" NOT NULL,
    usuario_modifica character varying(8) COLLATE pg_catalog."default",
    rowid character varying(100) COLLATE pg_catalog."default" NOT NULL,
    cod_correlativo_dei integer,
    CONSTRAINT "PRV_TIPOSTRANSACC_pkey" PRIMARY KEY (tipo_transaccion)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_tipostransacc
    OWNER to postgres;