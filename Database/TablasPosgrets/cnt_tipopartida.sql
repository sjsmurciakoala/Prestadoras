-- Table: public.cnt_sub_grupo

-- DROP TABLE IF EXISTS public.cnt_sub_grupo;

CREATE TABLE IF NOT EXISTS public.cnt_sub_grupo
(
    cod_empresa integer,
    cod_grupo character varying(1) COLLATE pg_catalog."default" NOT NULL,
    cod_sub_grupo character varying(2) COLLATE pg_catalog."default" NOT NULL,
    descripcion character varying(150) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_sub_grupo_pkey PRIMARY KEY (cod_grupo, cod_sub_grupo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_sub_grupo
    OWNER to postgres;