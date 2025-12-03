-- Table: public.abogado

-- DROP TABLE IF EXISTS public.abogado;

CREATE TABLE IF NOT EXISTS public.abogado
(
    abogado_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    abogado_codigo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    abogado_nombrecorto character varying(100) COLLATE pg_catalog."default" NOT NULL,
    abogado_nombrelargo character varying(300) COLLATE pg_catalog."default",
    abogado_telefono character varying(11) COLLATE pg_catalog."default",
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    codcuenta character varying(100) COLLATE pg_catalog."default",
    CONSTRAINT abogado_id_pkey PRIMARY KEY (abogado_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.abogado
    OWNER to postgres;