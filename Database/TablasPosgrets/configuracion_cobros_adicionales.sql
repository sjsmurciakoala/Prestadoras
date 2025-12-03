-- Table: public.configuracion_cobros_adicionales_detalle

-- DROP TABLE IF EXISTS public.configuracion_cobros_adicionales_detalle;

CREATE TABLE IF NOT EXISTS public.configuracion_cobros_adicionales_detalle
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    configuracion_cobro_adicional_ide integer NOT NULL,
    servicio_id integer NOT NULL,
    porcentaje numeric(12,2),
    CONSTRAINT configuracion_cobros_adicionales_detalle_pkey PRIMARY KEY (ide),
    CONSTRAINT configuracion_cobros_fkey FOREIGN KEY (configuracion_cobro_adicional_ide)
        REFERENCES public.configuracion_cobros_adicionales (ide) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.configuracion_cobros_adicionales_detalle
    OWNER to postgres;
-- Index: fki_configuracion_cobros_fkey

-- DROP INDEX IF EXISTS public.fki_configuracion_cobros_fkey;

CREATE INDEX IF NOT EXISTS fki_configuracion_cobros_fkey
    ON public.configuracion_cobros_adicionales_detalle USING btree
    (configuracion_cobro_adicional_ide ASC NULLS LAST)
    TABLESPACE pg_default;