-- Table: public.prv_compromiso_hdr

-- DROP TABLE IF EXISTS public.prv_compromiso_hdr;

CREATE TABLE IF NOT EXISTS public.prv_compromiso_hdr
(
    numero_orden integer NOT NULL,
    fecha timestamp without time zone NOT NULL,
    monto numeric(18,2) NOT NULL,
    concepto character varying(150) COLLATE pg_catalog."default" NOT NULL,
    cod_proveedor character varying(7) COLLATE pg_catalog."default",
    flag_proveedor integer,
    cuenta_contable character varying(20) COLLATE pg_catalog."default",
    cod_proyecto character varying(20) COLLATE pg_catalog."default",
    rtn character varying(20) COLLATE pg_catalog."default",
    pagar_a character varying(100) COLLATE pg_catalog."default",
    status_transacc boolean
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_compromiso_hdr
    OWNER to postgres;