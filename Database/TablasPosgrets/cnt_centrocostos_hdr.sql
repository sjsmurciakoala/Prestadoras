-- Table: public.cnt_centrocostos_hdr

-- DROP TABLE IF EXISTS public.cnt_centrocostos_hdr;

CREATE TABLE IF NOT EXISTS public.cnt_centrocostos_hdr
(
    cuenta character varying(9) COLLATE pg_catalog."default" NOT NULL,
    codccg character varying(2) COLLATE pg_catalog."default" NOT NULL,
    codsccg character varying(3) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    contable character varying(20) COLLATE pg_catalog."default",
    CONSTRAINT cnt_centrocostos_hdr_pkey PRIMARY KEY (cuenta)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_centrocostos_hdr
    OWNER to postgres;