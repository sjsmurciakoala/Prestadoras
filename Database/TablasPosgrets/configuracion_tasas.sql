-- Table: public.configuracion_tasas

-- DROP TABLE IF EXISTS public.configuracion_tasas;

CREATE TABLE IF NOT EXISTS public.configuracion_tasas
(
    configuracion_tasas_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 38784 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    maestro_cliente_id integer NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT configuracion_tasas_id_pkey PRIMARY KEY (configuracion_tasas_id),
    CONSTRAINT maestro_cliente_id_fkey FOREIGN KEY (maestro_cliente_id)
        REFERENCES public.cliente_maestro (maestro_cliente_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.configuracion_tasas
    OWNER to postgres;