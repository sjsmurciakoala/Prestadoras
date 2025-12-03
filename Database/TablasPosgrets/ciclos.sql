-- Table: public.ciclos

-- DROP TABLE IF EXISTS public.ciclos;

CREATE TABLE IF NOT EXISTS public.ciclos
(
    ciclos_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ciclos_codigo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    ciclos_descripcioncorta character varying(100) COLLATE pg_catalog."default" NOT NULL,
    ciclos_descripcionlarga character varying(300) COLLATE pg_catalog."default" NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT ciclos_id_pkey PRIMARY KEY (ciclos_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.ciclos
    OWNER to postgres;