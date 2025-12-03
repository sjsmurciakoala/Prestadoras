-- Table: public.proyectos

-- DROP TABLE IF EXISTS public.proyectos;

CREATE TABLE IF NOT EXISTS public.proyectos
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    empre character varying COLLATE pg_catalog."default",
    ano integer,
    codigo character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    lugar character varying COLLATE pg_catalog."default",
    ubicacion character varying COLLATE pg_catalog."default",
    aprobado numeric(18,2),
    supervisor character varying COLLATE pg_catalog."default",
    ejectutor character varying COLLATE pg_catalog."default",
    presupuesto character varying COLLATE pg_catalog."default",
    fecha1 timestamp without time zone,
    fecha2 timestamp without time zone,
    fuente_financiamiento character varying COLLATE pg_catalog."default",
    ampliado numeric(18,2),
    pagado numeric(18,2),
    fondo character varying COLLATE pg_catalog."default",
    CONSTRAINT proyectos_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.proyectos
    OWNER to postgres;