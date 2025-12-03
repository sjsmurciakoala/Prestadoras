-- Table: public.usuarios_miorden

-- DROP TABLE IF EXISTS public.usuarios_miorden;

CREATE TABLE IF NOT EXISTS public.usuarios_miorden
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    nombre character varying COLLATE pg_catalog."default" NOT NULL,
    usuario character varying COLLATE pg_catalog."default" NOT NULL,
    clave character varying COLLATE pg_catalog."default" NOT NULL,
    tipo integer NOT NULL,
    estado bit(1) NOT NULL,
    CONSTRAINT usuarios_miorden_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.usuarios_miorden
    OWNER to postgres;