-- Table: public.ajustes_detalle

-- DROP TABLE IF EXISTS public.ajustes_detalle;

CREATE TABLE IF NOT EXISTS public.ajustes_detalle
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    documento integer,
    tipo_servicio character varying COLLATE pg_catalog."default",
    monto numeric(18,2),
    CONSTRAINT ajustes_detalle_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.ajustes_detalle
    OWNER to postgres;