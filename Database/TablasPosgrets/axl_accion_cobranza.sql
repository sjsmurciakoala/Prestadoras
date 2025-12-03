-- Table: public.axl_accion_cobranza

-- DROP TABLE IF EXISTS public.axl_accion_cobranza;

CREATE TABLE IF NOT EXISTS public.axl_accion_cobranza
(
    cod_accion integer NOT NULL DEFAULT nextval('axl_accion_cobranza_cod_accion_seq'::regclass),
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid,
    CONSTRAINT axl_accion_cobranza_pkey PRIMARY KEY (cod_accion)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.axl_accion_cobranza
    OWNER to postgres;