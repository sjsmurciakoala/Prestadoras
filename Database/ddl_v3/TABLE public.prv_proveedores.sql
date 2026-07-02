-- public.prv_proveedores definition

-- Drop table

-- DROP TABLE public.prv_proveedores;

CREATE TABLE public.prv_proveedores (
	cod_proveedor varchar(20) NOT NULL,
	cod_tipoproveedor int2 NOT NULL,
	nombre varchar(150) NOT NULL,
	cuenta_contable varchar(20) NOT NULL,
	direccion varchar(100) NOT NULL,
	fecha_creacion timestamp NOT NULL,
	fecha_modificacion timestamp NULL,
	usuario_creo varchar(100) NOT NULL,
	usuario_modifica varchar(100) NULL,
	status bool NULL,
	rowid uuid DEFAULT gen_random_uuid() NULL,
	ultimo_correlativo_compromiso int4 DEFAULT 0 NOT NULL,
	compras_acum float8 NULL,
	compras_dolares float8 NULL,
	saldo_actual float8 NULL,
	saldo_act_dolares float8 NULL,
	saldo_anterior float8 NULL,
	saldo_ant_doleres float8 NULL,
	razon_social varchar(150) NULL,
	rtn varchar(20) NULL,
	telefono varchar(20) NULL,
	pagina_web varchar(150) NULL,
	fax varchar(50) NULL,
	email varchar(150) NULL,
	company_id int4 NOT NULL
);

COMMENT ON TABLE public.prv_proveedores IS 'Catalogo maestro de proveedores.';
COMMENT ON COLUMN public.prv_proveedores.cod_proveedor IS 'Codigo interno del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.cod_tipoproveedor IS 'Identificador del tipo de proveedor.';
COMMENT ON COLUMN public.prv_proveedores.nombre IS 'Nombre principal o comercial del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.cuenta_contable IS 'Cuenta contable asociada al proveedor.';
COMMENT ON COLUMN public.prv_proveedores.direccion IS 'Direccion principal del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.fecha_creacion IS 'Fecha de creacion del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.fecha_modificacion IS 'Fecha de ultima modificacion del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.usuario_creo IS 'Usuario que creo el proveedor.';
COMMENT ON COLUMN public.prv_proveedores.usuario_modifica IS 'Usuario que realizo la ultima modificacion del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.status IS 'Indica si el proveedor esta activo.';
COMMENT ON COLUMN public.prv_proveedores.rowid IS 'Identificador unico auxiliar del registro.';
COMMENT ON COLUMN public.prv_proveedores.ultimo_correlativo_compromiso IS 'Ultimo correlativo utilizado en compromisos para este proveedor.';
COMMENT ON COLUMN public.prv_proveedores.compras_acum IS 'Monto acumulado de compras en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.compras_dolares IS 'Monto acumulado de compras en dolares.';
COMMENT ON COLUMN public.prv_proveedores.saldo_actual IS 'Saldo actual del proveedor en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.saldo_act_dolares IS 'Saldo actual del proveedor en dolares.';
COMMENT ON COLUMN public.prv_proveedores.saldo_anterior IS 'Saldo anterior del proveedor en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.saldo_ant_doleres IS 'Saldo anterior del proveedor en dolares.';
COMMENT ON COLUMN public.prv_proveedores.razon_social IS 'Razon social del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.rtn IS 'RTN o identificacion fiscal del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.telefono IS 'Telefono principal de contacto.';
COMMENT ON COLUMN public.prv_proveedores.pagina_web IS 'Sitio web del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.fax IS 'Numero de fax del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.email IS 'Correo electronico principal del proveedor.';

-- Permissions

ALTER TABLE public.prv_proveedores OWNER TO postgres;
GRANT ALL ON TABLE public.prv_proveedores TO postgres;
