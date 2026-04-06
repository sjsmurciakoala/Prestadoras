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
	status bool NULL,
	cuenta_bancaria varchar(50) NOT NULL,
	rowid uuid DEFAULT gen_random_uuid() NULL,
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
	nombrebanco1 varchar(80) NULL,
	nombrebanco2 varchar(80) NULL
);

-- Permissions

ALTER TABLE public.prv_proveedores OWNER TO postgres;
GRANT ALL ON TABLE public.prv_proveedores TO postgres;