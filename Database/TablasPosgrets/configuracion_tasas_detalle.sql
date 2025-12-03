-- Table: public.configuracion_tasas_detalle

-- DROP TABLE IF EXISTS public.configuracion_tasas_detalle;

CREATE TABLE IF NOT EXISTS public.configuracion_tasas_detalle
(
    configuracion_tasas_detalle_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 149941 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    configuracion_tasas_id integer NOT NULL,
    servicios_id integer NOT NULL,
    configuracion_tasas_detalle_aplicaservicio boolean NOT NULL,
    configuracion_tasas_detalle_monto numeric(11,4) NOT NULL,
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT configuracion_tasas_detalle_id_pkey PRIMARY KEY (configuracion_tasas_detalle_id),
    CONSTRAINT configurcion_tasas_id_fkey FOREIGN KEY (configuracion_tasas_id)
        REFERENCES public.configuracion_tasas (configuracion_tasas_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.configuracion_tasas_detalle
    OWNER to postgres;