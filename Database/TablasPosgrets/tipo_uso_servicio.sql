-- Table: public.tipo_uso_servicio

-- DROP TABLE IF EXISTS public.tipo_uso_servicio;

CREATE TABLE IF NOT EXISTS public.tipo_uso_servicio
(
    tipo_uso_codigo character varying(2) COLLATE pg_catalog."default" NOT NULL,
    descripcion text COLLATE pg_catalog."default" NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT tipo_uso_codigo_pkey PRIMARY KEY (tipo_uso_codigo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.tipo_uso_servicio
    OWNER to postgres;