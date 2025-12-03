-- Table: public.grupoestadodetalle

-- DROP TABLE IF EXISTS public.grupoestadodetalle;

CREATE TABLE IF NOT EXISTS public.grupoestadodetalle
(
    idgrupodetalle integer NOT NULL DEFAULT nextval('grupoestadodetalle_idgrupodetalle_seq'::regclass),
    nombre text COLLATE pg_catalog."default" NOT NULL,
    idgrupo integer NOT NULL,
    CONSTRAINT grupoestadodetalle_pkey PRIMARY KEY (idgrupodetalle),
    CONSTRAINT fk_idgrupo FOREIGN KEY (idgrupo)
        REFERENCES public.grupoestado (idgrupo) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.grupoestadodetalle
    OWNER to postgres;