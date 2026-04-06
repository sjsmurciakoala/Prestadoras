-- =============================================================================
-- Seed: Document type VENTAS/MIS + plantilla contable para miscelaneos
-- Fecha: 2026-03-14
-- Branch: feature/facturacion-miscelaneos-ui-contabilidad-auto
-- Prerequisitos:
--   - Ejecutar 20260314_miscelaneos_catalogo_cont_account.sql
--   - Ejecutar 20260314_con_plantilla_partida_dtl_detail_expand.sql
--   - Actualizar sp_con_generar_comprobante (DETAIL_EXPAND)
-- =============================================================================

DO $$
DECLARE
    v_company_id bigint;
    v_type_id bigint;
    v_template_id bigint;
    v_cxc_account_id bigint;
BEGIN
    -- Resolver empresa activa (primera empresa, ajustar segun entorno)
    SELECT company_id INTO v_company_id
      FROM public.cfg_company
     WHERE upper(status) = 'ACTIVE'
     ORDER BY company_id
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No hay empresa activa configurada.';
    END IF;

    -- 1. Crear cfg_document_type VENTAS/MIS
    INSERT INTO public.cfg_document_type (
        company_id, module, code, name, description, requires_cai, is_active, created_at, created_by
    ) VALUES (
        v_company_id, 'VENTAS', 'MIS',
        'Recibo miscelaneo',
        'Recibo por conceptos miscelaneos (reconexion, reposicion, etc.)',
        false, true, now(), 'system'
    )
    ON CONFLICT (company_id, module, code) DO UPDATE
       SET name = EXCLUDED.name,
           description = EXCLUDED.description,
           is_active = true,
           updated_at = now(),
           updated_by = 'system';

    -- 2. Resolver type_id contable (FAC = Facturacion)
    SELECT type_id INTO v_type_id
      FROM public.con_tipo_transaccion
     WHERE company_id = v_company_id
       AND code = 'FAC'
     LIMIT 1;

    IF v_type_id IS NULL THEN
        RAISE EXCEPTION 'No existe con_tipo_transaccion FAC para empresa %. Cree el tipo contable primero.', v_company_id;
    END IF;

    -- 3. Resolver cuenta por cobrar (buscar cuenta tipo ACTIVO con palabra clave 'cobrar')
    --    NOTA: Ajustar este criterio segun el plan de cuentas real
    SELECT account_id INTO v_cxc_account_id
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id
       AND allows_posting = true
       AND lower(name) LIKE '%cobrar%'
       AND account_type = 'ACTIVO'
     ORDER BY account_id
     LIMIT 1;

    IF v_cxc_account_id IS NULL THEN
        -- Fallback: buscar primera cuenta de activo que permita posteo
        SELECT account_id INTO v_cxc_account_id
          FROM public.con_plan_cuentas
         WHERE company_id = v_company_id
           AND allows_posting = true
           AND account_type = 'ACTIVO'
         ORDER BY code
         LIMIT 1;
    END IF;

    IF v_cxc_account_id IS NULL THEN
        RAISE EXCEPTION 'No se encontro cuenta de activo (cuentas por cobrar) para empresa %. Configure el plan de cuentas.', v_company_id;
    END IF;

    -- 4. Crear plantilla contable VENTAS/MIS
    INSERT INTO public.con_plantilla_partida_hdr (
        company_id, module, document_type, name, description,
        is_active, created_at, created_by
    ) VALUES (
        v_company_id, 'VENTAS', 'MIS',
        'Plantilla Recibo Miscelaneo',
        'Debito CxC por total, credito por cada concepto miscelaneo',
        true, now(), 'system'
    )
    ON CONFLICT DO NOTHING;

    SELECT template_id INTO v_template_id
      FROM public.con_plantilla_partida_hdr
     WHERE company_id = v_company_id
       AND module = 'VENTAS'
       AND document_type = 'MIS'
       AND is_active = true
     ORDER BY template_id DESC
     LIMIT 1;

    IF v_template_id IS NULL THEN
        RAISE EXCEPTION 'No se pudo crear/encontrar plantilla VENTAS/MIS para empresa %.', v_company_id;
    END IF;

    -- 5. Linea 1: FIXED - Debito CxC por el total
    INSERT INTO public.con_plantilla_partida_dtl (
        company_id, template_id, line_number, account_id,
        debit_formula, credit_formula, description,
        line_mode, entry_side
    ) VALUES (
        v_company_id, v_template_id, 1, v_cxc_account_id,
        '{total}', NULL, 'Cuentas por cobrar - Miscelaneos',
        'FIXED', NULL
    )
    ON CONFLICT DO NOTHING;

    -- 6. Linea 2: DETAIL_EXPAND - Credito por cada concepto
    INSERT INTO public.con_plantilla_partida_dtl (
        company_id, template_id, line_number, account_id,
        debit_formula, credit_formula, description,
        line_mode, entry_side,
        detail_account_field, detail_amount_field, detail_description_field
    ) VALUES (
        v_company_id, v_template_id, 2, NULL,
        NULL, NULL, 'Ingreso miscelaneo por concepto',
        'DETAIL_EXPAND', 'C',
        'account_id', 'total', 'description'
    )
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'Seed VENTAS/MIS completado para empresa %. Template ID: %, CxC Account: %, Type ID: %',
        v_company_id, v_template_id, v_cxc_account_id, v_type_id;
END;
$$;


