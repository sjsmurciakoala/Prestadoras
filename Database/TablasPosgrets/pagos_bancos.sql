-- Table: public.pagos_bancos

-- DROP TABLE IF EXISTS public.pagos_bancos;

CREATE TABLE IF NOT EXISTS public.pagos_bancos
(
    idreg character(1) COLLATE pg_catalog."default",
    cliente_clave character varying COLLATE pg_catalog."default",
    rtn character varying COLLATE pg_catalog."default",
    recibo numeric(12,0),
    montop numeric(12,2),
    fechap date,
    referencia character varying COLLATE pg_catalog."default",
    banco character varying COLLATE pg_catalog."default",
    sucursal character varying COLLATE pg_catalog."default",
    agencia character varying COLLATE pg_catalog."default",
    cajero character varying COLLATE pg_catalog."default",
    terminal character varying COLLATE pg_catalog."default",
    horap time without time zone
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.pagos_bancos
    OWNER to postgres;