-- Table: public.miscelaneos_catalogo

-- DROP TABLE IF EXISTS public.miscelaneos_catalogo;

CREATE TABLE IF NOT EXISTS public.miscelaneos_catalogo
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    codigo character varying COLLATE pg_catalog."default",
    nombre character varying COLLATE pg_catalog."default",
    valor numeric(18,2),
    CONSTRAINT miscelaneos_catalogo_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.miscelaneos_catalogo
    OWNER to postgres;