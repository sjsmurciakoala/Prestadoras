-- Table: public.historialmes

-- DROP TABLE IF EXISTS public.historialmes;

CREATE TABLE IF NOT EXISTS public.historialmes
(
    ano numeric(4,0) NOT NULL,
    mes numeric(2,0) NOT NULL,
    ciclo character(10) COLLATE pg_catalog."default" NOT NULL,
    ruta character(10) COLLATE pg_catalog."default" DEFAULT NULL::bpchar,
    fecha timestamp without time zone,
    sep numeric(1,0) DEFAULT NULL::numeric,
    sep2 numeric(1,0) DEFAULT '0'::numeric,
    fechacierre date,
    usuarioapertura character(150) COLLATE pg_catalog."default" DEFAULT NULL::bpchar,
    usuariocierre character(20) COLLATE pg_catalog."default" DEFAULT NULL::bpchar,
    cerrado character(1) COLLATE pg_catalog."default" DEFAULT NULL::bpchar,
    cerrarperiodo character(1) COLLATE pg_catalog."default" DEFAULT 'P'::bpchar,
    fechaperiodo timestamp without time zone,
    "2-Sep" character varying(32767) COLLATE pg_catalog."default",
    CONSTRAINT historialmes_pkey PRIMARY KEY (ano, mes, ciclo)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.historialmes
    OWNER to postgres;