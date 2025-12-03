-- Table: public.cln_plan_pago_hdr

-- DROP TABLE IF EXISTS public.cln_plan_pago_hdr;

CREATE TABLE IF NOT EXISTS public.cln_plan_pago_hdr
(
    id integer NOT NULL DEFAULT nextval('cln_plan_pago_hdr_id_seq'::regclass),
    correlativo character varying(6) COLLATE pg_catalog."default",
    clienteid integer,
    monto numeric(10,2),
    direccion character varying(300) COLLATE pg_catalog."default",
    representante character varying(200) COLLATE pg_catalog."default",
    docrepresentante character varying(20) COLLATE pg_catalog."default",
    numrepresentante character varying(11) COLLATE pg_catalog."default",
    fecha timestamp without time zone,
    fechappago timestamp without time zone,
    comentario text COLLATE pg_catalog."default",
    porcprima numeric(10,2),
    vprima numeric(10,2),
    montofinanc numeric(10,2),
    meses integer,
    estadopago character varying(20) COLLATE pg_catalog."default",
    usuariocreacion character varying(50) COLLATE pg_catalog."default",
    fechacreacion timestamp without time zone,
    usuariomodificacion character varying(50) COLLATE pg_catalog."default",
    fechamodificacion timestamp without time zone,
    CONSTRAINT cln_plan_pago_hdr_pkey PRIMARY KEY (id),
    CONSTRAINT cln_plan_pago_hdr_clienteid_fkey FOREIGN KEY (clienteid)
        REFERENCES public.cliente_maestro (maestro_cliente_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.cln_plan_pago_hdr
    OWNER to postgres;