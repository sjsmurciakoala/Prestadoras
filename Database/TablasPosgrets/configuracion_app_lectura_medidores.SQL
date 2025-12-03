-- Table: public.configuracion_app_lectura_medidores

-- DROP TABLE IF EXISTS public.configuracion_app_lectura_medidores;

CREATE TABLE IF NOT EXISTS public.configuracion_app_lectura_medidores
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    descripcion character varying(200) COLLATE pg_catalog."default",
    valor_numeros numeric(10,2),
    valor_letras character varying(100) COLLATE pg_catalog."default",
    CONSTRAINT configuracion_app_lectura_medidores_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.configuracion_app_lectura_medidores
    OWNER to postgres;