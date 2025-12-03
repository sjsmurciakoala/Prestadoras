-- Table: public.informativo

-- DROP TABLE IF EXISTS public.informativo;

CREATE TABLE IF NOT EXISTS public.informativo
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    cod_condicion character varying COLLATE pg_catalog."default",
    codigo character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    CONSTRAINT informativo_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.informativo
    OWNER to postgres;