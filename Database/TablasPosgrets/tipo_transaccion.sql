-- Table: public.tipo_transaccion

-- DROP TABLE IF EXISTS public.tipo_transaccion;

CREATE TABLE IF NOT EXISTS public.tipo_transaccion
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    codigo character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    estado boolean,
    usuario_actualizacion character varying COLLATE pg_catalog."default",
    fecha_actualizacion character varying COLLATE pg_catalog."default",
    CONSTRAINT tipo_transaccion_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.tipo_transaccion
    OWNER to postgres;