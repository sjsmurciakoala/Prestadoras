ALTER TABLE public.prv_bancos
    ADD COLUMN IF NOT EXISTS usuario_creo varchar(100),
    ADD COLUMN IF NOT EXISTS usuario_modifica varchar(100);

UPDATE public.prv_bancos
SET usuario_creo = 'migration'
WHERE usuario_creo IS NULL
   OR NULLIF(btrim(usuario_creo), '') IS NULL;

ALTER TABLE public.prv_bancos
    ALTER COLUMN usuario_creo SET NOT NULL;

COMMENT ON COLUMN public.prv_bancos.usuario_creo IS 'Usuario que creo el banco en el catalogo.';
COMMENT ON COLUMN public.prv_bancos.usuario_modifica IS 'Usuario que realizo la ultima modificacion del banco.';

ALTER TABLE public.prv_proveedor_cuenta_bancaria
    ADD COLUMN IF NOT EXISTS usuario_creo varchar(100),
    ADD COLUMN IF NOT EXISTS usuario_modifica varchar(100);

UPDATE public.prv_proveedor_cuenta_bancaria
SET usuario_creo = 'migration'
WHERE usuario_creo IS NULL
   OR NULLIF(btrim(usuario_creo), '') IS NULL;

ALTER TABLE public.prv_proveedor_cuenta_bancaria
    ALTER COLUMN usuario_creo SET NOT NULL;

COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.usuario_creo IS 'Usuario que creo el detalle bancario del proveedor.';
COMMENT ON COLUMN public.prv_proveedor_cuenta_bancaria.usuario_modifica IS 'Usuario que realizo la ultima modificacion del detalle bancario.';
