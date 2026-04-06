-- 2026-03-04_add_reglas_integracion_apc.sql
-- Reglas de integración contable para APC (ventas/agua)

BEGIN;

DO $$
DECLARE
    v_company_id       bigint;
    v_doc_fac          bigint;
    v_doc_rec          bigint;
    v_doc_nc           bigint;
    v_doc_nd           bigint;
    v_acc_cxc          bigint;
    v_acc_ingresos     bigint;
    v_acc_caja         bigint;
    v_acc_banco        bigint;
    v_acc_iva          bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'APC'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company APC no encontrada. Verifica cfg_company.code.';
    END IF;

    -- Tipos de documento (VENTAS)
    SELECT document_type_id INTO v_doc_fac
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'FAC';
    SELECT document_type_id INTO v_doc_rec
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'REC';
    SELECT document_type_id INTO v_doc_nc
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'NC';
    SELECT document_type_id INTO v_doc_nd
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'ND';

    IF v_doc_fac IS NULL OR v_doc_rec IS NULL OR v_doc_nc IS NULL OR v_doc_nd IS NULL THEN
        RAISE EXCEPTION 'Faltan tipos de documento VENTAS (FAC/REC/NC/ND).';
    END IF;

    -- Cuentas
    SELECT account_id INTO v_acc_cxc
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id AND code = '1.1.03.02';

    SELECT account_id INTO v_acc_ingresos
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id AND code = '5.1.01.02';

    SELECT account_id INTO v_acc_caja
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id AND code = '1.1.01.01.01';

    SELECT account_id INTO v_acc_banco
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id AND code = '1.1.01.02.01';

    SELECT account_id INTO v_acc_iva
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id AND code = '2.1.03.01';

    IF v_acc_cxc IS NULL OR v_acc_ingresos IS NULL OR v_acc_caja IS NULL OR v_acc_banco IS NULL THEN
        RAISE EXCEPTION 'Plan de cuentas incompleto para APC (CxC/Ingresos/Caja/Banco).';
    END IF;

    -- Reglas de integración
    INSERT INTO public.con_regla_integracion (
        company_id, module, document_type_id, scenario_code, description,
        debit_account_id, credit_account_id, cost_center_id, is_active, created_at, created_by)
    VALUES
        (v_company_id, 'VENTAS', v_doc_fac, 'FAC_NETO', 'CxC vs ingresos (neto)', v_acc_cxc, v_acc_ingresos, NULL, true, now(), current_user),
        (v_company_id, 'VENTAS', v_doc_fac, 'FAC_IVA',  'CxC vs IVA por pagar',  v_acc_cxc, v_acc_iva,      NULL, true, now(), current_user),
        (v_company_id, 'VENTAS', v_doc_rec, 'COB_CAJA', 'Cobro en caja',          v_acc_caja, v_acc_cxc,    NULL, true, now(), current_user),
        (v_company_id, 'VENTAS', v_doc_rec, 'COB_BANCO','Cobro en banco',         v_acc_banco, v_acc_cxc,   NULL, true, now(), current_user),
        (v_company_id, 'VENTAS', v_doc_nc,  'NC',       'Nota de crédito',        v_acc_ingresos, v_acc_cxc,NULL, true, now(), current_user),
        (v_company_id, 'VENTAS', v_doc_nd,  'ND',       'Nota de débito',         v_acc_cxc, v_acc_ingresos,NULL, true, now(), current_user)
    ON CONFLICT (company_id, module, document_type_id, scenario_code) DO UPDATE
        SET description = EXCLUDED.description,
            debit_account_id = EXCLUDED.debit_account_id,
            credit_account_id = EXCLUDED.credit_account_id,
            cost_center_id = EXCLUDED.cost_center_id,
            is_active = EXCLUDED.is_active,
            updated_at = now(),
            updated_by = current_user;
END
$$;

COMMIT;
