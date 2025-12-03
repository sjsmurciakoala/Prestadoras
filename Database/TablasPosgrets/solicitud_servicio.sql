-- Table: public.solicitud_servicio

-- DROP TABLE IF EXISTS public.solicitud_servicio;

CREATE TABLE IF NOT EXISTS public.solicitud_servicio
(
    solicitud_servicio_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    cliente_identidad text COLLATE pg_catalog."default" NOT NULL,
    categoria_servicio_id integer NOT NULL,
    cliente_rtn text COLLATE pg_catalog."default",
    cliente_nombre text COLLATE pg_catalog."default" NOT NULL,
    cliente_telefono text COLLATE pg_catalog."default",
    cliente_movil text COLLATE pg_catalog."default" NOT NULL,
    cliente_email text COLLATE pg_catalog."default",
    cliente_direccion text COLLATE pg_catalog."default" NOT NULL,
    cliente_color_casa text COLLATE pg_catalog."default",
    observacion text COLLATE pg_catalog."default",
    empresa_nombre text COLLATE pg_catalog."default",
    empresa_telefono text COLLATE pg_catalog."default",
    empresa_direccion text COLLATE pg_catalog."default",
    negocio_nombre text COLLATE pg_catalog."default",
    negocio_telefono text COLLATE pg_catalog."default",
    negocio_clave_catastral text COLLATE pg_catalog."default",
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    asiginada boolean,
    fechanacimiento timestamp without time zone,
    clave_sure character varying(50) COLLATE pg_catalog."default",
    CONSTRAINT solicitud_servicio_pkey PRIMARY KEY (solicitud_servicio_id),
    CONSTRAINT solicitud_servicio_categoria_servicio_id_fkey FOREIGN KEY (categoria_servicio_id)
        REFERENCES public.categoria_servicio (categoria_servicio_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.solicitud_servicio
    OWNER to postgres;