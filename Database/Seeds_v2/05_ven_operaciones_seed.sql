-- ================================================
-- 05_ven_operaciones_seed.sql
-- Seeds demo para módulo de Ventas:
--   * Facturas, líneas e integración básica
--   * Nota de crédito aplicada a una factura
--   * Recibo/cobro con detalle contra factura
-- Requiere: ejecutar previamente 01_cfg_configuracion_seed.sql,
--           02_con_contabilidad_seed.sql,
--           03_adm_security_seed.sql,
--           04_adm_maestros_seed.sql y DDL 04_ventas_core.sql.
-- ================================================

DO $$
DECLARE
    v_company_id      bigint;
    v_branch_id       bigint;
    v_cliente_res     bigint;
    v_cliente_emp     bigint;
    v_servicio_agua   bigint;
    v_tax_isv15       bigint;
    v_doc_factura     bigint;
    v_doc_recibo      bigint;
    v_doc_nota        bigint;
    v_series_factura  bigint;
    v_series_recibo   bigint;
    v_factura1_id     bigint;
    v_factura2_id     bigint;
    v_cobro1_id       bigint;
    v_nota1_id        bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe compañía SIAD-DEMO. Ejecuta 01_cfg_configuracion_seed.sql antes de este seed.';
    END IF;

    SELECT branch_id
      INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

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

    SELECT servicio_id, impuesto_id
      INTO v_servicio_agua, v_tax_isv15
      FROM public.adm_servicio
     WHERE company_id = v_company_id
       AND code = 'SRV-AGUA'
     LIMIT 1;

    IF v_cliente_res IS NULL OR v_servicio_agua IS NULL THEN
        RAISE EXCEPTION 'Clientes o servicios demo no existen. Ejecuta 04_adm_maestros_seed.sql primero.';
    END IF;

    SELECT document_type_id
      INTO v_doc_factura
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'FAC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_recibo
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'REC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_nota
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND code = 'NC'
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

    IF v_doc_factura IS NULL OR v_doc_recibo IS NULL OR v_doc_nota IS NULL THEN
        RAISE EXCEPTION 'Faltan tipos de documento FAC/REC/NC. Verifica seed 01_cfg_configuracion_seed.sql.';
    END IF;

    -- =========================================================
    -- Factura 1: Cliente Residencial (Consumo Agua)
    -- =========================================================
    INSERT INTO public.ven_factura (
        company_id, branch_id, cliente_id, document_type_id, currency_code,
        exchange_rate, document_series_id, numero_documento, numero_fiscal,
        fecha_emision, fecha_vencimiento,
        cliente_nombre, cliente_tax_id,
        subtotal, subtotal_gravado, impuesto_isv, impuesto_total, total,
        saldo_actual, estatus, referencia, observaciones)
    VALUES (
        v_company_id, v_branch_id, v_cliente_res, v_doc_factura, 'HNL',
        1, v_series_factura, 'FV-2025-0001', 'FV-001-00000001',
        current_date - 7, current_date + 23,
        'Familia Andrade', '08011999000123',
        250.00, 250.00, 37.50, 37.50, 287.50,
        287.50, 'PENDING', 'Periodo 2025-10', 'Consumo estimado mensual')
    ON CONFLICT (company_id, numero_documento) DO UPDATE
        SET cliente_id = EXCLUDED.cliente_id,
            branch_id = EXCLUDED.branch_id,
            fecha_emision = EXCLUDED.fecha_emision,
            fecha_vencimiento = EXCLUDED.fecha_vencimiento,
            subtotal = EXCLUDED.subtotal,
            subtotal_gravado = EXCLUDED.subtotal_gravado,
            impuesto_isv = EXCLUDED.impuesto_isv,
            impuesto_total = EXCLUDED.impuesto_total,
            total = EXCLUDED.total,
            saldo_actual = EXCLUDED.saldo_actual,
            estatus = EXCLUDED.estatus,
            referencia = EXCLUDED.referencia,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING factura_id INTO v_factura1_id;

    INSERT INTO public.ven_factura_linea (
        factura_id, line_number, producto_codigo, descripcion, item_tipo,
        cantidad, precio_unitario, descuento, base_imponible,
        impuesto_monto, impuesto_id, tratamiento_impuesto, total_linea)
    VALUES (
        v_factura1_id, 1, 'SRV-AGUA', 'Consumo Agua Mensual 10 m3', 'SERVICIO',
        10, 25.00, 0, 250.00, 37.50, v_tax_isv15, 'GRAVADO', 287.50)
    ON CONFLICT (factura_id, line_number) DO UPDATE
        SET producto_codigo = EXCLUDED.producto_codigo,
            descripcion = EXCLUDED.descripcion,
            item_tipo = EXCLUDED.item_tipo,
            cantidad = EXCLUDED.cantidad,
            precio_unitario = EXCLUDED.precio_unitario,
            descuento = EXCLUDED.descuento,
            base_imponible = EXCLUDED.base_imponible,
            impuesto_monto = EXCLUDED.impuesto_monto,
            impuesto_id = EXCLUDED.impuesto_id,
            tratamiento_impuesto = EXCLUDED.tratamiento_impuesto,
            total_linea = EXCLUDED.total_linea;

    -- =========================================================
    -- Factura 2: Cliente Empresarial (Consumo + Reconexión)
    -- =========================================================
    INSERT INTO public.ven_factura (
        company_id, branch_id, cliente_id, document_type_id, currency_code,
        exchange_rate, document_series_id, numero_documento, numero_fiscal,
        fecha_emision, fecha_vencimiento,
        cliente_nombre, cliente_tax_id,
        subtotal, subtotal_gravado, impuesto_isv, impuesto_total, total,
        saldo_actual, estatus, referencia, observaciones)
    VALUES (
        v_company_id, v_branch_id, v_cliente_emp, v_doc_factura, 'HNL',
        1, v_series_factura, 'FV-2025-0002', 'FV-001-00000002',
        current_date - 3, current_date + 27,
        'Parque Industrial Norte', '08011999000124',
        850.00, 850.00, 127.50, 127.50, 977.50,
        977.50, 'PENDING', 'Servicio empresarial', 'Incluye reconexión')
    ON CONFLICT (company_id, numero_documento) DO UPDATE
        SET cliente_id = EXCLUDED.cliente_id,
            branch_id = EXCLUDED.branch_id,
            fecha_emision = EXCLUDED.fecha_emision,
            fecha_vencimiento = EXCLUDED.fecha_vencimiento,
            subtotal = EXCLUDED.subtotal,
            subtotal_gravado = EXCLUDED.subtotal_gravado,
            impuesto_isv = EXCLUDED.impuesto_isv,
            impuesto_total = EXCLUDED.impuesto_total,
            total = EXCLUDED.total,
            saldo_actual = EXCLUDED.saldo_actual,
            estatus = EXCLUDED.estatus,
            referencia = EXCLUDED.referencia,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING factura_id INTO v_factura2_id;

    INSERT INTO public.ven_factura_linea (
        factura_id, line_number, producto_codigo, descripcion, item_tipo,
        cantidad, precio_unitario, descuento, base_imponible,
        impuesto_monto, impuesto_id, tratamiento_impuesto, total_linea)
    VALUES
        (v_factura2_id, 1, 'SRV-AGUA', 'Consumo Agua Empresarial 20 m3', 'SERVICIO',
         20, 25.00, 0, 500.00, 75.00, v_tax_isv15, 'GRAVADO', 575.00),
        (v_factura2_id, 2, 'SRV-RECON', 'Reconexión de servicio', 'SERVICIO',
         1, 350.00, 0, 350.00, 52.50, v_tax_isv15, 'GRAVADO', 402.50)
    ON CONFLICT (factura_id, line_number) DO UPDATE
        SET producto_codigo = EXCLUDED.producto_codigo,
            descripcion = EXCLUDED.descripcion,
            item_tipo = EXCLUDED.item_tipo,
            cantidad = EXCLUDED.cantidad,
            precio_unitario = EXCLUDED.precio_unitario,
            descuento = EXCLUDED.descuento,
            base_imponible = EXCLUDED.base_imponible,
            impuesto_monto = EXCLUDED.impuesto_monto,
            impuesto_id = EXCLUDED.impuesto_id,
            tratamiento_impuesto = EXCLUDED.tratamiento_impuesto,
            total_linea = EXCLUDED.total_linea;

    -- =========================================================
    -- Nota de crédito parcial sobre la Factura 1
    -- =========================================================
    INSERT INTO public.ven_nota (
        factura_id, tipo, document_type_id, numero_documento, numero_fiscal,
        fecha_emision, motivo, subtotal, impuesto_total, monto, saldo_afectado)
    VALUES (
        v_factura1_id, 'CREDITO', v_doc_nota, 'NC-2025-0001', 'NC-001-00000001',
        current_date - 2, 'Ajuste por reclamo de lectura', 50.00, 7.50, 57.50, 57.50)
    ON CONFLICT (factura_id, numero_documento) DO UPDATE
        SET motivo = EXCLUDED.motivo,
            subtotal = EXCLUDED.subtotal,
            impuesto_total = EXCLUDED.impuesto_total,
            monto = EXCLUDED.monto,
            saldo_afectado = EXCLUDED.saldo_afectado,
            fecha_emision = EXCLUDED.fecha_emision,
            updated_at = now(),
            updated_by = current_user
    RETURNING nota_id INTO v_nota1_id;

    -- Ajustar saldo de factura 1 si corresponde
    UPDATE public.ven_factura
       SET saldo_actual = GREATEST(total - 57.50, 0),
           updated_at = now(),
           updated_by = current_user
     WHERE factura_id = v_factura1_id;

    -- =========================================================
    -- Cobro parcial aplicado a la Factura 1
    -- =========================================================
    INSERT INTO public.ven_cobro (
        company_id, cliente_id, document_type_id, document_series_id,
        numero_recibo, fecha_cobro, currency_code, exchange_rate,
        monto_cobrado, monto_retenciones, metodo, referencia_bancaria,
        observaciones, estado)
    VALUES (
        v_company_id, v_cliente_res, v_doc_recibo, v_series_recibo,
        'RC-2025-0001', current_date - 1, 'HNL', 1,
        230.00, 0, 'TRANSFERENCIA', 'ACH-785421', 'Pago parcial convenio', 'POSTED')
    ON CONFLICT (company_id, numero_recibo) DO UPDATE
        SET cliente_id = EXCLUDED.cliente_id,
            fecha_cobro = EXCLUDED.fecha_cobro,
            monto_cobrado = EXCLUDED.monto_cobrado,
            metodo = EXCLUDED.metodo,
            referencia_bancaria = EXCLUDED.referencia_bancaria,
            observaciones = EXCLUDED.observaciones,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING cobro_id INTO v_cobro1_id;

    INSERT INTO public.ven_cobro_detalle (
        cobro_id, factura_id, monto_aplicado, monto_retencion, descripcion)
    VALUES (
        v_cobro1_id, v_factura1_id, 230.00, 0, 'Abono parcial recibo RC-2025-0001')
    ON CONFLICT (cobro_id, factura_id) DO UPDATE
        SET monto_aplicado = EXCLUDED.monto_aplicado,
            monto_retencion = EXCLUDED.monto_retencion,
            descripcion = EXCLUDED.descripcion;

    -- Ajustar saldo de factura 1 después del cobro
    UPDATE public.ven_factura
       SET saldo_actual = GREATEST(saldo_actual - 230.00, 0),
           estatus = CASE WHEN GREATEST(saldo_actual - 230.00, 0) <= 0 THEN 'PAID' ELSE estatus END,
           updated_at = now(),
           updated_by = current_user
     WHERE factura_id = v_factura1_id;

    RAISE NOTICE 'Facturas %, %, nota % y cobro % listos para compañía %',
        v_factura1_id, v_factura2_id, v_nota1_id, v_cobro1_id, v_company_id;
END
$$;
