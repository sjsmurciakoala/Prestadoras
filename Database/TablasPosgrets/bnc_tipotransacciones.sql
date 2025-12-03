-- Table: public.bnc_tipotransacciones

-- DROP TABLE IF EXISTS public.bnc_tipotransacciones;

CREATE TABLE IF NOT EXISTS public.bnc_tipotransacciones
(
    cod_centrocosto character varying(10) COLLATE pg_catalog."default" NOT NULL,
    cod_partida integer NOT NULL,
    correlativo character varying(10) COLLATE pg_catalog."default" NOT NULL,
    cuenta_contable character varying(20) COLLATE pg_catalog."default" NOT NULL,
    del_sistema character(1) COLLATE pg_catalog."default" NOT NULL,
    destino character varying(9) COLLATE pg_catalog."default",
    emite_cheque character(1) COLLATE pg_catalog."default" NOT NULL,
    entra_sale character(1) COLLATE pg_catalog."default" NOT NULL,
    fecha_creacion timestamp without time zone NOT NULL,
    fecha_modificacion timestamp without time zone NOT NULL,
    filtro smallint,
    nombre character varying(100) COLLATE pg_catalog."default" NOT NULL,
    observaciones text COLLATE pg_catalog."default",
    pad character(1) COLLATE pg_catalog."default",
    pda character(1) COLLATE pg_catalog."default",
    rel_empleados character(1) COLLATE pg_catalog."default",
    tipo_transaccion character varying(3) COLLATE pg_catalog."default" NOT NULL,
    trn_prestamo character(1) COLLATE pg_catalog."default",
    usuario_creo character varying(100) COLLATE pg_catalog."default" NOT NULL,
    usuario_modifica character varying(100) COLLATE pg_catalog."default",
    rowid uuid DEFAULT gen_random_uuid(),
    cuenta_alterna boolean
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.bnc_tipotransacciones
    OWNER to postgres;