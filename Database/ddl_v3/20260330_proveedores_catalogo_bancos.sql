CREATE TABLE IF NOT EXISTS public.prv_bancos (
    prv_banco_id bigserial PRIMARY KEY,
    nombre varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    fecha_creacion timestamp NOT NULL DEFAULT now(),
    fecha_modificacion timestamp NULL,
    usuario_creo varchar(100) NOT NULL,
    usuario_modifica varchar(100) NULL,
    rowid uuid NULL DEFAULT gen_random_uuid()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_prv_bancos_nombre_normalizado
    ON public.prv_bancos (lower(btrim(nombre)));

COMMENT ON TABLE public.prv_bancos IS 'Catalogo de bancos disponibles para proveedores.';
COMMENT ON COLUMN public.prv_bancos.prv_banco_id IS 'Identificador interno del banco del catalogo.';
COMMENT ON COLUMN public.prv_bancos.nombre IS 'Nombre del banco disponible para seleccionar en proveedores.';
COMMENT ON COLUMN public.prv_bancos.activo IS 'Indica si el banco esta disponible para nuevos registros.';
COMMENT ON COLUMN public.prv_bancos.fecha_creacion IS 'Fecha de creacion del banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.fecha_modificacion IS 'Fecha de ultima modificacion del banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.usuario_creo IS 'Usuario que creo el banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.usuario_modifica IS 'Usuario que realizo la ultima modificacion del banco.';
COMMENT ON COLUMN public.prv_bancos.rowid IS 'Identificador unico auxiliar del registro.';

INSERT INTO public.prv_bancos (nombre, activo, fecha_creacion, usuario_creo)
SELECT banco_normalizado, true, now(), 'migration'
FROM (
    SELECT NULLIF(btrim(banco), '') AS banco_normalizado
    FROM public.prv_proveedor_cuenta_bancaria
) bancos
WHERE banco_normalizado IS NOT NULL
ON CONFLICT DO NOTHING;
