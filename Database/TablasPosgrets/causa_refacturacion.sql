-- Table: public.causa_refacturacion

-- DROP TABLE IF EXISTS public.causa_refacturacion;

CREATE TABLE IF NOT EXISTS public.causa_refacturacion
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    codigo character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    tipo character varying COLLATE pg_catalog."default",
    CONSTRAINT causa_refacturacion_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.causa_refacturacion
    OWNER to postgres;