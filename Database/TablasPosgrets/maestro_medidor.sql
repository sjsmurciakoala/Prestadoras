-- Table: public.maestro_medidor

-- DROP TABLE IF EXISTS public.maestro_medidor;

CREATE TABLE IF NOT EXISTS public.maestro_medidor
(
    maestro_medidor_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 23718 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    maestro_medidor_numero character varying(50) COLLATE pg_catalog."default" NOT NULL,
    maestro_medidor_marca character varying(50) COLLATE pg_catalog."default",
    maestro_medidor_fecha_instala timestamp without time zone,
    maestro_medidor_diametro numeric(4,2),
    maestro_medidor_empleado character varying(50) COLLATE pg_catalog."default",
    maestro_medidor_acueducto character varying(20) COLLATE pg_catalog."default",
    estado boolean NOT NULL,
    usuariocreacion character varying(256) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(256) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT maestro_medidor_id_pkey PRIMARY KEY (maestro_medidor_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.maestro_medidor
    OWNER to postgres;