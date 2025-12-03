-- Table: public.orden_trabajo_adjunto

-- DROP TABLE IF EXISTS public.orden_trabajo_adjunto;

CREATE TABLE IF NOT EXISTS public.orden_trabajo_adjunto
(
    id integer NOT NULL DEFAULT nextval('orden_trabajo_adjunto_id_seq'::regclass),
    adjunto bytea,
    nombre character varying(100) COLLATE pg_catalog."default",
    tipo character varying(20) COLLATE pg_catalog."default",
    latitud character varying(100) COLLATE pg_catalog."default",
    longitud character varying(100) COLLATE pg_catalog."default",
    numeroorden character varying(100) COLLATE pg_catalog."default",
    fechainicio timestamp without time zone,
    fechafin timestamp without time zone,
    fechaobtenerordenes timestamp without time zone,
    CONSTRAINT orden_trabajo_adjunto_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.orden_trabajo_adjunto
    OWNER to postgres;