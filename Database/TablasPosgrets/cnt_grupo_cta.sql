-- Table: public.cnt_grupo_cta

-- DROP TABLE IF EXISTS public.cnt_grupo_cta;

CREATE TABLE IF NOT EXISTS public.cnt_grupo_cta
(
    cod_empresa integer,
    cod_grupo_cta character varying(1) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_grupo_cta_pkey PRIMARY KEY (cod_grupo_cta)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_grupo_cta
    OWNER to postgres;