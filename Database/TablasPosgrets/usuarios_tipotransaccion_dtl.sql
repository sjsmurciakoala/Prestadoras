-- Table: public.usuarios_tipotransaccion_dtl

-- DROP TABLE IF EXISTS public.usuarios_tipotransaccion_dtl;

CREATE TABLE IF NOT EXISTS public.usuarios_tipotransaccion_dtl
(
    id_usertransacc_dtl integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    cod_usertransacc_hdr integer NOT NULL,
    cod_tipotransaccion character varying(3) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT usuarios_tipotransaccion_dtl_pkey PRIMARY KEY (id_usertransacc_dtl)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.usuarios_tipotransaccion_dtl
    OWNER to postgres;