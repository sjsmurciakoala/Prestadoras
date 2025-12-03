-- Table: public.bnc_cuentas

-- DROP TABLE IF EXISTS public.bnc_cuentas;

CREATE TABLE IF NOT EXISTS public.bnc_cuentas
(
    cod_banco integer NOT NULL,
    cod_cuenta integer NOT NULL,
    cod_empresa integer NOT NULL,
    cuenta_contable character varying(20) COLLATE pg_catalog."default" NOT NULL,
    descripcion character varying(150) COLLATE pg_catalog."default",
    emite_cheques boolean,
    numero_cheque character varying(20) COLLATE pg_catalog."default",
    ruta_transito character varying(20) COLLATE pg_catalog."default",
    saldo double precision,
    saldo_conciliado double precision,
    tasa_promedio double precision,
    tipo_cuenta character varying(150) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    codigo character varying(10) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.bnc_cuentas
    OWNER to postgres;