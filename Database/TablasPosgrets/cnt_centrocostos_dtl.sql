-- Table: public.cnt_centrocostos_dtl

-- DROP TABLE IF EXISTS public.cnt_centrocostos_dtl;

CREATE TABLE IF NOT EXISTS public.cnt_centrocostos_dtl
(
    cuenta character varying(9) COLLATE pg_catalog."default" NOT NULL,
    aprobado money NOT NULL,
    compro money NOT NULL,
    pagado money NOT NULL,
    obs money NOT NULL,
    valor money NOT NULL,
    ampl money NOT NULL,
    saldo money NOT NULL,
    mov money NOT NULL,
    transfe money NOT NULL,
    fondo money NOT NULL,
    proyeccion money NOT NULL,
    nuevoaprobado money NOT NULL,
    tipo character varying(10) COLLATE pg_catalog."default" NOT NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cnt_centrocostos_dtl
    OWNER to postgres;