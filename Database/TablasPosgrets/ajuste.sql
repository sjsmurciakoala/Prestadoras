-- Table: public.ajustes

-- DROP TABLE IF EXISTS public.ajustes;

CREATE TABLE IF NOT EXISTS public.ajustes
(
    documento integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    fecha date,
    estado character varying COLLATE pg_catalog."default",
    observacion character varying COLLATE pg_catalog."default",
    total numeric(18,2),
    motivo integer,
    tipo_nota integer,
    saldo numeric(18,2),
    periodo character varying COLLATE pg_catalog."default",
    lectura numeric(18,2),
    usuario character varying COLLATE pg_catalog."default",
    cliente_clave character varying(20) COLLATE pg_catalog."default",
    correlativo character varying(6) COLLATE pg_catalog."default",
    CONSTRAINT ajustes_pkey PRIMARY KEY (documento)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.ajustes
    OWNER to postgres;