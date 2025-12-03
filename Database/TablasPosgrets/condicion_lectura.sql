-- Table: public.concepto_cobro_adicional

-- DROP TABLE IF EXISTS public.concepto_cobro_adicional;

CREATE TABLE IF NOT EXISTS public.concepto_cobro_adicional
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    concepto character varying COLLATE pg_catalog."default",
    CONSTRAINT concepto_cobro_adicional_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.concepto_cobro_adicional
    OWNER to postgres;