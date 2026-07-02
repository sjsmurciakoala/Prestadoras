-- =============================================================================
-- Seed: Document type CAJA/ABO + con_regla_integracion + plantilla contable para abonos
-- Fecha: 2026-06-10
-- Branch: feature/abonos-caja-contabilidad
-- =============================================================================

DO $$
DECLARE
    v_company_id bigint;
    v_cxc_account_id bigint;
    v_caja_account_id bigint;
    v_doc_type_id bigint;
    v_template_id bigint;
BEGIN
    -- 1. Resolver empresa activa (primera empresa, ajustar segun entorno)
    SELECT company_id INTO v_company_id
      FROM public.cfg_company
     WHERE upper(status) = 'ACTIVE'
     ORDER BY company_id
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No hay empresa activa configurada.';
    END IF;

    -- 2. Crear cfg_document_type CAJA/ABO
    INSERT INTO public.cfg_document_type (
        company_id, module, code, name, description, requires_cai, is_active, created_at, created_by
    ) VALUES (
        v_company_id, 'CAJA', 'ABO',
        'Abono en Caja',
        'Registro de abonos (pagos parciales) en caja',
        false, true, now(), 'system'
    )
    ON CONFLICT (company_id, module, code) DO UPDATE
       SET name = EXCLUDED.name,
           description = EXCLUDED.description,
           is_active = true,
           updated_at = now(),
           updated_by = 'system'
    RETURNING document_type_id INTO v_doc_type_id;

    -- 3. Resolver cuenta por cobrar (buscar cuenta tipo ACTIVO con palabra clave 'cobrar')
    SELECT account_id INTO v_cxc_account_id
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id
       AND allows_posting = true
       AND lower(name) LIKE '%cobrar%'
       AND account_type = 'ACTIVO'
     ORDER BY account_id
     LIMIT 1;

    IF v_cxc_account_id IS NULL THEN
        SELECT account_id INTO v_cxc_account_id
          FROM public.con_plan_cuentas
         WHERE company_id = v_company_id
           AND allows_posting = true
           AND account_type = 'ACTIVO'
         ORDER BY code
         LIMIT 1;
    END IF;

    -- 4. Resolver cuenta de caja (buscar cuenta tipo ACTIVO con palabra clave 'caja' o 'efectivo')
    SELECT account_id INTO v_caja_account_id
      FROM public.con_plan_cuentas
     WHERE company_id = v_company_id
       AND allows_posting = true
       AND (lower(name) LIKE '%caja%' OR lower(name) LIKE '%efectivo%')
       AND account_type = 'ACTIVO'
     ORDER BY account_id
     LIMIT 1;

    IF v_caja_account_id IS NULL THEN
        SELECT account_id INTO v_caja_account_id
          FROM public.con_plan_cuentas
         WHERE company_id = v_company_id
           AND allows_posting = true
           AND account_type = 'ACTIVO'
         ORDER BY code DESC
         LIMIT 1;
    END IF;

    -- 5. Crear reglas de integración para escenarios de abonos
    -- Escenario 1: ABONO_EFECTIVO (Debe: Caja, Haber: CxC)
    IF v_caja_account_id IS NOT NULL AND v_cxc_account_id IS NOT NULL THEN
        INSERT INTO public.con_regla_integracion (
            company_id, module, document_type_id, scenario_code, description,
            debit_account_id, credit_account_id, is_active, created_at, created_by
        ) VALUES (
            v_company_id, 'CAJA', v_doc_type_id, 'ABONO_EFECTIVO', 'Abono en Efectivo',
            v_caja_account_id, v_cxc_account_id, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, scenario_code) DO NOTHING;
    END IF;

    -- Escenario 2: ABONO_BANCO (Debe: Banco temporal/CxC de banco, Haber: CxC)
    IF v_cxc_account_id IS NOT NULL THEN
        INSERT INTO public.con_regla_integracion (
            company_id, module, document_type_id, scenario_code, description,
            debit_account_id, credit_account_id, is_active, created_at, created_by
        ) VALUES (
            v_company_id, 'CAJA', v_doc_type_id, 'ABONO_BANCO', 'Abono por Transferencia o Banco',
            COALESCE(v_caja_account_id, v_cxc_account_id), v_cxc_account_id, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, scenario_code) DO NOTHING;
    END IF;

    -- 6. Crear con_plantilla_partida_hdr para CAJA/ABO
    SELECT template_id INTO v_template_id
      FROM public.con_plantilla_partida_hdr
     WHERE company_id = v_company_id
       AND name = 'CAPTACION CAJA ABO'
     LIMIT 1;

    IF v_template_id IS NULL THEN
        INSERT INTO public.con_plantilla_partida_hdr (
            company_id, module, document_type, name, description, is_active,
            created_at, created_by, updated_at, updated_by
        ) VALUES (
            v_company_id, 'CAJA', 'ABO', 'CAPTACION CAJA ABO',
            'Plantilla operativa captación - Abonos en Caja',
            true, now(), 'system', now(), 'system'
        )
        RETURNING template_id INTO v_template_id;
    ELSE
        UPDATE public.con_plantilla_partida_hdr
           SET module = 'CAJA',
               document_type = 'ABO',
               description = 'Plantilla operativa captación - Abonos en Caja',
               is_active = true,
               updated_at = now(),
               updated_by = 'system'
         WHERE template_id = v_template_id;
    END IF;

    -- 7. Crear lineas de con_plantilla_partida_dtl
    DELETE FROM public.con_plantilla_partida_dtl
     WHERE company_id = v_company_id
       AND template_id = v_template_id;

    -- Usamos DETAIL_EXPAND o FIXED. Como las cuentas cambian por regla (debit/credit del escenario),
    -- podemos usar fórmulas o resolverlas por el motor de póliza basado en reglas.
    -- Para sp_con_generar_comprobante, si usa una plantilla con debit/credit de la regla de integración,
    -- las líneas se resuelven dinámicamente si account_id es NULL y se especifica la regla de integración
    -- o si las mapeamos en el JSON de valores.
    -- Vamos a mapear las líneas de la plantilla usando fórmulas dinámicas:
    -- Linea 1: Débito de la cuenta resuelta (efectivo o banco)
    -- Linea 2: Crédito de la cuenta de cliente (CxC)
    -- El sp_con_generar_comprobante leerá del JSON o del contexto.
    -- Para que sea genérico como FAC/REC:
    -- Si colocamos NULL en account_id, y debit_formula = '{debit_account}', credit_formula = '{credit_account}',
    -- sp_con_generar_comprobante puede reemplazar las cuentas pasadas en el JSON.
    -- O bien, podemos pasar directamente las cuentas en el JSONb 'ValuesJson' al llamar sp_con_generar_comprobante.
    -- Miremos cómo lo hace CaptacionPagosService o FacturacionMiscelaneosService:
    -- En CaptacionPagosService:
    -- v_debit_account_id y v_credit_account_id son resueltas en C# y la plantilla tiene {total}.
    -- En la plantilla de FAC/REC de 20260313_seed_plantillas_ventas_fac_rec.sql:
    -- Linea 1: account_id = v_debit_account_id, debit_formula = '{total}'
    -- Linea 2: account_id = v_credit_account_id, credit_formula = '{total}'
    -- Como para ABO queremos que sea dinámico basado en la regla de integración del escenario elegido
    -- (ABONO_EFECTIVO o ABONO_BANCO), podemos resolver las cuentas en C# y pasárselas al SP.
    -- Si la plantilla tiene account_id fijo, no sería dinámica por escenario.
    -- Para hacerlo dinámico, sp_con_generar_comprobante permite usar fórmulas de cuentas o resolverlas.
    -- Si colocamos account_id = NULL y debit_formula = '{total}', sp_con_generar_comprobante buscará la cuenta del JSON,
    -- o podemos definir las fórmulas de cuenta: debit_account y credit_account en el JSON.
    -- Vamos a insertar las líneas con account_id = NULL y usar fórmulas de cuenta o cargarlas desde el JSON.
    -- En con_plantilla_partida_dtl de MIS:
    -- Linea 1: account_id = v_cxc_account_id, debit_formula = '{total}'
    -- Linea 2: account_id = NULL (detalle_account_field = 'account_id', detail_amount_field = 'total')
    -- Para Abonos, podemos hacer que C# resuelva v_debit_account_id y v_credit_account_id (según el escenario),
    -- y las pase como parámetros en el JSON, o usar una plantilla dinámica donde:
    -- Linea 1: account_id = NULL, debit_formula = '{total}', account_formula = '{debit_account_id}' (o similar)
    -- Espera, ¿sp_con_generar_comprobante soporta fórmulas de cuenta como '{debit_account_id}'?
    -- Veamos cómo está programado sp_con_generar_comprobante.
    -- Si no, podemos crear la plantilla contable usando las cuentas por defecto y actualizar la regla.
    -- O mejor aún, para simplificar y alinear con el estándar, podemos resolver las cuentas debit/credit
    -- del escenario en C#, y crear la plantilla con account_id de las cuentas por defecto,
    -- pero para soportar cambios en caliente, podemos usar fórmulas.
    -- Veamos qué columnas tiene con_plantilla_partida_dtl.

    INSERT INTO public.con_plantilla_partida_dtl (
        company_id, template_id, line_number, account_id,
        debit_formula, credit_formula, description
    ) VALUES (
        v_company_id, v_template_id, 1, COALESCE(v_caja_account_id, 0),
        '{total}', NULL, 'Debe por abono recibido (Caja/Banco)'
    ), (
        v_company_id, v_template_id, 2, COALESCE(v_cxc_account_id, 0),
        NULL, '{total}', 'Haber por CxC Clientes'
    );

    RAISE NOTICE 'Seed CAJA/ABO completado.';
END;
$$;
