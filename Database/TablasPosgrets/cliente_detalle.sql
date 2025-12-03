-- Table: public.cliente_detalle

-- DROP TABLE IF EXISTS public.cliente_detalle;

CREATE TABLE IF NOT EXISTS public.cliente_detalle
(
    detalle_cliente_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 46839 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    maestro_cliente_id integer NOT NULL,
    detalle_cliente_telefono character varying(15) COLLATE pg_catalog."default",
    detalle_cliente_movil character varying(15) COLLATE pg_catalog."default",
    detalle_cliente_email character varying(100) COLLATE pg_catalog."default",
    detalle_cliente_direccion character varying(200) COLLATE pg_catalog."default",
    detalle_cliente_color_casa character varying(100) COLLATE pg_catalog."default",
    detalle_cliente_inquilino character varying(100) COLLATE pg_catalog."default",
    maestro_medidor_id integer,
    empresa_nombre character varying(100) COLLATE pg_catalog."default",
    empresa_telefono character varying(50) COLLATE pg_catalog."default",
    empresa_direccion character varying(200) COLLATE pg_catalog."default",
    negocio_nombre character varying(100) COLLATE pg_catalog."default",
    negocio_telefono character varying(15) COLLATE pg_catalog."default",
    negocio_clave_catastral character varying(50) COLLATE pg_catalog."default",
    estado boolean,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    clave character varying(32767) COLLATE pg_catalog."default",
    observaciones character varying COLLATE pg_catalog."default",
    numero_contrato character varying COLLATE pg_catalog."default",
    descuento_valor numeric(18,2),
    CONSTRAINT cliente_detalle_pkey PRIMARY KEY (detalle_cliente_id),
    CONSTRAINT maestro_cliente_id_cliente_detalle_fkey FOREIGN KEY (maestro_cliente_id)
        REFERENCES public.cliente_maestro (maestro_cliente_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT maestro_medidor_id_cliente_detalle_fkey FOREIGN KEY (maestro_medidor_id)
        REFERENCES public.maestro_medidor (maestro_medidor_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cliente_detalle
    OWNER to postgres;