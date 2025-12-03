-- Table: public.cnt_partidas_hdr

-- DROP TABLE IF EXISTS public.cnt_partidas_hdr;

CREATE TABLE IF NOT EXISTS public.cnt_partidas_hdr
(
    cod_empresa integer NOT NULL,
    cod_partida integer NOT NULL DEFAULT nextval('cnt_partidas_hdr_cod_partida_seq'::regclass),
    cod_tipopartid integer NOT NULL,
    correlativo character varying(15) COLLATE pg_catalog."default",
    fecha_creacion date,
    hora_creacion timestamp without time zone,
    fecha_partida date,
    maestro character(1) COLLATE pg_catalog."default",
    sinopsis character varying(1000) COLLATE pg_catalog."default",
    tipo_transaccion character(3) COLLATE pg_catalog."default",
    usuario_creacion character varying(100) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    CONSTRAINT cnt_partidas_hdr_pkey PRIMARY KEY (cod_partida)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_partidas_hdr
    OWNER to postgres;