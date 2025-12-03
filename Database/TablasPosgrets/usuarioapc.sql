-- Table: public.usuarioapc

-- DROP TABLE IF EXISTS public.usuarioapc;

CREATE TABLE IF NOT EXISTS public.usuarioapc
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    nombre character varying(50) COLLATE pg_catalog."default",
    usuario character varying(25) COLLATE pg_catalog."default",
    clave character varying(30) COLLATE pg_catalog."default",
    ruta character varying(6) COLLATE pg_catalog."default",
    estado character varying(1) COLLATE pg_catalog."default",
    CONSTRAINT usuarioapc_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.usuarioapc
    OWNER to postgres;