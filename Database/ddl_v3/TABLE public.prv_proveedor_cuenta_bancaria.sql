-- public.prv_proveedor_cuenta_bancaria definition

-- Drop table

-- DROP TABLE public.prv_proveedor_cuenta_bancaria;

CREATE TABLE public.prv_proveedor_cuenta_bancaria (
	proveedor_cuenta_bancaria_id bigserial NOT NULL,
	cod_proveedor varchar(20) NOT NULL,
	banco varchar(80) NOT NULL,
	cuenta_bancaria varchar(50) NOT NULL,
	orden int4 DEFAULT 1 NOT NULL,
	fecha_creacion timestamp DEFAULT now() NOT NULL,
	fecha_modificacion timestamp NULL,
	usuario_creo varchar(100) NOT NULL,
	usuario_modifica varchar(100) NULL,
	rowid uuid DEFAULT gen_random_uuid() NULL,
	CONSTRAINT prv_proveedor_cuenta_bancaria_pkey PRIMARY KEY (proveedor_cuenta_bancaria_id)
);
CREATE INDEX ix_prv_proveedor_cuenta_bancaria_cod_proveedor ON public.prv_proveedor_cuenta_bancaria USING btree (cod_proveedor);
CREATE INDEX ix_prv_proveedor_cuenta_bancaria_orden ON public.prv_proveedor_cuenta_bancaria USING btree (cod_proveedor, orden);

COMMENT ON TABLE public.prv_proveedor_cuenta_bancaria IS 'Detalle de cuentas bancarias registradas por proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.proveedor_cuenta_bancaria_id IS 'Identificador interno del detalle bancario del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.cod_proveedor IS 'Codigo legado del proveedor en prv_proveedores.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.banco IS 'Nombre del banco del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.cuenta_bancaria IS 'Numero de cuenta bancaria del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.orden IS 'Orden de visualizacion de la cuenta bancaria dentro del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.fecha_creacion IS 'Fecha de creacion del detalle bancario.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.fecha_modificacion IS 'Fecha de ultima modificacion del detalle bancario.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.usuario_creo IS 'Usuario que creo el detalle bancario del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.usuario_modifica IS 'Usuario que realizo la ultima modificacion del detalle bancario.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.rowid IS 'Identificador unico auxiliar del registro.';

-- Permissions

ALTER TABLE public.prv_proveedor_cuenta_bancaria OWNER TO postgres;
GRANT ALL ON TABLE public.prv_proveedor_cuenta_bancaria TO postgres;
