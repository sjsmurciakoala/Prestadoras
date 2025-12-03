-- Table: public.calendariopro

-- DROP TABLE IF EXISTS public.calendariopro;

CREATE TABLE IF NOT EXISTS public.calendariopro
(
    ide integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ano integer,
    mes integer,
    ciclo character varying COLLATE pg_catalog."default",
    fechaal date,
    fechalec date,
    fechafac date,
    fecharefac date,
    fechavence date,
    diasvence integer,
    fechafac2 date,
    fechavence2 date,
    CONSTRAINT calendariopro_pkey PRIMARY KEY (ide)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.calendariopro
    OWNER to postgres;