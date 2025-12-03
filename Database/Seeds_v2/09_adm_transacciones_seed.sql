-- ================================================
-- 09_adm_transacciones_seed.sql
-- Seeds demo para Administración transaccional:
--   * Resúmenes y movimientos CxC/CxP
--   * Cálculo de interés moratorio y ajuste fiscal
--   * Bitácora de operaciones administrativas
-- Requiere: seeds 01-08 y DDL 11_administracion_transacciones.sql.
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_cliente_res       bigint;
    v_cliente_emp       bigint;
    v_proveedor_mat     bigint;
    v_factura_cli1      bigint;
    v_factura_cli2      bigint;
    v_cobro_cli1        bigint;
    v_nota_cli1         bigint;
    v_factura_prov      bigint;
    v_pago_prov         bigint;
    v_cxc_cli1          bigint;
    v_cxc_cli2          bigint;
    v_cxp_prov          bigint;
    v_oper_ven          bigint;
    v_oper_com          bigint;
    v_oper_ban          bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe compañía SIAD-DEMO. Ejecuta seeds anteriores antes del módulo administrativo.';
    END IF;

    SELECT cliente_id
      INTO v_cliente_res
      FROM public.adm_cliente
     WHERE company_id = v_company_id
       AND code = 'CLI-DEMO-001'
     LIMIT 1;

    SELECT cliente_id
      INTO v_cliente_emp
      FROM public.adm_cliente
     WHERE company_id = v_company_id
       AND code = 'CLI-DEMO-002'
     LIMIT 1;

    SELECT proveedor_id
      INTO v_proveedor_mat
      FROM public.adm_proveedor
     WHERE company_id = v_company_id
       AND code = 'PROV-TUB'
     LIMIT 1;

    SELECT factura_id
      INTO v_factura_cli1
      FROM public.ven_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FV-2025-0001'
     LIMIT 1;

    SELECT factura_id
      INTO v_factura_cli2
      FROM public.ven_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FV-2025-0002'
     LIMIT 1;

    SELECT nota_id
      INTO v_nota_cli1
      FROM public.ven_nota
     WHERE factura_id = v_factura_cli1
       AND numero_documento = 'NC-2025-0001'
     LIMIT 1;

    SELECT cobro_id
      INTO v_cobro_cli1
      FROM public.ven_cobro
     WHERE company_id = v_company_id
       AND numero_recibo = 'RC-2025-0001'
     LIMIT 1;

    SELECT factura_id
      INTO v_factura_prov
      FROM public.com_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FACPROV-2025-0001'
     LIMIT 1;

    SELECT pago_id
      INTO v_pago_prov
      FROM public.com_pago
     WHERE company_id = v_company_id
       AND numero_pago = 'PAG-2025-0001'
     LIMIT 1;

    -- Resumen CxC clientes
    INSERT INTO public.adm_cxc_resumen (
        company_id, cliente_id, saldo_inicial, cargos, abonos, saldo_actual)
    VALUES (v_company_id, v_cliente_res, 0, 287.50, 287.50, 0)
    ON CONFLICT (company_id, cliente_id) DO UPDATE
        SET saldo_inicial = EXCLUDED.saldo_inicial,
            cargos = EXCLUDED.cargos,
            abonos = EXCLUDED.abonos,
            saldo_actual = EXCLUDED.saldo_actual,
            ultima_actualizacion = now()
    RETURNING cxc_id INTO v_cxc_cli1;

    INSERT INTO public.adm_cxc_resumen (
        company_id, cliente_id, saldo_inicial, cargos, abonos, saldo_actual)
    VALUES (v_company_id, v_cliente_emp, 0, 977.50, 0, 977.50)
    ON CONFLICT (company_id, cliente_id) DO UPDATE
        SET saldo_inicial = EXCLUDED.saldo_inicial,
            cargos = EXCLUDED.cargos,
            abonos = EXCLUDED.abonos,
            saldo_actual = EXCLUDED.saldo_actual,
            ultima_actualizacion = now();

    SELECT cxc_id INTO v_cxc_cli2
      FROM public.adm_cxc_resumen
     WHERE company_id = v_company_id
       AND cliente_id = v_cliente_emp
     LIMIT 1;

    -- Movimientos CxC para cliente residencial
    IF v_cxc_cli1 IS NULL THEN
        SELECT cxc_id INTO v_cxc_cli1
          FROM public.adm_cxc_resumen
         WHERE company_id = v_company_id
           AND cliente_id = v_cliente_res
         LIMIT 1;
    END IF;

    IF v_cxc_cli1 IS NOT NULL THEN
        -- Factura
        IF NOT EXISTS (
            SELECT 1 FROM public.adm_cxc_movimiento
             WHERE cxc_id = v_cxc_cli1
               AND documento_tipo = 'FACTURA'
               AND documento_id = v_factura_cli1) THEN
            INSERT INTO public.adm_cxc_movimiento (
                cxc_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, cargo, saldo_posterior)
            VALUES (
                v_cxc_cli1, current_date - 7, 'FACTURA', v_factura_cli1,
                'Facturación FV-2025-0001', 287.50, 287.50);
        END IF;

        -- Nota crédito
        IF v_nota_cli1 IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM public.adm_cxc_movimiento
             WHERE cxc_id = v_cxc_cli1
               AND documento_tipo = 'NOTA_CREDITO'
               AND documento_id = v_nota_cli1) THEN
            INSERT INTO public.adm_cxc_movimiento (
                cxc_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, abono, saldo_posterior)
            VALUES (
                v_cxc_cli1, current_date - 2, 'NOTA_CREDITO', v_nota_cli1,
                'Ajuste reclamo lectura NC-2025-0001', 57.50, 230.00);
        END IF;

        -- Cobro
        IF v_cobro_cli1 IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM public.adm_cxc_movimiento
             WHERE cxc_id = v_cxc_cli1
               AND documento_tipo = 'COBRO'
               AND documento_id = v_cobro_cli1) THEN
            INSERT INTO public.adm_cxc_movimiento (
                cxc_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, abono, saldo_posterior)
            VALUES (
                v_cxc_cli1, current_date - 1, 'COBRO', v_cobro_cli1,
                'Cobro RC-2025-0001', 230.00, 0);
        END IF;
    END IF;

    -- Cliente empresarial (solo factura)
    IF v_cxc_cli2 IS NOT NULL AND v_factura_cli2 IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1 FROM public.adm_cxc_movimiento
             WHERE cxc_id = v_cxc_cli2
               AND documento_tipo = 'FACTURA'
               AND documento_id = v_factura_cli2) THEN
            INSERT INTO public.adm_cxc_movimiento (
                cxc_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, cargo, saldo_posterior)
            VALUES (
                v_cxc_cli2, current_date - 3, 'FACTURA', v_factura_cli2,
                'Facturación FV-2025-0002', 977.50, 977.50);
        END IF;
    END IF;

    -- Resumen CxP proveedor
    INSERT INTO public.adm_cxp_resumen (
        company_id, proveedor_id, saldo_inicial, cargos, abonos, saldo_actual)
    VALUES (
        v_company_id, v_proveedor_mat, 0, 10120.00, 10120.00, 0)
    ON CONFLICT (company_id, proveedor_id) DO UPDATE
        SET saldo_inicial = EXCLUDED.saldo_inicial,
            cargos = EXCLUDED.cargos,
            abonos = EXCLUDED.abonos,
            saldo_actual = EXCLUDED.saldo_actual,
            ultima_actualizacion = now()
    RETURNING cxp_id INTO v_cxp_prov;

    IF v_cxp_prov IS NOT NULL THEN
        -- Factura proveedor
        IF v_factura_prov IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM public.adm_cxp_movimiento
             WHERE cxp_id = v_cxp_prov
               AND documento_tipo = 'FACTURA'
               AND documento_id = v_factura_prov) THEN
            INSERT INTO public.adm_cxp_movimiento (
                cxp_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, cargo, saldo_posterior)
            VALUES (
                v_cxp_prov, current_date - 8, 'FACTURA', v_factura_prov,
                'Factura FACPROV-2025-0001', 10120.00, 10120.00);
        END IF;

        -- Pago proveedor
        IF v_pago_prov IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM public.adm_cxp_movimiento
             WHERE cxp_id = v_cxp_prov
               AND documento_tipo = 'PAGO'
               AND documento_id = v_pago_prov) THEN
            INSERT INTO public.adm_cxp_movimiento (
                cxp_id, fecha_movimiento, documento_tipo, documento_id,
                descripcion, abono, saldo_posterior)
            VALUES (
                v_cxp_prov, current_date - 2, 'PAGO', v_pago_prov,
                'Pago PAG-2025-0001', 10120.00, 0);
        END IF;
    END IF;

    -- Interés de mora para cliente empresarial
    IF v_cxc_cli2 IS NOT NULL AND v_factura_cli2 IS NOT NULL THEN
        INSERT INTO public.adm_interes_mora (
            cxc_id, factura_id, periodo, fecha_calculo,
            tasa_anual, dias_mora, monto_calculado, estado)
        VALUES (
            v_cxc_cli2, v_factura_cli2,
            to_char(current_date, 'YYYY-MM'),
            current_date, 0.2400, 15, 96.50, 'PENDING')
        ON CONFLICT (cxc_id, periodo, factura_id) DO UPDATE
            SET dias_mora = EXCLUDED.dias_mora,
                monto_calculado = EXCLUDED.monto_calculado,
                estado = EXCLUDED.estado;
    END IF;

    -- Ajuste fiscal demo
    INSERT INTO public.adm_ajuste_fiscal (
        company_id, tipo_ajuste, descripcion, documento_tipo,
        documento_id, monto, fecha_ajuste, estado)
    VALUES (
        v_company_id, 'ISV', 'Regularización ISV crédito fiscal',
        'FACPROV', v_factura_prov, 150.00, current_date - 1, 'APPLIED')
    ON CONFLICT DO NOTHING;

    -- Bitácora de operaciones
    SELECT operacion_id INTO v_oper_ven
      FROM public.adm_operacion
     WHERE company_id = v_company_id
       AND code = 'VEN_FACT'
     LIMIT 1;

    SELECT operacion_id INTO v_oper_com
      FROM public.adm_operacion
     WHERE company_id = v_company_id
       AND code = 'COM_ORD'
     LIMIT 1;

    SELECT operacion_id INTO v_oper_ban
      FROM public.adm_operacion
     WHERE company_id = v_company_id
       AND code = 'BAN_PAGO'
     LIMIT 1;

    INSERT INTO public.adm_operacion_log (
        operacion_id, company_id, usuario, modulo, entidad, entidad_id, descripcion)
    VALUES
        (v_oper_ven, v_company_id, 'sysadmin', 'VENTAS', 'ven_factura', 'FV-2025-0002', 'Factura empresarial registrada'),
        (v_oper_com, v_company_id, 'sysadmin', 'COMPRAS', 'com_orden', 'OC-2025-0001', 'Orden aprobada con entrega parcial'),
        (v_oper_ban, v_company_id, 'sysadmin', 'BANCOS', 'ban_movimiento', 'PAG-2025-0001', 'Pago a proveedor conciliado')
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'CxC, CxP e indicadores administrativos actualizados para compañía %', v_company_id;
END
$$;
