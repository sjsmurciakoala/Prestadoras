-- public.prv_compromiso_hdr definition

-- Drop table

-- DROP TABLE public.prv_compromiso_hdr;

CREATE TABLE public.prv_compromiso_hdr (
	numero_orden int4 NOT NULL,
	correlativo_proveedor int4 NULL,
	fecha timestamp NOT NULL,
	monto numeric(18, 2) NOT NULL,
	concepto varchar(150) NOT NULL,
	cod_proveedor varchar(7) NULL,
	flag_proveedor int4 NULL,
	cuenta_contable varchar(20) NULL,
	cod_proyecto varchar(20) NULL,
	rtn varchar(20) NULL,
	pagar_a varchar(100) NULL,
	status_transacc bool NULL,
	nombre_proveedor varchar(150) NULL
);

COMMENT ON TABLE public.prv_compromiso_hdr IS 'Encabezado de compromisos registrados para proveedores.';
COMMENT ON COLUMN public.prv_compromiso_hdr.numero_orden IS 'Numero global consecutivo del compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.correlativo_proveedor IS 'Correlativo consecutivo del compromiso dentro del proveedor.';
COMMENT ON COLUMN public.prv_compromiso_hdr.fecha IS 'Fecha del compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.monto IS 'Monto total del compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.concepto IS 'Concepto principal del compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.cod_proveedor IS 'Codigo del proveedor asociado al compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.flag_proveedor IS 'Indicador de si el compromiso esta ligado a un proveedor.';
COMMENT ON COLUMN public.prv_compromiso_hdr.cuenta_contable IS 'Cuenta contable principal asociada al compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.cod_proyecto IS 'Codigo del proyecto vinculado al compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.rtn IS 'RTN del proveedor o beneficiario del compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.pagar_a IS 'Nombre del beneficiario a quien se paga el compromiso.';
COMMENT ON COLUMN public.prv_compromiso_hdr.status_transacc IS 'Indica si el compromiso ya fue procesado contablemente.';

-- Permissions

ALTER TABLE public.prv_compromiso_hdr OWNER TO postgres;
GRANT ALL ON TABLE public.prv_compromiso_hdr TO postgres;
