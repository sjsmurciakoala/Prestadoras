-- Seed rápido para Plan de Cuentas de prueba
-- Insertar cuentas básicas para empresa APC

DO $$
DECLARE
    v_company_id bigint;
BEGIN
    SELECT company_id INTO v_company_id FROM public.cfg_company WHERE code = 'APC' LIMIT 1;
    
    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Empresa APC no encontrada';
    END IF;

    DELETE FROM public.con_plan_cuentas WHERE company_id = v_company_id;

    -- ACTIVOS
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1', 'ACTIVOS', 'Activos totales', 'ACTIVO', 'ACTIVO', 1, false, 'ACTIVE', 'HNL', 'ACT', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1', 'Activos Corrientes', 'Circulante', 'ACTIVO', 'ACTIVO_CORR', 2, false, 'ACTIVE', 'HNL', 'AC', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.1', 'Efectivo y Equivalentes', 'Caja y bancos', 'ACTIVO', 'EFECTIVO', 3, false, 'ACTIVE', 'HNL', 'EfE', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.1.01', 'Caja General', 'Caja principal', 'ACTIVO', 'EFECTIVO', 4, true, 'ACTIVE', 'HNL', 'CG', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.1.02', 'Banco Principal', 'Cuenta corriente principal', 'ACTIVO', 'EFECTIVO', 4, true, 'ACTIVE', 'HNL', 'BP', false, false, false, true, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.2', 'Cuentas por Cobrar', 'Deudores diversos', 'ACTIVO', 'CUENTAS_COBRAR', 3, false, 'ACTIVE', 'HNL', 'CxC', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.2.01', 'Clientes', 'Cuentas de clientes', 'ACTIVO', 'CUENTAS_COBRAR', 4, true, 'ACTIVE', 'HNL', 'CLI', false, false, true, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.3', 'Créditos Fiscales', 'Impuestos por recuperar', 'ACTIVO', 'IMPUESTOS', 3, false, 'ACTIVE', 'HNL', 'CF', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '1.1.3.01', 'IVA Crédito Fiscal', 'IVA a recuperar', 'ACTIVO', 'IMPUESTOS', 4, true, 'ACTIVE', 'HNL', 'IVA-CF', false, false, false, false, true, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    -- PASIVOS
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2', 'PASIVOS', 'Pasivos totales', 'PASIVO', 'PASIVO', 1, false, 'ACTIVE', 'HNL', 'PAS', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1', 'Pasivos Corrientes', 'Corto plazo', 'PASIVO', 'PASIVO_CORR', 2, false, 'ACTIVE', 'HNL', 'PC', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1.1', 'Cuentas por Pagar', 'Acreedores', 'PASIVO', 'CUENTAS_PAGAR', 3, false, 'ACTIVE', 'HNL', 'CxP', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1.1.01', 'Proveedores Nacionales', 'Proveedores locales', 'PASIVO', 'CUENTAS_PAGAR', 4, true, 'ACTIVE', 'HNL', 'PN', false, false, true, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1.2', 'Impuestos por Pagar', 'Obligaciones fiscales', 'PASIVO', 'IMPUESTOS', 3, false, 'ACTIVE', 'HNL', 'IxP', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1.2.01', 'ISV por Pagar', 'Impuesto sobre ventas', 'PASIVO', 'IMPUESTOS', 4, true, 'ACTIVE', 'HNL', 'ISV', false, false, false, false, true, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '2.1.2.02', 'ISR Retenido por Pagar', 'Impuesto retenido', 'PASIVO', 'IMPUESTOS', 4, true, 'ACTIVE', 'HNL', 'ISR-R', false, false, false, false, true, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    -- PATRIMONIO
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '3', 'PATRIMONIO', 'Capital y reservas', 'CAPITAL', 'CAPITAL', 1, false, 'ACTIVE', 'HNL', 'PAT', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '3.1', 'Capital Social', 'Aporte de socios', 'CAPITAL', 'CAPITAL', 2, false, 'ACTIVE', 'HNL', 'CS', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '3.1.1', 'Capital Pagado', 'Capital desembolsado', 'CAPITAL', 'CAPITAL', 3, true, 'ACTIVE', 'HNL', 'CP', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    -- INGRESOS
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '4', 'INGRESOS', 'Ingresos totales', 'INGRESO', 'INGRESO', 1, false, 'ACTIVE', 'HNL', 'ING', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '4.1', 'Ingresos Operativos', 'Ventas y servicios', 'INGRESO', 'INGRESO_OPER', 2, false, 'ACTIVE', 'HNL', 'IO', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '4.1.1', 'Ingresos por Servicios de Agua', 'Cuotas de agua', 'INGRESO', 'INGRESO_OPER', 3, true, 'ACTIVE', 'HNL', 'SA', true, true, true, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '4.1.2', 'Otros Ingresos Operativos', 'Ingresos diversos', 'INGRESO', 'INGRESO_OPER', 3, true, 'ACTIVE', 'HNL', 'OIO', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    -- COSTOS Y GASTOS
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5', 'COSTOS Y GASTOS', 'Costos y gastos totales', 'GASTO', 'GASTO', 1, false, 'ACTIVE', 'HNL', 'CG', false, false, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5.1', 'Costos de Operación', 'Costos de servicios', 'GASTO', 'COSTO', 2, false, 'ACTIVE', 'HNL', 'CO', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5.1.1', 'Costo Servicios Operativos', 'Materiales y mano de obra', 'GASTO', 'COSTO', 3, true, 'ACTIVE', 'HNL', 'CSO', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5.2', 'Gastos Operativos', 'Gastos de administración y ventas', 'GASTO', 'GASTO_OPER', 2, false, 'ACTIVE', 'HNL', 'GO', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5.2.1', 'Gastos Administrativos', 'Sueldos, servicios, etc', 'GASTO', 'GASTO_OPER', 3, true, 'ACTIVE', 'HNL', 'GA', false, true, false, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    INSERT INTO public.con_plan_cuentas (company_id, code, name, description, account_type, category, level, allows_posting, status, currency_code, short_description, allows_budget, allows_cost_center, allows_third, allows_bank, is_tax_base, allows_amount, allows_multi_currency, created_at, created_by, updated_at, updated_by)
    VALUES (v_company_id, '5.2.2', 'Gastos de Ventas', 'Comisiones, publicidad, etc', 'GASTO', 'GASTO_OPER', 3, true, 'ACTIVE', 'HNL', 'GV', false, true, true, false, false, true, false, now(), 'USUARIO', now(), 'USUARIO');
    
    -- Actualizar referencias padre-hijo
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1') WHERE company_id = v_company_id AND code IN ('1.1', '1.2');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1') WHERE company_id = v_company_id AND code IN ('1.1.1', '1.1.2', '1.1.3');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.2') WHERE company_id = v_company_id AND code = '1.2.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.1') WHERE company_id = v_company_id AND code IN ('1.1.1.01', '1.1.1.02');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.2') WHERE company_id = v_company_id AND code = '1.1.2.01';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.3') WHERE company_id = v_company_id AND code = '1.1.3.01';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.2.1') WHERE company_id = v_company_id AND code = '1.2.1.01';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2') WHERE company_id = v_company_id AND code = '2.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1') WHERE company_id = v_company_id AND code IN ('2.1.1', '2.1.2');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1.1') WHERE company_id = v_company_id AND code = '2.1.1.01';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1.2') WHERE company_id = v_company_id AND code IN ('2.1.2.01', '2.1.2.02');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '3') WHERE company_id = v_company_id AND code = '3.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '3.1') WHERE company_id = v_company_id AND code = '3.1.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '4') WHERE company_id = v_company_id AND code = '4.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '4.1') WHERE company_id = v_company_id AND code IN ('4.1.1', '4.1.2');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '5') WHERE company_id = v_company_id AND code IN ('5.1', '5.2');
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '5.1') WHERE company_id = v_company_id AND code = '5.1.1';
    UPDATE public.con_plan_cuentas SET parent_account_id = (SELECT account_id FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '5.2') WHERE company_id = v_company_id AND code IN ('5.2.1', '5.2.2');

    RAISE NOTICE 'Plan de cuentas de prueba insertado exitosamente para empresa APC (%)' , v_company_id;
END $$;
