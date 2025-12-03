-- Table: public.factura

-- DROP TABLE IF EXISTS public.factura;

CREATE TABLE IF NOT EXISTS public.factura
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    numrecibo integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 3075052 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    numfactura character varying COLLATE pg_catalog."default",
    clientecodigo character varying COLLATE pg_catalog."default",
    tipofactura character varying COLLATE pg_catalog."default",
    ano character varying COLLATE pg_catalog."default",
    mes character varying COLLATE pg_catalog."default",
    fechaemision date,
    fechavence date,
    rtn character varying COLLATE pg_catalog."default",
    periodo character varying COLLATE pg_catalog."default",
    numdei character varying COLLATE pg_catalog."default",
    saldototal numeric(18,2),
    usuario character varying COLLATE pg_catalog."default",
    identidad character varying COLLATE pg_catalog."default",
    estado character varying COLLATE pg_catalog."default",
    recolectora character varying COLLATE pg_catalog."default",
    fechapago date,
    tipofacturacion character varying COLLATE pg_catalog."default",
    referencia character varying COLLATE pg_catalog."default",
    CONSTRAINT factura_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.factura
    OWNER to postgres;