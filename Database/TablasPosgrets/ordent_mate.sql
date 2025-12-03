-- Table: public.ordent_mate

-- DROP TABLE IF EXISTS public.ordent_mate;

CREATE TABLE IF NOT EXISTS public.ordent_mate
(
    id integer NOT NULL DEFAULT nextval('ordent_mate_id_seq'::regclass),
    cuenta character varying(20) COLLATE pg_catalog."default",
    numero integer,
    codproduc character varying(20) COLLATE pg_catalog."default",
    descripcion character varying(200) COLLATE pg_catalog."default",
    cantidad integer,
    fecha timestamp without time zone,
    CONSTRAINT ordent_mate_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.ordent_mate
    OWNER to postgres;