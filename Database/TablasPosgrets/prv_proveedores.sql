-- Table: public.prv_proveedores

-- DROP TABLE IF EXISTS public.prv_proveedores;

CREATE TABLE IF NOT EXISTS public.prv_proveedores
(
    cod_proveedor character varying(20) COLLATE pg_catalog."default" NOT NULL,
    cod_tipoproveedor smallint NOT NULL,
    nombre character varying(150) COLLATE pg_catalog."default" NOT NULL,
    cuenta_contable character varying(20) COLLATE pg_catalog."default" NOT NULL,
    direccion character varying(100) COLLATE pg_catalog."default" NOT NULL,
    fecha_creacion timestamp without time zone NOT NULL,
    fecha_modificacion timestamp without time zone,
    status boolean,
    cuenta_bancaria character varying(50) COLLATE pg_catalog."default" NOT NULL,
    rowid uuid DEFAULT gen_random_uuid(),
    compras_acum double precision,
    compras_dolares double precision,
    saldo_actual double precision,
    saldo_act_dolares double precision,
    saldo_anterior double precision,
    saldo_ant_doleres double precision,
    razon_social character varying(150) COLLATE pg_catalog."default",
    rtn character varying(20) COLLATE pg_catalog."default",
    telefono character varying(20) COLLATE pg_catalog."default",
    pagina_web character varying(150) COLLATE pg_catalog."default",
    fax character varying(50) COLLATE pg_catalog."default",
    email character varying(150) COLLATE pg_catalog."default",
    nombrebanco1 character varying(80) COLLATE pg_catalog."default",
    nombrebanco2 character varying(80) COLLATE pg_catalog."default"
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.prv_proveedores
    OWNER to postgres;