-- Table: public.tipo_d

-- DROP TABLE IF EXISTS public.tipo_d;

CREATE TABLE IF NOT EXISTS public.tipo_d
(
    tipo_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    depto character varying(2) COLLATE pg_catalog."default",
    tipo character varying(2) COLLATE pg_catalog."default",
    descripcion character varying(80) COLLATE pg_catalog."default",
    concepto character varying COLLATE pg_catalog."default",
    depto_appmitrabajo character varying(2) COLLATE pg_catalog."default",
    CONSTRAINT tipo_d_pkey PRIMARY KEY (tipo_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.tipo_d
    OWNER to postgres;