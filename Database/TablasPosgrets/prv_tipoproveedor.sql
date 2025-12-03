-- Table: public.prv_tipoproveedor

-- DROP TABLE IF EXISTS public.prv_tipoproveedor;

CREATE TABLE IF NOT EXISTS public.prv_tipoproveedor
(
    cod_tipoproveedor integer NOT NULL DEFAULT nextval('prv_tipoproveedor_cod_tipoproveedor_seq'::regclass),
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    observaciones character varying(500) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid()
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_tipoproveedor
    OWNER to postgres;