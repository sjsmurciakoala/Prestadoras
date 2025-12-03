-- Table: public.presupuesto_fondos

-- DROP TABLE IF EXISTS public.presupuesto_fondos;

CREATE TABLE IF NOT EXISTS public.presupuesto_fondos
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    fondo_descripcion character varying COLLATE pg_catalog."default",
    CONSTRAINT presupuesto_fondos_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.presupuesto_fondos
    OWNER to postgres;