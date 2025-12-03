-- Table: public.log_cliclo_descarga_app

-- DROP TABLE IF EXISTS public.log_cliclo_descarga_app;

CREATE TABLE IF NOT EXISTS public.log_cliclo_descarga_app
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    anio integer,
    mes integer,
    ciclo integer,
    usuario character varying COLLATE pg_catalog."default",
    fecha timestamp without time zone
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.log_cliclo_descarga_app
    OWNER to postgres;