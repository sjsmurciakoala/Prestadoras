-- Table: public.servicios

-- DROP TABLE IF EXISTS public.servicios;

CREATE TABLE IF NOT EXISTS public.servicios
(
    servicios_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    servicios_codigo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    servicios_descripcioncorta character varying(100) COLLATE pg_catalog."default" NOT NULL,
    servicios_descripcionlarga character varying(300) COLLATE pg_catalog."default",
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT servicios_id_pkey PRIMARY KEY (servicios_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.servicios
    OWNER to postgres;