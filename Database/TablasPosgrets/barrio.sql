-- Table: public.barrio

-- DROP TABLE IF EXISTS public.barrio;

CREATE TABLE IF NOT EXISTS public.barrio
(
    barrio_codigo character varying(3) COLLATE pg_catalog."default" NOT NULL,
    descripcion text COLLATE pg_catalog."default" NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT barrio_pkey PRIMARY KEY (barrio_codigo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.barrio
    OWNER to postgres;