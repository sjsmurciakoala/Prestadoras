+-- public.prv_bancos definition

-- Drop table

-- DROP TABLE public.prv_bancos;

CREATE TABLE public.prv_bancos (
	prv_banco_id bigserial NOT NULL,
	nombre varchar(80) NOT NULL,
	activo bool DEFAULT true NOT NULL,
	fecha_creacion timestamp DEFAULT now() NOT NULL,
	fecha_modificacion timestamp NULL,
	rowid uuid DEFAULT gen_random_uuid() NULL,
	CONSTRAINT prv_bancos_pkey PRIMARY KEY (prv_banco_id)
);
CREATE UNIQUE INDEX ux_prv_bancos_nombre_normalizado ON public.prv_bancos USING btree (lower(btrim((nombre)::text)));

COMMENT ON TABLE public.prv_bancos IS 'Catalogo de bancos disponibles para proveedores.';
COMMENT ON COLUMN public.prv_bancos.prv_banco_id IS 'Identificador interno del banco del catalogo.';
COMMENT ON COLUMN public.prv_bancos.nombre IS 'Nombre del banco disponible para seleccionar en proveedores.';
COMMENT ON COLUMN public.prv_bancos.activo IS 'Indica si el banco esta disponible para nuevos registros.';
COMMENT ON COLUMN public.prv_bancos.fecha_creacion IS 'Fecha de creacion del banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.fecha_modificacion IS 'Fecha de ultima modificacion del banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.rowid IS 'Identificador unico auxiliar del registro.';

-- Permissions

ALTER TABLE public.prv_bancos OWNER TO postgres;
GRANT ALL ON TABLE public.prv_bancos TO postgres;
