-- Table: public.transaccion_abonado

-- DROP TABLE IF EXISTS public.transaccion_abonado;

CREATE TABLE IF NOT EXISTS public.transaccion_abonado
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 3075446 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    cliente_clave character varying COLLATE pg_catalog."default",
    recibo numeric,
    tipotransaccion character varying COLLATE pg_catalog."default",
    docufuente numeric,
    docufuente2 character varying COLLATE pg_catalog."default",
    fecha_docu date,
    tipo_partida character varying COLLATE pg_catalog."default",
    banco character varying COLLATE pg_catalog."default",
    descripcion character varying COLLATE pg_catalog."default",
    plazo numeric,
    docuaplicar numeric,
    trans_aplicar character varying COLLATE pg_catalog."default",
    debitos numeric(12,2),
    creditos numeric(12,2),
    saldo numeric(12,2),
    tipo_servicio character varying COLLATE pg_catalog."default",
    aplicar_alca character varying COLLATE pg_catalog."default",
    periodo character varying COLLATE pg_catalog."default",
    tasa character varying COLLATE pg_catalog."default",
    estado character varying COLLATE pg_catalog."default",
    fecha_registro date,
    ciclo character varying COLLATE pg_catalog."default",
    ruta character varying COLLATE pg_catalog."default",
    secuencia character varying COLLATE pg_catalog."default",
    tiene_med character varying COLLATE pg_catalog."default",
    codigoplan character varying COLLATE pg_catalog."default",
    motivo character varying COLLATE pg_catalog."default",
    usuario character varying COLLATE pg_catalog."default",
    saldo_detalle numeric(18,2),
    CONSTRAINT transaccion_abonado_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.transaccion_abonado
    OWNER to postgres;