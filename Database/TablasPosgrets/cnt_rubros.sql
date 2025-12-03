-- Table: public.cnt_rubros

-- DROP TABLE IF EXISTS public.cnt_rubros;

CREATE TABLE IF NOT EXISTS public.cnt_rubros
(
    cod_empresa integer NOT NULL,
    cod_reporte integer NOT NULL DEFAULT nextval('cnt_rubros_cod_reporte_seq'::regclass),
    nombre character varying(50) COLLATE pg_catalog."default" NOT NULL,
    orden_reporte integer NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT nombreunico UNIQUE (nombre)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_rubros
    OWNER to postgres;