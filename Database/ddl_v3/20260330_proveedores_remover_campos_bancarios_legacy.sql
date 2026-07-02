DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'prv_proveedor_cuenta_bancaria'
    ) THEN
        RAISE EXCEPTION 'No existe public.prv_proveedor_cuenta_bancaria. Cree primero la tabla detalle de cuentas bancarias.';
    END IF;
END $$;

ALTER TABLE public.prv_proveedores
    ADD COLUMN IF NOT EXISTS usuario_creo varchar(100),
    ADD COLUMN IF NOT EXISTS usuario_modifica varchar(100);

UPDATE public.prv_proveedores
SET usuario_creo = 'migration'
WHERE usuario_creo IS NULL
   OR NULLIF(btrim(usuario_creo), '') IS NULL;

ALTER TABLE public.prv_proveedores
    ALTER COLUMN usuario_creo SET NOT NULL;

ALTER TABLE public.prv_proveedor_cuenta_bancaria
    ADD COLUMN IF NOT EXISTS usuario_creo varchar(100),
    ADD COLUMN IF NOT EXISTS usuario_modifica varchar(100);

UPDATE public.prv_proveedor_cuenta_bancaria
SET usuario_creo = 'migration'
WHERE usuario_creo IS NULL
   OR NULLIF(btrim(usuario_creo), '') IS NULL;

ALTER TABLE public.prv_proveedor_cuenta_bancaria
    ALTER COLUMN usuario_creo SET NOT NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'prv_proveedores'
          AND column_name = 'cuenta_bancaria'
    ) THEN
        INSERT INTO public.prv_proveedor_cuenta_bancaria
            (cod_proveedor, banco, cuenta_bancaria, orden, fecha_creacion, usuario_creo)
        SELECT
            btrim(p.cod_proveedor),
            btrim(p.nombrebanco1),
            btrim(p.cuenta_bancaria),
            1,
            COALESCE(p.fecha_modificacion, p.fecha_creacion, now()),
            'migration'
        FROM public.prv_proveedores p
        WHERE NULLIF(btrim(p.cod_proveedor), '') IS NOT NULL
          AND NULLIF(btrim(p.nombrebanco1), '') IS NOT NULL
          AND NULLIF(btrim(p.cuenta_bancaria), '') IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM public.prv_proveedor_cuenta_bancaria d
              WHERE d.cod_proveedor = p.cod_proveedor
                AND d.orden = 1
          );

        INSERT INTO public.prv_proveedor_cuenta_bancaria
            (cod_proveedor, banco, cuenta_bancaria, orden, fecha_creacion, usuario_creo)
        SELECT
            btrim(p.cod_proveedor),
            btrim(p.nombrebanco2),
            btrim(p.cuenta_bancaria),
            2,
            COALESCE(p.fecha_modificacion, p.fecha_creacion, now()),
            'migration'
        FROM public.prv_proveedores p
        WHERE NULLIF(btrim(p.cod_proveedor), '') IS NOT NULL
          AND NULLIF(btrim(p.nombrebanco2), '') IS NOT NULL
          AND NULLIF(btrim(p.cuenta_bancaria), '') IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM public.prv_proveedor_cuenta_bancaria d
              WHERE d.cod_proveedor = p.cod_proveedor
                AND d.orden = 2
          );
    END IF;
END $$;

ALTER TABLE public.prv_proveedores
    DROP COLUMN IF EXISTS cuenta_bancaria,
    DROP COLUMN IF EXISTS nombrebanco1,
    DROP COLUMN IF EXISTS nombrebanco2;

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
COMMENT ON COLUMN public.prv_proveedores.compras_acum IS 'Monto acumulado de compras en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.compras_dolares IS 'Monto acumulado de compras en dolares.';
COMMENT ON COLUMN public.prv_proveedores.saldo_actual IS 'Saldo actual del proveedor en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.saldo_act_dolares IS 'Saldo actual del proveedor en dolares.';
COMMENT ON COLUMN public.prv_proveedores.saldo_anterior IS 'Saldo anterior del proveedor en moneda local.';
COMMENT ON COLUMN public.prv_proveedores.saldo_ant_doleres IS 'Saldo anterior del proveedor en dolares.';
COMMENT ON COLUMN public.prv_proveedores.razon_social IS 'Razon social del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.rtn IS 'RTN o identificacion fiscal del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.nombre_contacto IS 'Nombre de la persona de contacto.';
COMMENT ON COLUMN public.prv_proveedores.telefono IS 'Telefono principal de contacto.';
COMMENT ON COLUMN public.prv_proveedores.pagina_web IS 'Sitio web del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.fax IS 'Numero de fax del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.email IS 'Correo electronico principal del proveedor.';

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
