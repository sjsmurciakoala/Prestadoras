-- Table: public.historicosinmedidor

-- DROP TABLE IF EXISTS public.historicosinmedidor;

CREATE TABLE IF NOT EXISTS public.historicosinmedidor
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    cuenta character varying(15) COLLATE pg_catalog."default",
    ano integer,
    mes integer,
    numerofactura character varying(50) COLLATE pg_catalog."default",
    correlativocai integer,
    idcai integer,
    fecha timestamp without time zone,
    usuario character varying(50) COLLATE pg_catalog."default",
    CONSTRAINT historicosinmedidor_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.historicosinmedidor
    OWNER to postgres;