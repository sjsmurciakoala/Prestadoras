-- Table: public.presupuestos

-- DROP TABLE IF EXISTS public.presupuestos;

CREATE TABLE IF NOT EXISTS public.presupuestos
(
    id_presupuesto integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    centro_costo character varying(10) COLLATE pg_catalog."default" NOT NULL,
    monto double precision NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_modificacion timestamp with time zone,
    usuario_creo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    usuario_modifico character varying(50) COLLATE pg_catalog."default",
    fondo character varying(50) COLLATE pg_catalog."default" NOT NULL,
    estado boolean,
    ano integer,
    CONSTRAINT presupuestos_pkey PRIMARY KEY (id_presupuesto)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.presupuestos
    OWNER to postgres;