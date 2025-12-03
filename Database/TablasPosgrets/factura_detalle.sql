-- Table: public.factura_detalle

-- DROP TABLE IF EXISTS public.factura_detalle;

CREATE TABLE IF NOT EXISTS public.factura_detalle
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    numrecibo integer,
    codigo character varying COLLATE pg_catalog."default",
    tiposervicio character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    montovalor numeric(18,2),
    factura_id integer,
    montovalor_saldo numeric(18,2),
    CONSTRAINT factura_detalle_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.factura_detalle
    OWNER to postgres;