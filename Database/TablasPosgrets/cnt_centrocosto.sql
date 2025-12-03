-- Table: public.cnt_centroscosto

-- DROP TABLE IF EXISTS public.cnt_centroscosto;

CREATE TABLE IF NOT EXISTS public.cnt_centroscosto
(
    cod_centrocosto integer NOT NULL DEFAULT nextval('cnt_centroscosto_cod_centrocosto_seq'::regclass),
    nom_centrocosto character varying(250) COLLATE pg_catalog."default" NOT NULL,
    status character(1) COLLATE pg_catalog."default",
    flag_tipo_cc boolean,
    fechadesde date,
    fechahasta date,
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_centroscosto_pkey PRIMARY KEY (cod_centrocosto)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_centroscosto
    OWNER to postgres;