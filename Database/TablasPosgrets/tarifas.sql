-- Table: public.tarifas

-- DROP TABLE IF EXISTS public.tarifas;

CREATE TABLE IF NOT EXISTS public.tarifas
(
    tipo integer NOT NULL,
    categoria_id integer NOT NULL,
    codigo character varying COLLATE pg_catalog."default" NOT NULL,
    descripcion character varying COLLATE pg_catalog."default",
    valor numeric(18,2),
    CONSTRAINT tarifas_pkey PRIMARY KEY (tipo, categoria_id, codigo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.tarifas
    OWNER to postgres;