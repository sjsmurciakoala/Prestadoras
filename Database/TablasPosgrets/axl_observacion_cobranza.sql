-- Table: public.axl_observacion_cobranza

-- DROP TABLE IF EXISTS public.axl_observacion_cobranza;

CREATE TABLE IF NOT EXISTS public.axl_observacion_cobranza
(
    id integer NOT NULL DEFAULT nextval('axl_observacion_cobranza_id_seq'::regclass),
    observacion character varying(50) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT axl_observacion_cobranza_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.axl_observacion_cobranza
    OWNER to postgres;