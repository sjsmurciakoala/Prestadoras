-- Table: public.pruebainsert

-- DROP TABLE IF EXISTS public.pruebainsert;

CREATE TABLE IF NOT EXISTS public.pruebainsert
(
    id integer NOT NULL DEFAULT nextval('pruebainsert_id_seq'::regclass),
    ejemplo character varying(500) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.pruebainsert
    OWNER to postgres;