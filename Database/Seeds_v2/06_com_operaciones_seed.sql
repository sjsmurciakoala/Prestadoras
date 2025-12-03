-- ================================================
-- 06_com_operaciones_seed.sql
-- Seeds demo para el módulo de Compras:
--   * Orden de compra con líneas
--   * Factura de proveedor referenciada al pedido
--   * Pago con aplicación y retenciones en cero (demo)
-- Requiere: 01_cfg_configuracion_seed.sql,
--           02_con_contabilidad_seed.sql,
--           03_adm_security_seed.sql,
--           04_adm_maestros_seed.sql,
--           DDL 05_compras_core.sql.
-- ================================================

DO $$
DECLARE
    v_company_id       bigint;
    v_branch_id        bigint;
    v_proveedor_mat    bigint;
    v_proveedor_srv    bigint;
    v_doc_oc           bigint;
    v_doc_fac          bigint;
    v_doc_pag          bigint;
    v_series_oc        bigint;
    v_series_fac       bigint;
    v_series_pag       bigint;
    v_tax_isv15        bigint;
    v_orden_id         bigint;
    v_factura_id       bigint;
    v_pago_id          bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe compañía SIAD-DEMO. Ejecuta seeds de configuración antes de Compras.';
    END IF;

    SELECT branch_id
      INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

    SELECT proveedor_id
      INTO v_proveedor_mat
      FROM public.adm_proveedor
     WHERE company_id = v_company_id
       AND code = 'PROV-TUB'
     LIMIT 1;

    SELECT proveedor_id
      INTO v_proveedor_srv
      FROM public.adm_proveedor
     WHERE company_id = v_company_id
       AND code = 'PROV-SRV'
     LIMIT 1;

    IF v_proveedor_mat IS NULL OR v_proveedor_srv IS NULL THEN
        RAISE EXCEPTION 'Proveedores demo no existen. Ejecuta 04_adm_maestros_seed.sql primero.';
    END IF;

    SELECT tax_id
      INTO v_tax_isv15
      FROM public.cfg_tax
     WHERE company_id = v_company_id
       AND name = 'ISV 15%'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_oc
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND code = 'OC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_fac
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND code = 'FAC'
     LIMIT 1;

    SELECT document_type_id
      INTO v_doc_pag
      FROM public.cfg_document_type
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND code = 'PAG'
     LIMIT 1;

    SELECT series_id
      INTO v_series_oc
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND prefix = 'SIAD-OC'
     LIMIT 1;

    SELECT series_id
      INTO v_series_fac
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND prefix = 'SIAD-FC'
     LIMIT 1;

    SELECT series_id
      INTO v_series_pag
      FROM public.cfg_document_series
     WHERE company_id = v_company_id
       AND module = 'COMPRAS'
       AND prefix = 'SIAD-PP'
     LIMIT 1;

    IF v_doc_oc IS NULL OR v_doc_fac IS NULL OR v_doc_pag IS NULL THEN
        RAISE EXCEPTION 'Faltan tipos de documento OC/FAC/PAG. Valida 01_cfg_configuracion_seed.sql.';
    END IF;

    -- =========================================================
    -- Orden de compra (tuberías y servicios)
    -- =========================================================
    INSERT INTO public.com_orden (
        company_id, branch_id, proveedor_id, numero_orden,
        fecha_orden, fecha_entrega, currency_code, exchange_rate,
        estado, subtotal, impuesto_total, total, observaciones)
    VALUES (
        v_company_id, v_branch_id, v_proveedor_mat, 'OC-2025-0001',
        current_date - 10, current_date + 5, 'HNL', 1,
        'APPROVED', 8800.00, 1320.00, 10120.00, 'Reabastecimiento de tubería')
    ON CONFLICT (company_id, numero_orden) DO UPDATE
        SET proveedor_id = EXCLUDED.proveedor_id,
            fecha_orden = EXCLUDED.fecha_orden,
            fecha_entrega = EXCLUDED.fecha_entrega,
            subtotal = EXCLUDED.subtotal,
            impuesto_total = EXCLUDED.impuesto_total,
            total = EXCLUDED.total,
            estado = EXCLUDED.estado,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING orden_id INTO v_orden_id;

    INSERT INTO public.com_orden_linea (
        orden_id, line_number, producto_codigo, descripcion,
        cantidad, costo_unitario, descuento, impuesto_monto, impuesto_id, total_linea)
    VALUES
        (v_orden_id, 1, 'MAT-TUB-50', 'Tubería PVC 1/2"', 200, 40.00, 0, 1200.00, v_tax_isv15, 9200.00),
        (v_orden_id, 2, 'SRV-RECON', 'Servicios de instalación camión cisterna', 10, 60.00, 0, 120.00, v_tax_isv15, 720.00)
    ON CONFLICT (orden_id, line_number) DO UPDATE
        SET producto_codigo = EXCLUDED.producto_codigo,
            descripcion = EXCLUDED.descripcion,
            cantidad = EXCLUDED.cantidad,
            costo_unitario = EXCLUDED.costo_unitario,
            descuento = EXCLUDED.descuento,
            impuesto_monto = EXCLUDED.impuesto_monto,
            impuesto_id = EXCLUDED.impuesto_id,
            total_linea = EXCLUDED.total_linea;

    -- =========================================================
    -- Factura del proveedor vinculada a la orden
    -- =========================================================
    INSERT INTO public.com_factura (
        company_id, proveedor_id, document_type_id, currency_code,
        exchange_rate, numero_documento, numero_fiscal,
        fecha_emision, fecha_vencimiento,
        proveedor_nombre, proveedor_tax_id,
        subtotal, subtotal_gravado, impuesto_isv, impuesto_total,
        total, saldo_actual, estado, orden_id, observaciones)
    VALUES (
        v_company_id, v_proveedor_mat, v_doc_fac, 'HNL',
        1, 'FACPROV-2025-0001', 'CF-001-00000021',
        current_date - 8, current_date + 22,
        'HidroMateriales S.A.', '08011998000011',
        8800.00, 8800.00, 1320.00, 1320.00,
        10120.00, 10120.00, 'PENDING', v_orden_id, 'Entrega parcial OC-2025-0001')
    ON CONFLICT (company_id, proveedor_id, numero_documento) DO UPDATE
        SET fecha_emision = EXCLUDED.fecha_emision,
            fecha_vencimiento = EXCLUDED.fecha_vencimiento,
            subtotal = EXCLUDED.subtotal,
            subtotal_gravado = EXCLUDED.subtotal_gravado,
            impuesto_isv = EXCLUDED.impuesto_isv,
            impuesto_total = EXCLUDED.impuesto_total,
            total = EXCLUDED.total,
            saldo_actual = EXCLUDED.saldo_actual,
            estado = EXCLUDED.estado,
            orden_id = EXCLUDED.orden_id,
            observaciones = EXCLUDED.observaciones,
            updated_at = now(),
            updated_by = current_user
    RETURNING factura_id INTO v_factura_id;

    INSERT INTO public.com_factura_linea (
        factura_id, line_number, producto_codigo, descripcion,
        cantidad, costo_unitario, descuento,
        base_imponible, impuesto_monto, impuesto_id, tratamiento_impuesto, total_linea)
    VALUES
        (v_factura_id, 1, 'MAT-TUB-50', 'Tubería PVC 1/2"', 200, 40.00, 0, 8000.00, 1200.00, v_tax_isv15, 'GRAVADO', 9200.00),
        (v_factura_id, 2, 'SRV-RECON', 'Servicios de instalación', 10, 60.00, 0, 600.00, 90.00, v_tax_isv15, 'GRAVADO', 690.00)
    ON CONFLICT (factura_id, line_number) DO UPDATE
        SET producto_codigo = EXCLUDED.producto_codigo,
            descripcion = EXCLUDED.descripcion,
            cantidad = EXCLUDED.cantidad,
            costo_unitario = EXCLUDED.costo_unitario,
            descuento = EXCLUDED.descuento,
            base_imponible = EXCLUDED.base_imponible,
            impuesto_monto = EXCLUDED.impuesto_monto,
            impuesto_id = EXCLUDED.impuesto_id,
            tratamiento_impuesto = EXCLUDED.tratamiento_impuesto,
            total_linea = EXCLUDED.total_linea;

    -- =========================================================
    -- Pago aplicado a la factura de proveedor
    -- =========================================================
    INSERT INTO public.com_pago (
        company_id, proveedor_id, document_type_id, document_series_id,
        numero_pago, fecha_pago, currency_code, exchange_rate,
        monto_pagado, monto_retenciones, metodo, referencia_bancaria,
        observaciones, estado)
    VALUES (
        v_company_id, v_proveedor_mat, v_doc_pag, v_series_pag,
        'PAG-2025-0001', current_date - 2, 'HNL', 1,
        10120.00, 0, 'TRANSFERENCIA', 'ACH-458712', 'Pago completo FACPROV-2025-0001', 'POSTED')
    ON CONFLICT (company_id, numero_pago) DO UPDATE
        SET proveedor_id = EXCLUDED.proveedor_id,
            fecha_pago = EXCLUDED.fecha_pago,
            monto_pagado = EXCLUDED.monto_pagado,
            metodo = EXCLUDED.metodo,
            referencia_bancaria = EXCLUDED.referencia_bancaria,
            observaciones = EXCLUDED.observaciones,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING pago_id INTO v_pago_id;

    INSERT INTO public.com_pago_detalle (
        pago_id, factura_id, monto_aplicado, monto_retencion, descripcion)
    VALUES (
        v_pago_id, v_factura_id, 10120.00, 0, 'Pago total OC-2025-0001')
    ON CONFLICT (pago_id, factura_id) DO UPDATE
        SET monto_aplicado = EXCLUDED.monto_aplicado,
            monto_retencion = EXCLUDED.monto_retencion,
            descripcion = EXCLUDED.descripcion;

    UPDATE public.com_factura
       SET saldo_actual = 0,
           estado = 'PAID',
           updated_at = now(),
           updated_by = current_user
     WHERE factura_id = v_factura_id;

    RAISE NOTICE 'OC %, factura % y pago % registrados para compañía %',
        v_orden_id, v_factura_id, v_pago_id, v_company_id;
END
$$;
