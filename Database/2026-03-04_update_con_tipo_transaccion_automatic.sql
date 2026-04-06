-- 2026-03-04_update_con_tipo_transaccion_automatic.sql
-- Ajusta tipos de transacción contable para APC con nomenclatura estándar

BEGIN;

DO $$
DECLARE
    v_company_id bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'APC'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company APC no encontrada. Verifica cfg_company.code.';
    END IF;

    UPDATE public.con_tipo_transaccion
       SET status = 'INACTIVE',
           updated_at = now(),
           updated_by = current_user
     WHERE company_id = v_company_id
       AND code IN ('FACTURACION', 'PAGO', 'NOTA_CREDITO', 'NOTA_DEBITO');

    INSERT INTO public.con_tipo_transaccion
        (company_id, code, name, description, category, is_automatic, allows_cost_center, allows_third_party, status, created_at, created_by)
    VALUES
        (v_company_id, 'AP',  'Apertura',        'Registro inicial del período contable', 'APERTURA', true,  false, false, 'ACTIVE', now(), current_user),
        (v_company_id, 'FAC', 'Facturación',     'Facturación de servicios',              'FACTURACION', true,  true,  true,  'ACTIVE', now(), current_user),
        (v_company_id, 'COB', 'Cobro',           'Cobro de clientes',                     'COBRO', true,  true,  true,  'ACTIVE', now(), current_user),
        (v_company_id, 'AJU', 'Ajuste',          'Ajustes contables',                      'AJUSTE', false, true,  false, 'ACTIVE', now(), current_user),
        (v_company_id, 'NC',  'Nota de crédito', 'Disminución de una factura',            'NOTA_CREDITO', true,  true,  true,  'ACTIVE', now(), current_user),
        (v_company_id, 'ND',  'Nota de débito',  'Aumento de una factura',                'NOTA_DEBITO', true,  true,  true,  'ACTIVE', now(), current_user),
        (v_company_id, 'TRA', 'Transferencia',   'Movimiento entre cuentas',             'TRANSFERENCIA', false, false, false, 'ACTIVE', now(), current_user),
        (v_company_id, 'CIE', 'Cierre',          'Cierre contable del período',           'CIERRE', true,  false, false, 'ACTIVE', now(), current_user)
    ON CONFLICT (company_id, code) DO UPDATE
        SET name = EXCLUDED.name,
            description = EXCLUDED.description,
            category = EXCLUDED.category,
            is_automatic = EXCLUDED.is_automatic,
            allows_cost_center = EXCLUDED.allows_cost_center,
            allows_third_party = EXCLUDED.allows_third_party,
            status = EXCLUDED.status,
            updated_at = now(),
            updated_by = current_user;
END
$$;

COMMIT;


select * from con_tipo_transaccion
