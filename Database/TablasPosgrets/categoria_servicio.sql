-- Table: public.categoria_servicio

-- DROP TABLE IF EXISTS public.categoria_servicio;

CREATE TABLE IF NOT EXISTS public.categoria_servicio
(
    categoria_servicio_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 9 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    descripcion text COLLATE pg_catalog."default" NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT categoria_servicio_pkey PRIMARY KEY (categoria_servicio_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.categoria_servicio
    OWNER to postgres;