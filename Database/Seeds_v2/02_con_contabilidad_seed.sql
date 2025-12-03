-- ================================================
-- 02_con_contabilidad_seed.sql
-- Seeds demo para el módulo contable: plan de cuentas,
-- centros de costo, períodos, diarios, reglas de integración
-- y plantillas de póliza básicas.
-- Requiere: 01_configuracion_base.sql + 01_cfg_configuracion_seed.sql,
--           02_contabilidad_core.sql.
-- ================================================

DO $$
DECLARE
    v_company_id          bigint;
    v_currency_code       char(3);
    v_year                int := EXTRACT(YEAR FROM current_date)::int;
    v_month               int;
    v_start_date          date;
    v_end_date            date;
    v_status              varchar(20);
    v_doc_ven_fac         bigint;
    v_doc_ven_rec         bigint;
    v_doc_ven_nc          bigint;
    v_doc_com_fac         bigint;
    v_doc_com_pag         bigint;
    v_cc_admin            bigint;
    v_cc_ventas           bigint;
    v_cc_oper             bigint;
    v_tpl_ven_fact        bigint;
    v_tpl_ven_cobro       bigint;
    v_tpl_com_fact        bigint;
    v_tpl_com_pago        bigint;
    v_acc_caja            bigint;
    v_acc_banco           bigint;
    v_acc_clientes        bigint;
    v_acc_iva_debito      bigint;
    v_acc_proveedores     bigint;
    v_acc_isv_por_pagar   bigint;
    v_acc_isr_ret         bigint;
    v_acc_ingresos        bigint;
    v_acc_costos          bigint;
    v_acc_gasto_admin     bigint;
    record_plan           record;
BEGIN
    SELECT company_id, currency_code
      INTO v_company_id, v_currency_code
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company SIAD-DEMO no encontrada. Ejecuta 01_cfg_configuracion_seed.sql primero.';
    END IF;

    -- Centros de costo base
    INSERT INTO public.con_centro_costo (company_id, code, name, description, status)
    VALUES
        (v_company_id, 'ADM', 'Administración', 'Gastos administrativos', 'ACTIVE'),
        (v_company_id, 'VEN', 'Ventas', 'Operaciones comerciales', 'ACTIVE'),
        (v_company_id, 'OPS', 'Operaciones', 'Planta / campo', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET name = EXCLUDED.name,
            description = EXCLUDED.description,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user;

    SELECT cost_center_id INTO v_cc_admin FROM public.con_centro_costo WHERE company_id = v_company_id AND code = 'ADM';
    SELECT cost_center_id INTO v_cc_ventas FROM public.con_centro_costo WHERE company_id = v_company_id AND code = 'VEN';
    SELECT cost_center_id INTO v_cc_oper   FROM public.con_centro_costo WHERE company_id = v_company_id AND code = 'OPS';

    -- Diarios contables
    INSERT INTO public.con_diario (company_id, code, name, description, sequence_prefix, is_active, allows_manual)
    VALUES
        (v_company_id, 'GEN', 'Diario General', 'Asientos misceláneos', 'GEN', true, true),
        (v_company_id, 'VEN', 'Diario Ventas', 'Automáticos de ventas', 'VEN', true, false),
        (v_company_id, 'COM', 'Diario Compras', 'Automáticos de compras', 'COM', true, false),
        (v_company_id, 'BAN', 'Diario Bancos', 'Movimientos bancarios', 'BAN', true, false)
    ON CONFLICT (company_id, code) DO UPDATE
        SET name = EXCLUDED.name,
            description = EXCLUDED.description,
            sequence_prefix = EXCLUDED.sequence_prefix,
            is_active = EXCLUDED.is_active,
            allows_manual = EXCLUDED.allows_manual,
            updated_at = now(),
            updated_by = current_user;

    -- Control de periodos (12 meses del año actual)
    FOR v_month IN 1..12 LOOP
        v_start_date := make_date(v_year, v_month, 1);
        v_end_date := (v_start_date + INTERVAL '1 month - 1 day')::date;
        v_status := CASE
                        WHEN v_start_date < date_trunc('month', current_date)::date THEN 'CLOSED'
                        ELSE 'OPEN'
                    END;

        INSERT INTO public.con_periodo_contable (
            company_id, code, name, start_date, end_date, status, closed_at, closed_by)
        VALUES (
            v_company_id,
            to_char(v_start_date, 'YYYYMM'),
            to_char(v_start_date, 'FMMonth YYYY'),
            v_start_date,
            v_end_date,
            v_status,
            CASE WHEN v_status = 'CLOSED' THEN now() ELSE NULL END,
            CASE WHEN v_status = 'CLOSED' THEN current_user ELSE NULL END)
        ON CONFLICT (company_id, code) DO UPDATE
            SET name = EXCLUDED.name,
                start_date = EXCLUDED.start_date,
                end_date = EXCLUDED.end_date,
                status = EXCLUDED.status,
                closed_at = CASE
                                WHEN EXCLUDED.status = 'CLOSED' THEN COALESCE(public.con_periodo_contable.closed_at, now())
                                ELSE NULL
                            END,
                closed_by = CASE
                                WHEN EXCLUDED.status = 'CLOSED' THEN COALESCE(public.con_periodo_contable.closed_by, current_user)
                                ELSE NULL
                            END,
                updated_at = now(),
                updated_by = current_user;
    END LOOP;

    -- Definición del plan de cuentas
    CREATE TEMP TABLE tmp_plan_defs (
        code           varchar(30),
        name           varchar(200),
        account_type   varchar(30),
        category       varchar(30),
        level          smallint,
        allows_posting boolean,
        parent_code    varchar(30)
    ) ON COMMIT DROP;

    INSERT INTO tmp_plan_defs (code, name, account_type, category, level, allows_posting, parent_code) VALUES
        ('1',        'Activos',                       'ACTIVO',  'ACTIVO',            1, false, NULL),
        ('1.1',      'Activos corrientes',            'ACTIVO',  'ACTIVO_CORR',       2, false, '1'),
        ('1.1.1',    'Efectivo y equivalentes',       'ACTIVO',  'EFECTIVO',          3, false, '1.1'),
        ('1.1.1.01', 'Caja general',                  'ACTIVO',  'EFECTIVO',          4, true,  '1.1.1'),
        ('1.1.1.02', 'Banco principal',               'ACTIVO',  'EFECTIVO',          4, true,  '1.1.1'),
        ('1.1.2',    'Cuentas por cobrar',            'ACTIVO',  'CUENTAS_COBRAR',    3, false, '1.1'),
        ('1.1.2.01', 'Clientes servicios',            'ACTIVO',  'CUENTAS_COBRAR',    4, true,  '1.1.2'),
        ('1.1.3',    'Créditos fiscales',             'ACTIVO',  'IMPUESTOS',         3, false, '1.1'),
        ('1.1.3.01', 'IVA crédito fiscal',            'ACTIVO',  'IMPUESTOS',         4, true,  '1.1.3'),
        ('1.2',      'Activos no corrientes',         'ACTIVO',  'ACTIVO_NO_CORR',    2, false, '1'),
        ('1.2.1',    'Propiedad planta y equipo',     'ACTIVO',  'ACTIVOS_FIJOS',     3, false, '1.2'),
        ('1.2.1.01', 'Infraestructura hídrica',       'ACTIVO',  'ACTIVOS_FIJOS',     4, true,  '1.2.1'),
        ('2',        'Pasivos',                       'PASIVO',  'PASIVO',            1, false, NULL),
        ('2.1',      'Pasivos corrientes',            'PASIVO',  'PASIVO_CORR',       2, false, '2'),
        ('2.1.1',    'Cuentas por pagar',             'PASIVO',  'CUENTAS_PAGAR',     3, false, '2.1'),
        ('2.1.1.01', 'Proveedores nacionales',        'PASIVO',  'CUENTAS_PAGAR',     4, true,  '2.1.1'),
        ('2.1.1.02', 'Documentos por pagar',          'PASIVO',  'CUENTAS_PAGAR',     4, true,  '2.1.1'),
        ('2.1.2',    'Impuestos por pagar',           'PASIVO',  'IMPUESTOS',         3, false, '2.1'),
        ('2.1.2.01', 'ISV por pagar',                 'PASIVO',  'IMPUESTOS',         4, true,  '2.1.2'),
        ('2.1.2.02', 'ISR retenido por pagar',        'PASIVO',  'IMPUESTOS',         4, true,  '2.1.2'),
        ('3',        'Patrimonio',                    'CAPITAL', 'CAPITAL',           1, false, NULL),
        ('3.1',      'Capital social',                'CAPITAL', 'CAPITAL',           2, false, '3'),
        ('3.1.1',    'Capital pagado',                'CAPITAL', 'CAPITAL',           3, true,  '3.1'),
        ('4',        'Ingresos',                      'INGRESO', 'INGRESO',           1, false, NULL),
        ('4.1',      'Ingresos operativos',           'INGRESO', 'INGRESO_OPER',      2, false, '4'),
        ('4.1.1',    'Ingresos servicios de agua',    'INGRESO', 'INGRESO_OPER',      3, true,  '4.1'),
        ('4.1.2',    'Otros ingresos',                'INGRESO', 'INGRESO_OTROS',     3, true,  '4.1'),
        ('5',        'Costos y gastos',               'GASTO',   'GASTO',             1, false, NULL),
        ('5.1',      'Costos de operación',           'GASTO',   'COSTO',             2, false, '5'),
        ('5.1.1',    'Costo servicios operativos',    'GASTO',   'COSTO',             3, true,  '5.1'),
        ('5.2',      'Gastos operativos',             'GASTO',   'GASTO_OPER',        2, false, '5'),
        ('5.2.1',    'Gastos administrativos',        'GASTO',   'GASTO_OPER',        3, true,  '5.2'),
        ('5.2.2',    'Gastos de ventas',              'GASTO',   'GASTO_OPER',        3, true,  '5.2'),
        ('6',        'Otros resultados',              'GASTO',   'OTROS',             1, false, NULL),
        ('6.1',      'Gastos financieros',            'GASTO',   'OTROS',             2, true,  '6');

    FOR record_plan IN SELECT * FROM tmp_plan_defs LOOP
        INSERT INTO public.con_plan_cuentas (
            company_id, code, name, description, account_type, category,
            level, allows_posting, currency_code, status)
        VALUES (
            v_company_id,
            record_plan.code,
            record_plan.name,
            record_plan.name,
            record_plan.account_type,
            record_plan.category,
            record_plan.level,
            record_plan.allows_posting,
            v_currency_code,
            'ACTIVE')
        ON CONFLICT (company_id, code) DO UPDATE
            SET name = EXCLUDED.name,
                description = EXCLUDED.description,
                account_type = EXCLUDED.account_type,
                category = EXCLUDED.category,
                level = EXCLUDED.level,
                allows_posting = EXCLUDED.allows_posting,
                currency_code = EXCLUDED.currency_code,
                status = EXCLUDED.status,
                updated_at = now(),
                updated_by = current_user;
    END LOOP;

    FOR record_plan IN SELECT * FROM tmp_plan_defs LOOP
        IF record_plan.parent_code IS NULL THEN
            UPDATE public.con_plan_cuentas
               SET parent_account_id = NULL
             WHERE company_id = v_company_id
               AND code = record_plan.code;
        ELSE
            UPDATE public.con_plan_cuentas child
               SET parent_account_id = parent.account_id
              FROM public.con_plan_cuentas parent
             WHERE parent.company_id = v_company_id
               AND parent.code = record_plan.parent_code
               AND child.company_id = v_company_id
               AND child.code = record_plan.code;
        END IF;
    END LOOP;

    DROP TABLE IF EXISTS tmp_plan_defs;

    -- Documentos necesarios para reglas
    SELECT document_type_id INTO v_doc_ven_fac
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'FAC';
    SELECT document_type_id INTO v_doc_ven_rec
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'REC';
    SELECT document_type_id INTO v_doc_ven_nc
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'VENTAS' AND code = 'NC';
    SELECT document_type_id INTO v_doc_com_fac
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND code = 'FAC';
    SELECT document_type_id INTO v_doc_com_pag
      FROM public.cfg_document_type
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND code = 'PAG';

    IF v_doc_ven_fac IS NULL OR v_doc_com_fac IS NULL THEN
        RAISE EXCEPTION 'No se encontraron tipos de documento (VENTAS/COMPRAS). Revisa seed de configuración.';
    END IF;

    -- Mapear cuentas clave
    SELECT account_id INTO v_acc_caja          FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.1.01';
    SELECT account_id INTO v_acc_banco         FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.1.02';
    SELECT account_id INTO v_acc_clientes      FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.2.01';
    SELECT account_id INTO v_acc_iva_debito    FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '1.1.3.01';
    SELECT account_id INTO v_acc_proveedores   FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1.1.01';
    SELECT account_id INTO v_acc_isv_por_pagar FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1.2.01';
    SELECT account_id INTO v_acc_isr_ret       FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '2.1.2.02';
    SELECT account_id INTO v_acc_ingresos      FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '4.1.1';
    SELECT account_id INTO v_acc_costos        FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '5.1.1';
    SELECT account_id INTO v_acc_gasto_admin   FROM public.con_plan_cuentas WHERE company_id = v_company_id AND code = '5.2.1';

    IF v_acc_clientes IS NULL OR v_acc_proveedores IS NULL THEN
        RAISE EXCEPTION 'Plan de cuentas incompleto. Verifica inserción de cuentas principales.';
    END IF;

    -- Plantillas de póliza
    INSERT INTO public.con_plantilla_poliza (company_id, module, document_type, name, description, is_active)
    VALUES
        (v_company_id, 'VENTAS',  'FAC', 'Factura servicios', 'Registra CxC vs ingresos', true),
        (v_company_id, 'VENTAS',  'REC', 'Recibo de cobro',   'Cancela CxC con caja/banco', true),
        (v_company_id, 'COMPRAS', 'FAC', 'Factura proveedor', 'Registra gastos vs CxP', true),
        (v_company_id, 'COMPRAS', 'PAG', 'Pago proveedor',    'Cancela CxP con banco', true)
    ON CONFLICT (company_id, module, document_type, name) DO UPDATE
        SET description = EXCLUDED.description,
            is_active = EXCLUDED.is_active,
            updated_at = now(),
            updated_by = current_user;

    SELECT template_id INTO v_tpl_ven_fact FROM public.con_plantilla_poliza
     WHERE company_id = v_company_id AND module = 'VENTAS' AND document_type = 'FAC' AND name = 'Factura servicios';
    SELECT template_id INTO v_tpl_ven_cobro FROM public.con_plantilla_poliza
     WHERE company_id = v_company_id AND module = 'VENTAS' AND document_type = 'REC' AND name = 'Recibo de cobro';
    SELECT template_id INTO v_tpl_com_fact FROM public.con_plantilla_poliza
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND document_type = 'FAC' AND name = 'Factura proveedor';
    SELECT template_id INTO v_tpl_com_pago FROM public.con_plantilla_poliza
     WHERE company_id = v_company_id AND module = 'COMPRAS' AND document_type = 'PAG' AND name = 'Pago proveedor';

    -- Helper para insertar/actualizar líneas
    PERFORM 1;

    INSERT INTO public.con_plantilla_poliza_linea (template_id, line_number, account_id, cost_center_id, debit_formula, credit_formula, description)
    VALUES
        (v_tpl_ven_fact, 1, v_acc_clientes,  NULL, '{total}',       NULL,           'CxC clientes'),
        (v_tpl_ven_fact, 2, v_acc_ingresos,  v_cc_ventas, NULL,     '{subtotal}',   'Ingresos servicios'),
        (v_tpl_ven_fact, 3, v_acc_isv_por_pagar, NULL,    NULL,     '{iva}',        'ISV por pagar')
    ON CONFLICT (template_id, line_number) DO UPDATE
        SET account_id = EXCLUDED.account_id,
            cost_center_id = EXCLUDED.cost_center_id,
            debit_formula = EXCLUDED.debit_formula,
            credit_formula = EXCLUDED.credit_formula,
            description = EXCLUDED.description,
            template_id = EXCLUDED.template_id;

    INSERT INTO public.con_plantilla_poliza_linea (template_id, line_number, account_id, cost_center_id, debit_formula, credit_formula, description)
    VALUES
        (v_tpl_ven_cobro, 1, v_acc_banco,    NULL, '{cobrado}', NULL, 'Ingreso a banco'),
        (v_tpl_ven_cobro, 2, v_acc_clientes, NULL, NULL,        '{cobrado}', 'Aplicación CxC')
    ON CONFLICT (template_id, line_number) DO UPDATE
        SET account_id = EXCLUDED.account_id,
            debit_formula = EXCLUDED.debit_formula,
            credit_formula = EXCLUDED.credit_formula,
            description = EXCLUDED.description;

    INSERT INTO public.con_plantilla_poliza_linea (template_id, line_number, account_id, cost_center_id, debit_formula, credit_formula, description)
    VALUES
        (v_tpl_com_fact, 1, v_acc_costos,       v_cc_oper, '{subtotal}', NULL, 'Costo/gasto operativo'),
        (v_tpl_com_fact, 2, v_acc_iva_debito,   NULL,      '{iva}',      NULL, 'IVA crédito fiscal'),
        (v_tpl_com_fact, 3, v_acc_proveedores,  NULL,      NULL,         '{total}', 'CxP proveedor')
    ON CONFLICT (template_id, line_number) DO UPDATE
        SET account_id = EXCLUDED.account_id,
            cost_center_id = EXCLUDED.cost_center_id,
            debit_formula = EXCLUDED.debit_formula,
            credit_formula = EXCLUDED.credit_formula,
            description = EXCLUDED.description;

    INSERT INTO public.con_plantilla_poliza_linea (template_id, line_number, account_id, cost_center_id, debit_formula, credit_formula, description)
    VALUES
        (v_tpl_com_pago, 1, v_acc_proveedores, NULL, '{pagado}', NULL, 'Cancela CxP'),
        (v_tpl_com_pago, 2, v_acc_banco,       NULL, NULL, '{pagado}', 'Salida banco'),
        (v_tpl_com_pago, 3, v_acc_isr_ret,     NULL, NULL, '{retencion_isr}', 'Retención ISR')
    ON CONFLICT (template_id, line_number) DO UPDATE
        SET account_id = EXCLUDED.account_id,
            debit_formula = EXCLUDED.debit_formula,
            credit_formula = EXCLUDED.credit_formula,
            description = EXCLUDED.description;

    -- Reglas de integración automática
    INSERT INTO public.con_regla_integracion (
        company_id, module, document_type_id, scenario_code, description,
        debit_account_id, credit_account_id, cost_center_id, is_active)
    VALUES
        (v_company_id, 'VENTAS',  v_doc_ven_fac, 'FACTURA_NETO', 'CxC vs ingresos', v_acc_clientes, v_acc_ingresos, v_cc_ventas, true),
        (v_company_id, 'VENTAS',  v_doc_ven_fac, 'FACTURA_IVA',  'Impuesto por pagar', v_acc_clientes, v_acc_isv_por_pagar, NULL, true),
        (v_company_id, 'VENTAS',  v_doc_ven_rec, 'COBRO',        'Cobro bancario', v_acc_banco, v_acc_clientes, NULL, true),
        (v_company_id, 'VENTAS',  v_doc_ven_nc,  'NOTA_CRED',    'Ajuste crédito cliente', v_acc_ingresos, v_acc_clientes, v_cc_ventas, true),
        (v_company_id, 'COMPRAS', v_doc_com_fac, 'COMPRA_NETO',  'Gasto vs proveedor', v_acc_costos, v_acc_proveedores, v_cc_oper, true),
        (v_company_id, 'COMPRAS', v_doc_com_fac, 'COMPRA_IVA',   'IVA crédito', v_acc_iva_debito, v_acc_proveedores, NULL, true),
        (v_company_id, 'COMPRAS', v_doc_com_pag, 'PAGO',         'Pago a proveedor', v_acc_proveedores, v_acc_banco, NULL, true),
        (v_company_id, 'COMPRAS', v_doc_com_pag, 'RET_ISR',      'Retención ISR', v_acc_proveedores, v_acc_isr_ret, NULL, true)
    ON CONFLICT (company_id, module, document_type_id, scenario_code) DO UPDATE
        SET description = EXCLUDED.description,
            debit_account_id = EXCLUDED.debit_account_id,
            credit_account_id = EXCLUDED.credit_account_id,
            cost_center_id = EXCLUDED.cost_center_id,
            is_active = EXCLUDED.is_active,
            updated_at = now(),
            updated_by = current_user;

    RAISE NOTICE 'Seeds contabilidad completados para la compañía %', v_company_id;
END
$$;
