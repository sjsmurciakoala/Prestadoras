-- Table: public.cliente_maestro

-- DROP TABLE IF EXISTS public.cliente_maestro;

CREATE TABLE IF NOT EXISTS public.cliente_maestro
(
    maestro_cliente_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 102789 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    maestro_cliente_clave character varying(20) COLLATE pg_catalog."default" NOT NULL,
    maestro_cliente_identidad text COLLATE pg_catalog."default" NOT NULL,
    maestro_cliente_rtn text COLLATE pg_catalog."default",
    maestro_cliente_nombre text COLLATE pg_catalog."default" NOT NULL,
    maestro_cliente_tercera_edad boolean,
    categoria_servicio_id integer,
    barrio_codigo character varying(5) COLLATE pg_catalog."default",
    maestro_cliente_fecha_baja timestamp without time zone,
    maestro_cliente_indicativo_ruta character varying(25) COLLATE pg_catalog."default",
    maestro_cliente_secuencia character varying(6) COLLATE pg_catalog."default",
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    tipo_uso_codigo character varying(2) COLLATE pg_catalog."default",
    ciclos_id integer,
    cliente_fecha_nac timestamp without time zone,
    maestro_cliente_tiene_contrato boolean,
    maestro_cliente_tiene_convenio boolean,
    maestro_cliente_tiene_medidor boolean DEFAULT false,
    clave_sure character varying(40) COLLATE pg_catalog."default",
    contador character varying(50) COLLATE pg_catalog."default",
    letracodigo character varying(10) COLLATE pg_catalog."default",
    descuento_tercera_edad double precision,
    bloqueado_cobranza boolean DEFAULT false,
    abogado integer,
    CONSTRAINT cliente_maestro_pkey PRIMARY KEY (maestro_cliente_id),
    CONSTRAINT cliente_maestro_unique UNIQUE (maestro_cliente_clave),
    CONSTRAINT barrio_codigo_cliente_maestro_fkey FOREIGN KEY (barrio_codigo)
        REFERENCES public.barrio (barrio_codigo) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT categoria_servicio_id_cliente_maestro_fkey FOREIGN KEY (categoria_servicio_id)
        REFERENCES public.categoria_servicio (categoria_servicio_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT ciclos_id_cliente_maestro_fkey FOREIGN KEY (ciclos_id)
        REFERENCES public.ciclos (ciclos_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT tipo_uso_codigo_cliente_maestro_fkey FOREIGN KEY (tipo_uso_codigo)
        REFERENCES public.tipo_uso_servicio (tipo_uso_codigo) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cliente_maestro
    OWNER to postgres;