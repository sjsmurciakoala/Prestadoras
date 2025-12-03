-- Table: public.cln_accion_cobranza

-- DROP TABLE IF EXISTS public.cln_accion_cobranza;

CREATE TABLE IF NOT EXISTS public.cln_accion_cobranza
(
    id integer NOT NULL DEFAULT nextval('cln_accion_cobranza_id_seq'::regclass),
    codigocliente character varying(20) COLLATE pg_catalog."default" NOT NULL,
    fecha timestamp without time zone NOT NULL,
    accion character varying(200) COLLATE pg_catalog."default" NOT NULL,
    observacion character varying(200) COLLATE pg_catalog."default",
    CONSTRAINT cln_accion_cobranza_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cln_accion_cobranza
    OWNER to postgres;