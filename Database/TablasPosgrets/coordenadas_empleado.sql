-- Table: public.coordenadas_empleado

-- DROP TABLE IF EXISTS public.coordenadas_empleado;

CREATE TABLE IF NOT EXISTS public.coordenadas_empleado
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    nombre character varying(50) COLLATE pg_catalog."default",
    ano integer,
    mes integer,
    dia integer,
    fecha timestamp without time zone,
    latitud character varying(25) COLLATE pg_catalog."default",
    longitud character varying(25) COLLATE pg_catalog."default",
    CONSTRAINT coordenadas_empleado_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.coordenadas_empleado
    OWNER to postgres;