-- Table: public.cnt_mayores

-- DROP TABLE IF EXISTS public.cnt_mayores;

CREATE TABLE IF NOT EXISTS public.cnt_mayores
(
    cod_empresa integer,
    cod_grupo_cta character varying(15) COLLATE pg_catalog."default" NOT NULL,
    cod_sub_grupo character varying(15) COLLATE pg_catalog."default" NOT NULL,
    cod_mayor character varying(15) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    orden smallint,
    partida_resumen character(1) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_mayores_pkey PRIMARY KEY (cod_grupo_cta, cod_sub_grupo, cod_mayor)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_mayores
    OWNER to postgres;