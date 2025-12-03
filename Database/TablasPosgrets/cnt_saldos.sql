-- Table: public.cnt_saldos

-- DROP TABLE IF EXISTS public.cnt_saldos;

CREATE TABLE IF NOT EXISTS public.cnt_saldos
(
    cargos double precision NOT NULL,
    cod_cuenta character varying(25) COLLATE pg_catalog."default" NOT NULL,
    cod_empresa integer NOT NULL,
    creditos double precision NOT NULL,
    fecha_cierre date,
    hora_cierre timestamp without time zone NOT NULL,
    saldo_actual double precision NOT NULL,
    saldo_anterior double precision NOT NULL,
    ult_fecha_modificada date,
    ult_usuario character varying(100) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid()
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_saldos
    OWNER to postgres;