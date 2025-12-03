-- ================================================
-- 10_qa_operaciones_avanzadas.sql
-- Escenarios adicionales para QA:
--   * Factura con retención de ISV e ingreso parcial
--   * Nota de débito sobre cliente empresarial
--   * Pago a proveedor con retención ISR
-- Requiere: seeds 01-09 ejecutados.
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_branch_id         bigint;
    v_cliente_emp       bigint;
    v_proveedor_srv     bigint;
    v_servicio_recon    bigint;
    v_tax_isv15         bigint;
    v_doc_factura       bigint;
    v_doc_nota          bigint;
    v_doc_recibo        bigint;
    v_doc_pag           bigint;
    v_series_factura    bigint;
    v_series_recibo     bigint;
    v_series_pag        bigint;
    v_factura_ret_id    bigint;
    v_factura_srv_id    bigint;
    v_factura_prov      bigint;
    v_nota_debito_id    bigint;
    v_cobro_ret_id      bigint;
    v_pago_ret_id       bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe SIAD-DEMO. Ejecuta seeds base primero.';
    END IF;

    SELECT branch_id
      INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

    SELECT cliente_id
      INTO v_cliente_emp
      FROM public.adm_cliente
     WHERE company_id = v_company_id
       AND code = 'CLI-DEMO-002'
     LIMIT 1;

    SELECT proveedor_id
      INTO v_proveedor_srv
      FROM public.adm_proveedor
     WHERE company_id = v_company_id
       AND code = 'PROV-SRV'
     LIMIT 1;

    SELECT servicio_id, impuesto_id
      INTO v_servicio_recon, v_tax_isv15
      FROM public.adm_servicio
     WHERE company_id = v_company_id
       AND code = 'SRV-RECON'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_factura
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'FAC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_nota
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'ND'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_recibo
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'REC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_pag
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND code = 'PAG'
     LIMIT 1;

    SELECT series_id
      INTO v_series_factura
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND prefix = 'SIAD-FV'
     LIMIT 1;

    SELECT series_id
      INTO v_series_recibo
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND prefix = 'SIAD-RC'
     LIMIT 1;

    SELECT series_id
      INTO v_series_pag
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND prefix = 'SIAD-PP'
     LIMIT 1;

    -- Buscar facturas de proveedores demo
    SELECT factura_id
      INTO v_factura_prov
      FROM public.com_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FACPROV-2025-0001'
     LIMIT 1;

    SELECT factura_id
      INTO v_factura_srv_id
      FROM public.com_factura
     WHERE company_id = v_company_id
       AND proveedor_id = v_proveedor_srv
     ORDER BY factura_id DESC
     LIMIT 1;

    -- Factura con retención ISV (cliente empresarial)
    INSERT INTO public.ven_factura (
        company_id, branch_id, cliente_id, document_type_id, currency_code,
        exchange_rate, document_series_id, numero_documento, numero_fiscal,
        fecha_emision, cliente_nombre, cliente_tax_id,
        subtotal, subtotal_gravado, impuesto_isv, impuesto_total,
        monto_retenciones, total, saldo_actual, estatus, referencia)
    VALUES (
        v_company_id, v_branch_id, v_cliente_emp, v_doc_factura, 'HNL',
        1, v_series_factura, 'FV-2025-0010', 'FV-001-00000010',
        current_date - 1, 'Parque Industrial Norte', '08011999000124',
        1500.00, 1500.00, 225.00, 225.00,
        45.00, 1725.00, 1680.00, 'PENDING', 'Contrato corporativo')
    ON CONFLICT (company_id, numero_documento) DO UPDATE
        SET subtotal = EXCLUDED.subtotal,
            impuesto_isv = EXCLUDED.impuesto_isv,
            monto_retenciones = EXCLUDED.monto_retenciones,
            total = EXCLUDED.total,
            saldo_actual = EXCLUDED.saldo_actual,
            estatus = EXCLUDED.estatus,
            referencia = EXCLUDED.referencia,
            updated_at = now(),
            updated_by = current_user
    RETURNING factura_id INTO v_factura_ret_id;

    INSERT INTO public.ven_factura_linea (
        factura_id, line_number, producto_codigo, descripcion, item_tipo,
        cantidad, precio_unitario, descuento, base_imponible,
        impuesto_monto, impuesto_id, tratamiento_impuesto, total_linea)
    VALUES (
        v_factura_ret_id, 1, 'SRV-RECON', 'Servicio reconexión corporativo', 'SERVICIO',
        3, 500.00, 0, 1500.00, 225.00, v_tax_isv15, 'GRAVADO', 1725.00)
    ON CONFLICT (factura_id, line_number) DO UPDATE
        SET base_imponible = EXCLUDED.base_imponible,
            impuesto_monto = EXCLUDED.impuesto_monto,
            total_linea = EXCLUDED.total_linea;

    -- Nota de débito (multa por atraso)
    INSERT INTO public.ven_nota (
        factura_id, tipo, document_type_id, numero_documento, numero_fiscal,
        fecha_emision, motivo, subtotal, impuesto_total, monto, saldo_afectado)
    VALUES (
        v_factura_ret_id, 'DEBITO', v_doc_nota, 'ND-2025-0001', 'ND-001-00000001',
        current_date, 'Recargo por reconexión urgente', 200.00, 30.00, 230.00, 230.00)
    ON CONFLICT (factura_id, numero_documento) DO UPDATE
        SET motivo = EXCLUDED.motivo,
            subtotal = EXCLUDED.subtotal,
            impuesto_total = EXCLUDED.impuesto_total,
            monto = EXCLUDED.monto,
            saldo_afectado = EXCLUDED.saldo_afectado,
            fecha_emision = EXCLUDED.fecha_emision,
            updated_at = now(),
            updated_by = current_user
    RETURNING nota_id INTO v_nota_debito_id;

    UPDATE public.ven_factura
       SET saldo_actual = saldo_actual + 230.00,
           total = total + 230.00,
           updated_at = now(),
           updated_by = current_user
     WHERE factura_id = v_factura_ret_id;

    -- Cobro donde el cliente retiene ISV (45.00)
    INSERT INTO public.ven_cobro (
        company_id, cliente_id, document_type_id, document_series_id,
        numero_recibo, fecha_cobro, currency_code, exchange_rate,
        banco_cuenta_id, monto_cobrado, monto_retenciones,
        metodo, referencia_bancaria, observaciones, estado)
    VALUES (
        v_company_id, v_cliente_emp, v_doc_recibo, v_series_recibo,
        'RC-2025-0010', current_date, 'HNL', 1,
        (SELECT banco_cuenta_id FROM public.ban_cuenta WHERE code = 'BAN-OPER' LIMIT 1),
        1885.00, 45.00, 'TRANSFERENCIA', 'ACH-900123',
        'Cobro con retención ISV', 'POSTED')
    ON CONFLICT (company_id, numero_recibo) DO UPDATE
        SET monto_cobrado = EXCLUDED.monto_cobrado,
            monto_retenciones = EXCLUDED.monto_retenciones,
            fecha_cobro = EXCLUDED.fecha_cobro,
            banco_cuenta_id = EXCLUDED.banco_cuenta_id,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING cobro_id INTO v_cobro_ret_id;

    INSERT INTO public.ven_cobro_detalle (
        cobro_id, factura_id, monto_aplicado, monto_retencion, descripcion)
    VALUES (
        v_cobro_ret_id, v_factura_ret_id, 1885.00, 45.00, 'Cobro RC-2025-0010 con retención ISV')
    ON CONFLICT (cobro_id, factura_id) DO UPDATE
        SET monto_aplicado = EXCLUDED.monto_aplicado,
            monto_retencion = EXCLUDED.monto_retencion,
            descripcion = EXCLUDED.descripcion;

    UPDATE public.ven_factura
       SET saldo_actual = 0,
           estatus = 'PAID',
           updated_at = now(),
           updated_by = current_user
     WHERE factura_id = v_factura_ret_id;

    IF v_factura_srv_id IS NULL AND v_factura_prov IS NULL THEN
        RAISE EXCEPTION 'No se encontró factura de proveedor para aplicar retención.';
    END IF;

    -- Pago con retención ISR (proveedor servicios)
    INSERT INTO public.com_pago (
        company_id, proveedor_id, document_type_id, document_series_id,
        numero_pago, fecha_pago, currency_code, exchange_rate,
        banco_cuenta_id, monto_pagado, monto_retenciones, metodo,
        referencia_bancaria, observaciones, estado)
    VALUES (
        v_company_id, v_proveedor_srv, v_doc_pag, v_series_pag,
        'PAG-2025-0002', current_date, 'HNL', 1,
        (SELECT banco_cuenta_id FROM public.ban_cuenta WHERE code = 'BAN-PAG' LIMIT 1),
        1500.00, 75.00, 'CHEQUE', 'CHK-3301',
        'Pago servicios con retención ISR 5%', 'POSTED')
    ON CONFLICT (company_id, numero_pago) DO UPDATE
        SET proveedor_id = EXCLUDED.proveedor_id,
            fecha_pago = EXCLUDED.fecha_pago,
            monto_pagado = EXCLUDED.monto_pagado,
            monto_retenciones = EXCLUDED.monto_retenciones,
            banco_cuenta_id = EXCLUDED.banco_cuenta_id,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING pago_id INTO v_pago_ret_id;

    INSERT INTO public.com_pago_detalle (
        pago_id, factura_id, monto_aplicado, monto_retencion,
        retencion_tax_id, descripcion)
    VALUES (
        v_pago_ret_id, COALESCE(v_factura_srv_id, v_factura_prov), 1425.00, 75.00,
        v_tax_isv15, 'Pago con retención ISR demo')
    ON CONFLICT (pago_id, factura_id) DO UPDATE
        SET monto_aplicado = EXCLUDED.monto_aplicado,
            monto_retencion = EXCLUDED.monto_retencion,
            retencion_tax_id = EXCLUDED.retencion_tax_id,
            descripcion = EXCLUDED.descripcion;

    RAISE NOTICE 'Escenarios QA (retenciones y notas) generados para compañía %', v_company_id;
END
$$;
