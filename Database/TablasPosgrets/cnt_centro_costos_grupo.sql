-- Table: public.cnt_centro_costos_grupo

-- DROP TABLE IF EXISTS public.cnt_centro_costos_grupo;

CREATE TABLE IF NOT EXISTS public.cnt_centro_costos_grupo
(
    cod_empresa integer,
    codccg character varying(2) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_centro_costos_grupo_pkey PRIMARY KEY (codccg)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_centro_costos_grupo
    OWNER to postgres;