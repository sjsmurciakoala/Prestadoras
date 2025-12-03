-- Table: public.usuarios_tipotransaccion_hdr

-- DROP TABLE IF EXISTS public.usuarios_tipotransaccion_hdr;

CREATE TABLE IF NOT EXISTS public.usuarios_tipotransaccion_hdr
(
    id_usertransacc integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    usuario character varying(60) COLLATE pg_catalog."default" NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    usuario_creo character varying(60) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT usuarios_tipotransaccion_hdr_pkey PRIMARY KEY (id_usertransacc)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.usuarios_tipotransaccion_hdr
    OWNER to postgres;