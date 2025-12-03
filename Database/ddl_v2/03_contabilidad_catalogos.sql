-- ================================================
-- 03_contabilidad_catalogos.sql
-- Catálogos base para plan de cuentas y plantillas contables demo
-- Requiere: 01_configuracion_base.sql y 02_contabilidad_core.sql
-- Nota: ajustar los códigos de cuentas según la normativa local antes de producción.
-- ================================================

DO $$
DECLARE
    v_company_id bigint;
BEGIN
    -- Aseguramos que exista al menos una compañía (demo)
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     ORDER BY company_id
     LIMIT 1;

    IF v_company_id IS NULL THEN
        INSERT INTO public.cfg_currency (currency_code, name, symbol, is_base_currency)
        VALUES ('HNL', 'Lempira Hondureño', 'L', true)
        ON CONFLICT (currency_code) DO NOTHING;

        INSERT INTO public.cfg_company (code, commercial_name, legal_name, tax_id, currency_code)
        VALUES ('DEMO', 'Empresa Demo', 'Empresa Demo S.A.', '08011990000012', 'HNL')
        RETURNING company_id INTO v_company_id;
    END IF;

    -- Tipos de documentos base
    INSERT INTO public.cfg_document_type (company_id, module, code, name, description, requires_cai)
    VALUES
        (v_company_id, 'VENTAS', 'FACTURA', 'Factura de Venta', 'Documento fiscal autorizado para ventas', true),
        (v_company_id, 'VENTAS', 'NC', 'Nota de Crédito', 'Reverso parcial o total de factura de venta', true),
        (v_company_id, 'VENTAS', 'ND', 'Nota de Débito', 'Incremento de saldo a clientes', true),
        (v_company_id, 'VENTAS', 'REC', 'Recibo de Cobro', 'Aplicación de cobros a clientes', false),
        (v_company_id, 'COMPRAS', 'FACTURA', 'Factura de Compra', 'Documento fiscal del proveedor', true),
        (v_company_id, 'COMPRAS', 'NC', 'Nota de Crédito Proveedor', 'Ajuste de saldo a favor del comprador', true),
        (v_company_id, 'COMPRAS', 'PAGO', 'Orden de Pago', 'Documento de pago a proveedores', false),
        (v_company_id, 'BANCOS', 'MOV', 'Movimiento Bancario', 'Ingreso o egreso de bancos', false),
        (v_company_id, 'INVENTARIO', 'AJUSTE', 'Ajuste de Inventario', 'Entrada o salida por ajuste manual', false),
        (v_company_id, 'INVENTARIO', 'TRASLADO', 'Traslado entre almacenes', 'Movimiento de transferencia interna', false)
    ON CONFLICT (company_id, module, code)
    DO UPDATE SET name = EXCLUDED.name,
                  description = EXCLUDED.description,
                  requires_cai = EXCLUDED.requires_cai,
                  updated_at = now(),
                  updated_by = current_user;

    -- Plan de cuentas simplificado
    INSERT INTO public.con_plan_cuentas (company_id, code, name, account_type, level, allows_posting)
    VALUES
        (v_company_id, '1',   'Activos', 'ACTIVO', 1, false),
        (v_company_id, '1.1', 'Activos Corrientes', 'ACTIVO', 2, false),
        (v_company_id, '1.1.1', 'Caja y Bancos', 'ACTIVO', 3, false),
        (v_company_id, '1.1.1.01', 'Caja General', 'ACTIVO', 4, true),
        (v_company_id, '1.1.1.02', 'Banco Principal', 'ACTIVO', 4, true),
        (v_company_id, '1.1.2', 'Clientes', 'ACTIVO', 3, false),
        (v_company_id, '1.1.2.01', 'Clientes Nacionales', 'ACTIVO', 4, true),
        (v_company_id, '2',   'Pasivos', 'PASIVO', 1, false),
        (v_company_id, '2.1', 'Pasivos Corrientes', 'PASIVO', 2, false),
        (v_company_id, '2.1.1', 'Proveedores', 'PASIVO', 3, false),
        (v_company_id, '2.1.1.01', 'Proveedores Nacionales', 'PASIVO', 4, true),
        (v_company_id, '3',   'Patrimonio', 'CAPITAL', 1, false),
        (v_company_id, '4',   'Ingresos', 'INGRESO', 1, false),
        (v_company_id, '4.1', 'Ingresos Operativos', 'INGRESO', 2, false),
        (v_company_id, '4.1.1', 'Ingresos por Servicios', 'INGRESO', 3, true),
        (v_company_id, '5',   'Gastos', 'GASTO', 1, false),
        (v_company_id, '5.1', 'Gastos Operativos', 'GASTO', 2, false),
        (v_company_id, '5.1.1', 'Servicios Básicos', 'GASTO', 3, true)
    ON CONFLICT (company_id, code) DO NOTHING;

    -- Centros de costo demo
    INSERT INTO public.con_centro_costo (company_id, code, name)
    VALUES
        (v_company_id, 'ADM', 'Administración'),
        (v_company_id, 'VEN', 'Ventas'),
        (v_company_id, 'SOP', 'Soporte Técnico')
    ON CONFLICT (company_id, code) DO NOTHING;

    -- Plantilla de póliza: Factura de venta
    INSERT INTO public.con_plantilla_poliza (company_id, module, document_type, name, description)
    VALUES (v_company_id, 'VENTAS', 'FACTURA', 'Factura Servicios', 'Reconoce ingreso y CxC')
    ON CONFLICT (company_id, module, document_type, name) DO NOTHING;

    -- Obtener template_id
    PERFORM 1;
END
$$;

-- Insertar líneas de la plantilla usando UPSERT
WITH plantilla AS (
    SELECT template_id, company_id
    FROM public.con_plantilla_poliza
    WHERE module = 'VENTAS'
      AND document_type = 'FACTURA'
)
, line_defs AS (
    SELECT p.template_id,
           p.company_id,
           vals.line_number,
           vals.debit_formula,
           vals.credit_formula,
           vals.description,
           vals.account_code
    FROM plantilla p
    CROSS JOIN (
        VALUES
            (1, 'total', NULL, 'Clientes por Cobrar', '1.1.2.01'),
            (2, NULL, 'total', 'Ingresos por Servicios', '4.1.1'),
            (3, 'iva', NULL, 'IVA Débito Fiscal', '2.1.1.01')
    ) AS vals(line_number, debit_formula, credit_formula, description, account_code)
)
INSERT INTO public.con_plantilla_poliza_linea (template_id, line_number, account_id, cost_center_id, debit_formula, credit_formula, description)
SELECT l.template_id,
       l.line_number,
       acct.account_id,
       NULL,
       l.debit_formula,
       l.credit_formula,
       l.description
FROM line_defs l
JOIN public.con_plan_cuentas acct
  ON acct.company_id = l.company_id
 AND acct.code = l.account_code
ON CONFLICT (template_id, line_number) DO NOTHING;
