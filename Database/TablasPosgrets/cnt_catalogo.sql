-- Table: public.cnt_catalogo

-- DROP TABLE IF EXISTS public.cnt_catalogo;

CREATE TABLE IF NOT EXISTS public.cnt_catalogo
(
    cod_cuenta character varying(20) COLLATE pg_catalog."default" NOT NULL,
    cod_mayor character varying(15) COLLATE pg_catalog."default",
    cod_empresa integer,
    cod_grupo_cta character(1) COLLATE pg_catalog."default",
    cuenta_ext character(1) COLLATE pg_catalog."default",
    fecha_creacion date,
    flag_budget character(1) COLLATE pg_catalog."default",
    flag_fijovariable character(1) COLLATE pg_catalog."default",
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    status character(1) COLLATE pg_catalog."default",
    tipo_cuenta smallint,
    ult_fecha_modificada date,
    ult_usuario character varying(8) COLLATE pg_catalog."default",
    usuario_creo character varying(8) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    cod_sub_grupo character varying(15) COLLATE pg_catalog."default",
    cscuenta character varying(15) COLLATE pg_catalog."default",
    CONSTRAINT cnt_catalogo_pkey PRIMARY KEY (cod_cuenta)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_catalogo
    OWNER to postgres;