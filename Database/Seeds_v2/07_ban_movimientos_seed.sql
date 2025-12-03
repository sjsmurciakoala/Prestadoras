-- ================================================
-- 07_ban_movimientos_seed.sql
-- Seeds demo para módulo de Bancos:
--   * Cuentas bancarias base
--   * Movimientos enlazados a cobros/pagos existentes
--   * Actualiza referencias en ven_cobro y com_pago
-- Requiere: seeds 01-06 ejecutados y DDL 06_bancos_core.sql.
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_branch_id         bigint;
    v_cta_operativa_id  bigint;
    v_cta_egresos_id    bigint;
    v_cobro_id          bigint;
    v_cobro_monto       numeric(18,2);
    v_cobro_fecha       date;
    v_pago_id           bigint;
    v_pago_monto        numeric(18,2);
    v_pago_fecha        date;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe compañía SIAD-DEMO. Ejecuta seeds previos antes de Bancos.';
    END IF;

    SELECT branch_id
      INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

    INSERT INTO public.ban_cuenta (
        company_id, code, nombre, banco_nombre, branch_id, tipo,
        currency_code, numero_cuenta, saldo_inicial, fecha_saldo,
        estado, allow_reconciliation)
    VALUES (
        v_company_id, 'BAN-OPER', 'Cuenta Operativa Lempiras', 'Banco Atlántida',
        v_branch_id, 'CHEQUES', 'HNL', '001-000123-01', 25000.00, current_date - 30,
        'ACTIVE', true)
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            banco_nombre = EXCLUDED.banco_nombre,
            branch_id = EXCLUDED.branch_id,
            currency_code = EXCLUDED.currency_code,
            numero_cuenta = EXCLUDED.numero_cuenta,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING banco_cuenta_id INTO v_cta_operativa_id;

    INSERT INTO public.ban_cuenta (
        company_id, code, nombre, banco_nombre, branch_id, tipo,
        currency_code, numero_cuenta, saldo_inicial, fecha_saldo,
        estado, allow_reconciliation)
    VALUES (
        v_company_id, 'BAN-PAG', 'Cuenta Pagos Proveedores', 'Banco Ficohsa',
        v_branch_id, 'CHEQUES', 'HNL', '002-000789-05', 18000.00, current_date - 30,
        'ACTIVE', true)
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            banco_nombre = EXCLUDED.banco_nombre,
            branch_id = EXCLUDED.branch_id,
            currency_code = EXCLUDED.currency_code,
            numero_cuenta = EXCLUDED.numero_cuenta,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING banco_cuenta_id INTO v_cta_egresos_id;

    -- Cobro de ventas existente
    SELECT cobro_id, monto_cobrado, fecha_cobro
      INTO v_cobro_id, v_cobro_monto, v_cobro_fecha
      FROM public.ven_cobro
     WHERE company_id = v_company_id
       AND numero_recibo = 'RC-2025-0001'
     LIMIT 1;

    IF v_cobro_id IS NOT NULL THEN
        UPDATE public.ven_cobro
           SET banco_cuenta_id = v_cta_operativa_id
         WHERE cobro_id = v_cobro_id;

        INSERT INTO public.ban_movimiento (
            company_id, banco_cuenta_id, tipo, fecha_movimiento,
            currency_code, exchange_rate, monto, monto_local,
            descripcion, referencia, origen_modulo, origen_documento_id, estado)
        SELECT
            v_company_id,
            v_cta_operativa_id,
            'INGRESO',
            v_cobro_fecha,
            'HNL',
            1,
            v_cobro_monto,
            v_cobro_monto,
            'Cobro RC-2025-0001 clientes residenciales',
            'RC-2025-0001',
            'VENTAS',
            v_cobro_id,
            'POSTED'
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ban_movimiento
             WHERE company_id = v_company_id
               AND origen_modulo = 'VENTAS'
               AND origen_documento_id = v_cobro_id);
    END IF;

    -- Pago de compras existente
    SELECT pago_id, monto_pagado, fecha_pago
      INTO v_pago_id, v_pago_monto, v_pago_fecha
      FROM public.com_pago
     WHERE company_id = v_company_id
       AND numero_pago = 'PAG-2025-0001'
     LIMIT 1;

    IF v_pago_id IS NOT NULL THEN
        UPDATE public.com_pago
           SET banco_cuenta_id = v_cta_egresos_id
         WHERE pago_id = v_pago_id;

        INSERT INTO public.ban_movimiento (
            company_id, banco_cuenta_id, tipo, fecha_movimiento,
            currency_code, exchange_rate, monto, monto_local,
            descripcion, referencia, origen_modulo, origen_documento_id, estado)
        SELECT
            v_company_id,
            v_cta_egresos_id,
            'EGRESO',
            v_pago_fecha,
            'HNL',
            1,
            v_pago_monto,
            v_pago_monto,
            'Pago proveedores PAG-2025-0001',
            'PAG-2025-0001',
            'COMPRAS',
            v_pago_id,
            'POSTED'
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ban_movimiento
             WHERE company_id = v_company_id
               AND origen_modulo = 'COMPRAS'
               AND origen_documento_id = v_pago_id);
    END IF;

    RAISE NOTICE 'Cuentas bancarias y movimientos demo actualizados para compañía %', v_company_id;
END
$$;
