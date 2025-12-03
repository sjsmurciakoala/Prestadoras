-- Table: public.cai

-- DROP TABLE IF EXISTS public.cai;

CREATE TABLE IF NOT EXISTS public.cai
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ruta character varying(10) COLLATE pg_catalog."default",
    cai character varying(60) COLLATE pg_catalog."default",
    fecha_emision date,
    rango_inicial integer,
    rango_final integer,
    codigo_base character varying(50) COLLATE pg_catalog."default",
    contador_actual integer,
    CONSTRAINT cai_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cai
    OWNER to postgres;