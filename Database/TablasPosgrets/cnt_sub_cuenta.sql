-- Table: public.cnt_sub_cuenta

-- DROP TABLE IF EXISTS public.cnt_sub_cuenta;

CREATE TABLE IF NOT EXISTS public.cnt_sub_cuenta
(
    cod_empresa integer,
    cod_grupo character varying(15) COLLATE pg_catalog."default" NOT NULL,
    csgrupo character varying(15) COLLATE pg_catalog."default" NOT NULL,
    cod_mayor character varying(15) COLLATE pg_catalog."default" NOT NULL,
    cscuenta character varying(15) COLLATE pg_catalog."default" NOT NULL,
    descripcion character varying(150) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_sub_cuenta_pkey PRIMARY KEY (cod_grupo, csgrupo, cod_mayor, cscuenta)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_sub_cuenta
    OWNER to postgres;