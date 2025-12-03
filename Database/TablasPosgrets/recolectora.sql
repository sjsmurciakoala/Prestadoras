-- Table: public.recolectora

-- DROP TABLE IF EXISTS public.recolectora;

CREATE TABLE IF NOT EXISTS public.recolectora
(
    codigo character varying(3) COLLATE pg_catalog."default" NOT NULL,
    descripcion character(40) COLLATE pg_catalog."default",
    ctabanco character(3) COLLATE pg_catalog."default",
    aplica numeric(11,4),
    contable character(20) COLLATE pg_catalog."default",
    llave character varying(45) COLLATE pg_catalog."default",
    vigencia date,
    idbancows character varying(3) COLLATE pg_catalog."default",
    logo bytea,
    CONSTRAINT recolectora_pkey PRIMARY KEY (codigo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.recolectora
    OWNER to postgres;