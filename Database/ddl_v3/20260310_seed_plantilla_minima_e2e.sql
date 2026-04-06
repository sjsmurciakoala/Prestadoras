-- ============================================================
-- Seed minimo de plantilla para pruebas E2E de comprobantes
-- Fecha: 2026-03-10
-- Objetivo:
--   Crear/actualizar una plantilla activa de dos lineas
--   (debito/credito por {total}) usando cuentas desde
--   con_regla_integracion y cfg_document_type (sin hardcodes).
-- ============================================================
--
-- Uso:
-- 1) Ajustar tmp_con_seed_tpl_params.
-- 2) Ejecutar script completo.
-- 3) Re-ejecutar 20260310_test_e2e_sp_con_generar_comprobante.sql.
--

DROP TABLE IF EXISTS tmp_con_seed_tpl_params;
CREATE TEMP TABLE tmp_con_seed_tpl_params (
    company_id bigint NOT NULL,
    module text NULL,
    document_type text NULL,
    scenario_code_preference text NULL,
    template_name text NOT NULL,
    template_description text NOT NULL,
    user_name text NOT NULL
);

INSERT INTO tmp_con_seed_tpl_params (
    company_id,
    module,
    document_type,
    scenario_code_preference,
    template_name,
    template_description,
    user_name
) VALUES (
    1,
    'VENTAS',
    'FAC',
    'FAC_NETO',
    'E2E AUTO - VENTAS FAC',
    'Plantilla minima para pruebas E2E (debe/haber total)',
    'jmurcia'
);

DO $$
DECLARE
    v_company_id bigint;
    v_module text;
    v_document_type text;
    v_scenario_pref text;
    v_template_name text;
    v_template_description text;
    v_user text;

    v_doc_type_id bigint;
    v_doc_module text;
    v_doc_code text;

    v_debit_account_id bigint;
    v_credit_account_id bigint;
    v_cost_center_id bigint;

    v_template_id bigint;
BEGIN
    SELECT
        p.company_id,
        NULLIF(upper(btrim(p.module)), ''),
        NULLIF(upper(btrim(p.document_type)), ''),
        NULLIF(upper(btrim(p.scenario_code_preference)), ''),
        p.template_name,
        p.template_description,
        p.user_name
    INTO
        v_company_id,
        v_module,
        v_document_type,
        v_scenario_pref,
        v_template_name,
        v_template_description,
        v_user
    FROM tmp_con_seed_tpl_params p
    LIMIT 1;

    -- Resolver documento objetivo activo.
    SELECT dt.document_type_id, dt.module, dt.code
      INTO v_doc_type_id, v_doc_module, v_doc_code
      FROM public.cfg_document_type dt
     WHERE dt.company_id = v_company_id
       AND dt.is_active = true
       AND (v_module IS NULL OR dt.module = v_module)
       AND (v_document_type IS NULL OR dt.code = v_document_type)
     ORDER BY
       CASE
           WHEN dt.module = 'VENTAS' AND dt.code = 'FAC' THEN 0
           ELSE 1
       END,
       dt.document_type_id DESC
     LIMIT 1;

    IF v_doc_type_id IS NULL THEN
        RAISE EXCEPTION 'No hay cfg_document_type activo para company %, module %, document_type %',
            v_company_id, COALESCE(v_module, '<NULL>'), COALESCE(v_document_type, '<NULL>');
    END IF;

    -- Resolver regla activa para obtener cuentas contables.
    SELECT
        r.debit_account_id,
        r.credit_account_id,
        r.cost_center_id
      INTO
        v_debit_account_id,
        v_credit_account_id,
        v_cost_center_id
      FROM public.con_regla_integracion r
     WHERE r.company_id = v_company_id
       AND r.document_type_id = v_doc_type_id
       AND r.is_active = true
     ORDER BY
       CASE
           WHEN v_scenario_pref IS NOT NULL AND upper(COALESCE(r.scenario_code, '')) = v_scenario_pref THEN 0
           WHEN upper(COALESCE(r.scenario_code, '')) IN ('FAC_NETO', 'FACTURA_NETO') THEN 1
           ELSE 2
       END,
       r.updated_at DESC NULLS LAST,
       r.regla_id DESC
     LIMIT 1;

    IF v_debit_account_id IS NULL OR v_credit_account_id IS NULL THEN
        RAISE EXCEPTION 'No hay con_regla_integracion activa con cuentas para company %, module %, document_type %',
            v_company_id, v_doc_module, v_doc_code;
    END IF;

    -- Crear o ubicar cabecera de plantilla.
    SELECT h.template_id
      INTO v_template_id
      FROM public.con_plantilla_partida_hdr h
     WHERE h.company_id = v_company_id
       AND h.module = v_doc_module
       AND h.document_type = v_doc_code
       AND upper(btrim(h.name)) = upper(btrim(v_template_name))
     LIMIT 1;

    IF v_template_id IS NULL THEN
        INSERT INTO public.con_plantilla_partida_hdr (
            company_id, module, document_type, name, description, is_active,
            created_at, created_by, updated_at, updated_by
        ) VALUES (
            v_company_id, v_doc_module, v_doc_code, v_template_name, v_template_description, true,
            now(), v_user, now(), v_user
        )
        RETURNING template_id INTO v_template_id;
    ELSE
        UPDATE public.con_plantilla_partida_hdr
           SET description = v_template_description,
               is_active = true,
               updated_at = now(),
               updated_by = v_user
         WHERE template_id = v_template_id;
    END IF;

    -- Mantener lineas 1 y 2 controladas para prueba E2E.
    DELETE FROM public.con_plantilla_partida_dtl
     WHERE company_id = v_company_id
       AND template_id = v_template_id
       AND line_number IN (1, 2);

    INSERT INTO public.con_plantilla_partida_dtl (
        company_id, template_id, line_number, account_id, cost_center_id,
        debit_formula, credit_formula, description
    ) VALUES
        (
            v_company_id, v_template_id, 1, v_debit_account_id, NULL,
            '{total}', NULL, 'E2E Debe por total'
        ),
        (
            v_company_id, v_template_id, 2, v_credit_account_id, v_cost_center_id,
            NULL, '{total}', 'E2E Haber por total'
        );

    RAISE NOTICE 'Plantilla E2E lista. template_id=%, company_id=%, module=%, document_type=%',
        v_template_id, v_company_id, v_doc_module, v_doc_code;
END
$$;

SELECT
    h.template_id,
    h.company_id,
    h.module,
    h.document_type,
    h.name,
    h.is_active
FROM public.con_plantilla_partida_hdr h
JOIN tmp_con_seed_tpl_params p
  ON p.company_id = h.company_id
WHERE upper(btrim(h.name)) = upper(btrim(p.template_name))
ORDER BY h.template_id DESC
LIMIT 5;

SELECT
    d.template_id,
    d.line_number,
    a.code AS account_code,
    a.name AS account_name,
    d.debit_formula,
    d.credit_formula,
    d.description
FROM public.con_plantilla_partida_dtl d
JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
JOIN tmp_con_seed_tpl_params p ON p.company_id = d.company_id
WHERE d.template_id = (
    SELECT h.template_id
    FROM public.con_plantilla_partida_hdr h
    WHERE h.company_id = p.company_id
      AND upper(btrim(h.name)) = upper(btrim(p.template_name))
    ORDER BY h.template_id DESC
    LIMIT 1
)
ORDER BY d.line_number;
