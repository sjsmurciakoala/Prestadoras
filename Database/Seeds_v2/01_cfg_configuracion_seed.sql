-- ================================================
-- 01_cfg_configuracion_seed.sql
-- Seeds demo para el módulo de Configuración (empresa, sucursal, monedas,
-- impuestos y documentos) alineados con la estructura v2.
-- Requiere: Database/ddl_v2/01_configuracion_base.sql ejecutado previamente.
-- ================================================

DO $$
DECLARE
    v_company_id      bigint;
    v_branch_id       bigint;
    v_doc_factura     bigint;
    v_doc_nota        bigint;
    v_doc_nota_deb    bigint;
    v_doc_recibo      bigint;
    v_doc_oc          bigint;
    v_doc_fact_c      bigint;
    v_doc_pago_prov   bigint;
    v_doc_transfer    bigint;
BEGIN
    -- Monedas base (Lempira como moneda principal y USD de referencia)
    INSERT INTO public.cfg_currency (currency_code, name, symbol, decimal_places, is_base_currency, status)
    VALUES
        ('HNL', 'Lempira Hondureño', 'L', 2, true, 'ACTIVE'),
        ('USD', 'US Dollar', '$', 2, false, 'ACTIVE')
    ON CONFLICT (currency_code) DO UPDATE
        SET name = EXCLUDED.name,
            symbol = EXCLUDED.symbol,
            decimal_places = EXCLUDED.decimal_places,
            is_base_currency = EXCLUDED.is_base_currency,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user;

    -- Asegurar que únicamente HNL quede marcada como base
    UPDATE public.cfg_currency
       SET is_base_currency = (currency_code = 'HNL')
     WHERE currency_code IN ('HNL', 'USD');

    -- Empresa demo
    INSERT INTO public.cfg_company (
        code, commercial_name, legal_name, tax_id, email, phone, address,
        country_code, currency_code, timezone, status)
    VALUES (
        'SIAD-DEMO',
        'Servicios Integrales de Agua Demo',
        'Servicios Integrales de Agua Demo S.A.',
        '08011990123456',
        'info@siad-demo.com',
        '+504 2211-0000',
        'Col. Centroamérica, Tegucigalpa, Honduras',
        'HND',
        'HNL',
        'America/Tegucigalpa',
        'ACTIVE')
    ON CONFLICT (code) DO UPDATE
        SET commercial_name = EXCLUDED.commercial_name,
            legal_name = EXCLUDED.legal_name,
            tax_id = EXCLUDED.tax_id,
            email = EXCLUDED.email,
            phone = EXCLUDED.phone,
            address = EXCLUDED.address,
            country_code = EXCLUDED.country_code,
            currency_code = EXCLUDED.currency_code,
            timezone = EXCLUDED.timezone,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user
    RETURNING company_id INTO v_company_id;

    -- Sucursal principal
    INSERT INTO public.cfg_branch (
        company_id, code, name, address, phone, email, status)
    VALUES (
        v_company_id,
        'MATRIZ',
        'Sucursal Matriz',
        'Col. Centroamérica, Tegucigalpa, Honduras',
        '+504 2211-0001',
        'matriz@siad-demo.com',
        'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET name = EXCLUDED.name,
            address = EXCLUDED.address,
            phone = EXCLUDED.phone,
            email = EXCLUDED.email,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user
    RETURNING branch_id INTO v_branch_id;

    -- Impuestos base
    INSERT INTO public.cfg_tax (
        company_id, name, description, tax_type, rate, is_withholding, ledger_account_code, status)
    VALUES
        (v_company_id, 'ISV 15%', 'Impuesto sobre ventas 15%', 'VAT', 0.15, false, '4101-000-001', 'ACTIVE'),
        (v_company_id, 'ISV 18%', 'Impuesto sobre ventas 18% (servicios especiales)', 'VAT', 0.18, false, '4101-000-002', 'ACTIVE'),
        (v_company_id, 'Retención ISR 1%', 'Retención de ISR proveedores nacionales', 'WITHHOLDING', 0.01, true, '2102-000-001', 'ACTIVE')
    ON CONFLICT (company_id, name) DO UPDATE
        SET description = EXCLUDED.description,
            tax_type = EXCLUDED.tax_type,
            rate = EXCLUDED.rate,
            is_withholding = EXCLUDED.is_withholding,
            ledger_account_code = EXCLUDED.ledger_account_code,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user;

    -- Tipos de documento por módulo
    INSERT INTO public.cfg_document_type (company_id, module, code, name, description, requires_cai, is_active)
    VALUES
        (v_company_id, 'VENTAS',  'FAC', 'Factura Cliente', 'Factura fiscal de ventas', true,  true),
        (v_company_id, 'VENTAS',  'NC',  'Nota de Crédito', 'Devoluciones y ajustes ventas', true,  true),
        (v_company_id, 'VENTAS',  'ND',  'Nota de Débito', 'Cargos adicionales ventas', true,  true),
        (v_company_id, 'VENTAS',  'REC', 'Recibo de Caja', 'Cobro de clientes', false, true),
        (v_company_id, 'COMPRAS', 'OC',  'Orden de Compra', 'Ordenes a proveedores', false, true),
        (v_company_id, 'COMPRAS', 'FAC', 'Factura Proveedor', 'Registro de compras', false, true),
        (v_company_id, 'COMPRAS', 'PAG', 'Pago Proveedor', 'Egresos a proveedores', false, true),
        (v_company_id, 'BANCOS',  'TRF', 'Transferencia Bancaria', 'Movimientos internos bancos', false, true)
    ON CONFLICT (company_id, module, code) DO UPDATE
        SET name = EXCLUDED.name,
            description = EXCLUDED.description,
            requires_cai = EXCLUDED.requires_cai,
            is_active = EXCLUDED.is_active,
            updated_at = now(),
            updated_by = current_user;

    SELECT document_type_id INTO v_doc_factura
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'FAC';

    SELECT document_type_id INTO v_doc_nota
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'NC';

    SELECT document_type_id INTO v_doc_nota_deb
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'ND';

    SELECT document_type_id INTO v_doc_recibo
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'REC';

    SELECT document_type_id INTO v_doc_oc
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND code = 'OC';

    SELECT document_type_id INTO v_doc_fact_c
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND code = 'FAC';

    SELECT document_type_id INTO v_doc_pago_prov
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND code = 'PAG';

    SELECT document_type_id INTO v_doc_transfer
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'BANCOS' AND code = 'TRF';

    -- Series de documentos (consecutivos demo)
    INSERT INTO public.cfg_document_series (
        company_id, branch_id, module, document_type, document_type_id,
        prefix, next_number, min_number, max_number, expires_on,
        authorization_code, cai_number, status)
    VALUES
        (v_company_id, v_branch_id, 'VENTAS',  'FACTURA',      v_doc_factura,  'SIAD-FV', 1001, 1001, 1999, current_date + INTERVAL '1 year', 'AUTH-FV-2025', 'CAI-FV-001', 'ACTIVE'),
        (v_company_id, v_branch_id, 'VENTAS',  'NOTA_CREDITO', v_doc_nota,     'SIAD-NC', 5001, 5001, 5999, current_date + INTERVAL '1 year', 'AUTH-NC-2025', 'CAI-NC-001', 'ACTIVE'),
        (v_company_id, v_branch_id, 'VENTAS',  'NOTA_DEBITO',  v_doc_nota_deb,'SIAD-ND', 5501, 5501, 6499, current_date + INTERVAL '1 year', 'AUTH-ND-2025', 'CAI-ND-001', 'ACTIVE'),
        (v_company_id, v_branch_id, 'VENTAS',  'RECIBO',       v_doc_recibo,   'SIAD-RC', 3001, 3001, 3999, current_date + INTERVAL '1 year', NULL, NULL, 'ACTIVE'),
        (v_company_id, v_branch_id, 'COMPRAS', 'ORDEN',        v_doc_oc,       'SIAD-OC', 2001, 2001, 2999, current_date + INTERVAL '2 years', NULL, NULL, 'ACTIVE'),
        (v_company_id, v_branch_id, 'COMPRAS', 'FACTURA',      v_doc_fact_c,   'SIAD-FC', 4001, 4001, 4999, current_date + INTERVAL '1 year', NULL, NULL, 'ACTIVE'),
        (v_company_id, v_branch_id, 'COMPRAS', 'PAGO',         v_doc_pago_prov,'SIAD-PP', 6001, 6001, 6999, current_date + INTERVAL '1 year', NULL, NULL, 'ACTIVE'),
        (v_company_id, v_branch_id, 'BANCOS',  'TRANSFER',     v_doc_transfer, 'SIAD-TR', 7001, 7001, 7999, current_date + INTERVAL '1 year', NULL, NULL, 'ACTIVE')
    ON CONFLICT (company_id, module, document_type, prefix) DO UPDATE
        SET document_type_id = EXCLUDED.document_type_id,
            branch_id = EXCLUDED.branch_id,
            next_number = EXCLUDED.next_number,
            min_number = COALESCE(EXCLUDED.min_number, public.cfg_document_series.min_number),
            max_number = EXCLUDED.max_number,
            expires_on = EXCLUDED.expires_on,
            authorization_code = EXCLUDED.authorization_code,
            cai_number = EXCLUDED.cai_number,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user;

    RAISE NOTICE 'Seeds de configuración: compañía %, sucursal %', v_company_id, v_branch_id;
END
$$;
