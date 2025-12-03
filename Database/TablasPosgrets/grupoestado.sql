-- Table: public.grupoestado

-- DROP TABLE IF EXISTS public.grupoestado;

CREATE TABLE IF NOT EXISTS public.grupoestado
(
    idgrupo integer NOT NULL DEFAULT nextval('grupoestado_idgrupo_seq'::regclass),
    nombre text COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT grupoestado_pkey PRIMARY KEY (idgrupo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.grupoestado
    OWNER to postgres;