-- Table: public.ops_compromisos

-- DROP TABLE IF EXISTS public.ops_compromisos;

CREATE TABLE IF NOT EXISTS public.ops_compromisos
(
    cod_empresa integer NOT NULL,
    ano integer NOT NULL,
    orden character varying(20) COLLATE pg_catalog."default",
    numero numeric(18,2),
    codigo character varying(20) COLLATE pg_catalog."default" NOT NULL,
    beneficiario character varying(150) COLLATE pg_catalog."default",
    cod_programa character varying(4) COLLATE pg_catalog."default",
    cod_actvidad character varying(4) COLLATE pg_catalog."default",
    cod_gastos character varying(4) COLLATE pg_catalog."default",
    compromiso numeric(18,2),
    fecha timestamp without time zone,
    fechavence timestamp without time zone,
    concepto character varying(250) COLLATE pg_catalog."default",
    pagos numeric(18,2),
    fechap timestamp without time zone,
    docu character varying(20) COLLATE pg_catalog."default",
    codproy character varying(20) COLLATE pg_catalog."default",
    fondo character varying(1) COLLATE pg_catalog."default",
    paga numeric(18,2),
    cta_contable character varying(25) COLLATE pg_catalog."default",
    ctacobrar character varying(25) COLLATE pg_catalog."default",
    ordenp integer,
    id integer,
    cod_proveedor character varying(4) COLLATE pg_catalog."default",
    bor character varying(1) COLLATE pg_catalog."default",
    aplicado character varying(1) COLLATE pg_catalog."default",
    rtn character varying(30) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.ops_compromisos
    OWNER to postgres;