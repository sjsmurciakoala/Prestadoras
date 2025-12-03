-- Table: public.cnt_partidas_dtl

-- DROP TABLE IF EXISTS public.cnt_partidas_dtl;

CREATE TABLE IF NOT EXISTS public.cnt_partidas_dtl
(
    cargos numeric(15,4),
    cod_centrocosto character varying(10) COLLATE pg_catalog."default",
    cod_cliente character varying(6) COLLATE pg_catalog."default",
    cod_cuenta character varying(20) COLLATE pg_catalog."default",
    cod_empresa integer,
    cod_marcagrupo character varying(4) COLLATE pg_catalog."default",
    cod_partida integer,
    comprobante character varying(1000) COLLATE pg_catalog."default",
    concepto character varying(1000) COLLATE pg_catalog."default",
    correlativo integer,
    creditos numeric(15,4),
    tasacambio integer,
    rowid uuid DEFAULT gen_random_uuid()
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_partidas_dtl
    OWNER to postgres;