-- Table: public.cln_plan_pago_dtl

-- DROP TABLE IF EXISTS public.cln_plan_pago_dtl;

CREATE TABLE IF NOT EXISTS public.cln_plan_pago_dtl
(
    id integer NOT NULL DEFAULT nextval('cln_plan_pago_dtl_id_seq'::regclass),
    idhdr integer,
    valorcuota numeric(10,2),
    fechacuota timestamp without time zone,
    mes integer,
    estadopago character varying(20) COLLATE pg_catalog."default",
    usuariocreacion character varying(50) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(50) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT cln_plan_pago_dtl_pkey PRIMARY KEY (id),
    CONSTRAINT cln_plan_pago_dtl_idhdr_fkey FOREIGN KEY (idhdr)
        REFERENCES public.cln_plan_pago_hdr (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cln_plan_pago_dtl
    OWNER to postgres;