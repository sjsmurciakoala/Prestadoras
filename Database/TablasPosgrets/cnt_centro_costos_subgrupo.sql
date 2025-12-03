-- Table: public.cnt_centro_costos_subgrupo

-- DROP TABLE IF EXISTS public.cnt_centro_costos_subgrupo;

CREATE TABLE IF NOT EXISTS public.cnt_centro_costos_subgrupo
(
    codccg character varying(2) COLLATE pg_catalog."default" NOT NULL,
    codsccg character varying(2) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_centro_costos_subgrupo_pkey PRIMARY KEY (codccg, codsccg)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_centro_costos_subgrupo
    OWNER to postgres;