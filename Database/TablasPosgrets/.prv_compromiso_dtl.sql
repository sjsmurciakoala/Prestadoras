-- Table: public.prv_compromiso_dtl

-- DROP TABLE IF EXISTS public.prv_compromiso_dtl;

CREATE TABLE IF NOT EXISTS public.prv_compromiso_dtl
(
    numero_orden integer NOT NULL,
    cod_presupuestario character varying(20) COLLATE pg_catalog."default" NOT NULL,
    programa character varying(2) COLLATE pg_catalog."default" NOT NULL,
    actividad character varying(2) COLLATE pg_catalog."default" NOT NULL,
    objeto_gasto character varying(100) COLLATE pg_catalog."default" NOT NULL,
    cuenta_gasto character varying(20) COLLATE pg_catalog."default" NOT NULL,
    descripcion character varying(150) COLLATE pg_catalog."default" NOT NULL,
    monto numeric(18,2) NOT NULL,
    conceptodtl character varying(100) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_compromiso_dtl
    OWNER to postgres;