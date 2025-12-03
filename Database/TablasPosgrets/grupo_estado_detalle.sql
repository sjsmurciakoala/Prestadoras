-- Table: public.grupo_estado_detalle

-- DROP TABLE IF EXISTS public.grupo_estado_detalle;

CREATE TABLE IF NOT EXISTS public.grupo_estado_detalle
(
    ide integer NOT NULL DEFAULT nextval('grupo_estado_detalle_ide_seq'::regclass),
    nombre text COLLATE pg_catalog."default" NOT NULL,
    grupo_id integer,
    CONSTRAINT grupo_estado_detalle_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.grupo_estado_detalle
    OWNER to postgres;