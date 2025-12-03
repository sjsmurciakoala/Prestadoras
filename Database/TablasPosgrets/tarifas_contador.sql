-- Table: public.tarifas_contador

-- DROP TABLE IF EXISTS public.tarifas_contador;

CREATE TABLE IF NOT EXISTS public.tarifas_contador
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 17 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    tipo integer NOT NULL,
    categoria_id integer,
    codigo character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    minimo numeric(12,0),
    maximo numeric(12,0),
    cuota numeric(12,2),
    valor_base numeric(12,2),
    alquiler numeric(12,2),
    CONSTRAINT tarifas_contador_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.tarifas_contador
    OWNER to postgres;