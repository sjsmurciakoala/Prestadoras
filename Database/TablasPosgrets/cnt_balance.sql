-- Table: public.cnt_balance

-- DROP TABLE IF EXISTS public.cnt_balance;

CREATE TABLE IF NOT EXISTS public.cnt_balance
(
    cod_cuenta character varying(20) COLLATE pg_catalog."default",
    cod_empresa integer,
    descripcion character varying(100) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    niveles integer,
    cod_reporte integer
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_balance
    OWNER to postgres;