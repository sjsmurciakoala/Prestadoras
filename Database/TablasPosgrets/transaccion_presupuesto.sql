-- Table: public.transaccion_presupuesto

-- DROP TABLE IF EXISTS public.transaccion_presupuesto;

CREATE TABLE IF NOT EXISTS public.transaccion_presupuesto
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    trpr_descripcion character varying COLLATE pg_catalog."default",
    trpr_tipo_transaccion character varying COLLATE pg_catalog."default",
    trpr_presupuesto_origen character varying COLLATE pg_catalog."default",
    trpr_fecha timestamp without time zone,
    trpr_monto numeric(18,2),
    trp_saldo numeric,
    trpr_ano integer,
    trpr_destino character varying COLLATE pg_catalog."default",
    trpr_codigoproyecto character varying COLLATE pg_catalog."default",
    trpr_tipodestino character varying COLLATE pg_catalog."default",
    trpr_fondo_id integer,
    CONSTRAINT transaccion_presupuesto_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.transaccion_presupuesto
    OWNER to postgres;