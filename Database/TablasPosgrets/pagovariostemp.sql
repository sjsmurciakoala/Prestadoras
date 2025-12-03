-- Table: public.pagovariostemp

-- DROP TABLE IF EXISTS public.pagovariostemp;

CREATE TABLE IF NOT EXISTS public.pagovariostemp
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    recibo integer,
    codigo character varying COLLATE pg_catalog."default",
    fecha date,
    fecha_vence date,
    identidad character varying COLLATE pg_catalog."default",
    nombre character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    "valor " numeric(18,2),
    usuario character varying COLLATE pg_catalog."default",
    tipo_servicio character varying COLLATE pg_catalog."default",
    tipo_factura character varying COLLATE pg_catalog."default",
    cod_banco character varying COLLATE pg_catalog."default",
    cajero character varying COLLATE pg_catalog."default",
    cliente_clave character varying COLLATE pg_catalog."default",
    estado character varying COLLATE pg_catalog."default",
    expe character varying COLLATE pg_catalog."default",
    CONSTRAINT pagovariostemp_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.pagovariostemp
    OWNER to postgres;