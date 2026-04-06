-- ============================================================
-- Script operativo: Plantillas VENTAS/FAC y VENTAS/REC
-- Fecha: 2026-03-13
-- Objetivo:
--   Crear o actualizar plantillas contables activas para
--   captacion (lectora, manual, miscelaneos) usando cuentas
--   resueltas desde con_regla_integracion y cfg_document_type.
--
--   Genera DOS plantillas:
--     1) VENTAS / FAC  (facturacion)
--     2) VENTAS / REC  (recibo/cobranza)
--
-- Cada plantilla tiene dos lineas:,
--   Linea 1: Debito por {total}
--   Linea 2: Credito por {total}
--
-- Las cuentas se resuelven automaticamente desde
-- con_regla_integracion activa para el document_type
-- correspondiente.
-- ============================================================
--
-- Uso:
-- 1) Ajustar company_id y user_name en tmp_seed_params.
-- 2) Ejecutar script completo en la base de datos target.
-- 3) Verificar salida de las queries finales.
--
-- Re-ejecutable: SI (upsert por nombre de plantilla).
-- ============================================================

DROP TABLE IF EXISTS tmp_seed_params;
CREATE TEMP TABLE tmp_seed_params (
    company_id bigint NOT NULL,
    user_name text NOT NULL
);

INSERT INTO tmp_seed_params (company_id, user_name)
VALUES (1, 'jmurcia');

-- ============================================================
-- Bloque principal: genera plantillas FAC y REC
-- ============================================================
DO $$
DECLARE
    v_company_id bigint;
    v_user text;

    v_codes text[] := ARRAY['FAC', 'REC'];
    v_names text[] := ARRAY['CAPTACION VENTAS FAC', 'CAPTACION VENTAS REC'];
    v_descs text[] := ARRAY[
        'Plantilla operativa captacion - Facturacion (debe/haber por total)',
        'Plantilla operativa captacion - Recibo/Cobranza (debe/haber por total)'
    ];

    v_idx int;
    v_code text;
    v_tpl_name text;
    v_tpl_desc text;

    v_doc_type_id bigint;
    v_doc_module text;
    v_doc_code text;

    v_debit_account_id bigint;
    v_credit_account_id bigint;
    v_cost_center_id bigint;

    v_template_id bigint;
    v_created int := 0;
    v_updated int := 0;
BEGIN
    SELECT p.company_id, p.user_name
      INTO v_company_id, v_user
      FROM tmp_seed_params p
     LIMIT 1;

    FOR v_idx IN 1..array_length(v_codes, 1) LOOP
        v_code := v_codes[v_idx];
        v_tpl_name := v_names[v_idx];
        v_tpl_desc := v_descs[v_idx];

        -- 1) Resolver cfg_document_type activo para VENTAS / <code>.
        SELECT dt.document_type_id, dt.module, dt.code
          INTO v_doc_type_id, v_doc_module, v_doc_code
          FROM public.cfg_document_type dt
         WHERE dt.company_id = v_company_id
           AND dt.module = 'VENTAS'
           AND dt.code = v_code
           AND dt.is_active = true
         ORDER BY dt.document_type_id
         LIMIT 1;

        IF v_doc_type_id IS NULL THEN
            RAISE NOTICE 'SKIP: No hay cfg_document_type activo para VENTAS/% en company %. Creando document_type...', v_code, v_company_id;

            INSERT INTO public.cfg_document_type (
                company_id, module, code, name, is_active,
                created_at, created_by, updated_at, updated_by
            ) VALUES (
                v_company_id, 'VENTAS', v_code,
                CASE v_code
                    WHEN 'FAC' THEN 'Factura de Venta'
                    WHEN 'REC' THEN 'Recibo de Cobranza'
                    ELSE v_code
                END,
                true, now(), v_user, now(), v_user
            )
            RETURNING document_type_id, module, code
               INTO v_doc_type_id, v_doc_module, v_doc_code;

            RAISE NOTICE '  -> Creado cfg_document_type id=% para VENTAS/%', v_doc_type_id, v_code;
        END IF;

        -- 2) Resolver regla activa para obtener cuentas contables.
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
               WHEN upper(COALESCE(r.scenario_code, '')) IN ('FAC_NETO', 'FACTURA_NETO', 'REC_NETO', 'RECIBO_NETO') THEN 0
               ELSE 1
           END,
           r.updated_at DESC NULLS LAST,
           r.regla_id DESC
         LIMIT 1;

        IF v_debit_account_id IS NULL OR v_credit_account_id IS NULL THEN
            -- Intentar obtener cuentas desde la regla de FAC si estamos en REC.
            IF v_code = 'REC' THEN
                RAISE NOTICE '  No hay regla para REC en company %, intentando heredar de FAC...', v_company_id;
                SELECT
                    r.debit_account_id,
                    r.credit_account_id,
                    r.cost_center_id
                  INTO
                    v_debit_account_id,
                    v_credit_account_id,
                    v_cost_center_id
                  FROM public.con_regla_integracion r
                  JOIN public.cfg_document_type dt ON dt.document_type_id = r.document_type_id
                 WHERE r.company_id = v_company_id
                   AND dt.module = 'VENTAS'
                   AND dt.code = 'FAC'
                   AND r.is_active = true
                 ORDER BY r.updated_at DESC NULLS LAST, r.regla_id DESC
                 LIMIT 1;
            END IF;

            IF v_debit_account_id IS NULL OR v_credit_account_id IS NULL THEN
                RAISE NOTICE 'SKIP: No hay regla activa con cuentas para VENTAS/% en company %. Plantilla no creada.', v_code, v_company_id;
                CONTINUE;
            END IF;

            RAISE NOTICE '  -> Cuentas heredadas de FAC para REC: debit=%, credit=%', v_debit_account_id, v_credit_account_id;
        END IF;

        -- 3) Crear o actualizar cabecera de plantilla.
        SELECT h.template_id
          INTO v_template_id
          FROM public.con_plantilla_partida_hdr h
         WHERE h.company_id = v_company_id
           AND h.module = v_doc_module
           AND h.document_type = v_doc_code
           AND upper(btrim(h.name)) = upper(btrim(v_tpl_name))
         LIMIT 1;

        IF v_template_id IS NULL THEN
            INSERT INTO public.con_plantilla_partida_hdr (
                company_id, module, document_type, name, description, is_active,
                created_at, created_by, updated_at, updated_by
            ) VALUES (
                v_company_id, v_doc_module, v_doc_code, v_tpl_name, v_tpl_desc, true,
                now(), v_user, now(), v_user
            )
            RETURNING template_id INTO v_template_id;

            v_created := v_created + 1;
            RAISE NOTICE '  -> Plantilla CREADA: id=%, VENTAS/%', v_template_id, v_doc_code;
        ELSE
            UPDATE public.con_plantilla_partida_hdr
               SET description = v_tpl_desc,
                   is_active = true,
                   updated_at = now(),
                   updated_by = v_user
             WHERE template_id = v_template_id;

            v_updated := v_updated + 1;
            RAISE NOTICE '  -> Plantilla ACTUALIZADA: id=%, VENTAS/%', v_template_id, v_doc_code;
        END IF;

        -- 4) Borrar y recrear lineas de detalle.
        DELETE FROM public.con_plantilla_partida_dtl
         WHERE company_id = v_company_id
           AND template_id = v_template_id;

        INSERT INTO public.con_plantilla_partida_dtl (
            company_id, template_id, line_number, account_id, cost_center_id,
            debit_formula, credit_formula, description
        ) VALUES
            (
                v_company_id, v_template_id, 1, v_debit_account_id, NULL,
                '{total}', NULL, 'Debe por total cobrado'
            ),
            (
                v_company_id, v_template_id, 2, v_credit_account_id, v_cost_center_id,
                NULL, '{total}', 'Haber por total cobrado'
            );

        RAISE NOTICE '  -> Detalles insertados (2 lineas) para template_id=%', v_template_id;
    END LOOP;

    RAISE NOTICE '';
    RAISE NOTICE '=== Resumen: % creadas, % actualizadas ===', v_created, v_updated;
END
$$;

-- ============================================================
-- Verificacion: Cabeceras de plantillas VENTAS
-- ============================================================
SELECT
    h.template_id,
    h.company_id,
    h.module,
    h.document_type,
    h.name,
    h.description,
    h.is_active,
    h.updated_at
FROM public.con_plantilla_partida_hdr h
WHERE h.module = 'VENTAS'
  AND h.company_id = (SELECT company_id FROM tmp_seed_params LIMIT 1)
ORDER BY h.document_type, h.template_id;

-- ============================================================
-- Verificacion: Detalle de plantillas VENTAS con cuentas
-- ============================================================
SELECT
    h.template_id,
    h.document_type,
    h.name AS plantilla,
    d.line_number,
    a.code AS cuenta_codigo,
    a.name AS cuenta_nombre,
    d.debit_formula,
    d.credit_formula,
    d.description
FROM public.con_plantilla_partida_hdr h
JOIN public.con_plantilla_partida_dtl d
  ON d.template_id = h.template_id AND d.company_id = h.company_id
JOIN public.con_plan_cuentas a
  ON a.account_id = d.account_id
WHERE h.module = 'VENTAS'
  AND h.company_id = (SELECT company_id FROM tmp_seed_params LIMIT 1)
ORDER BY h.document_type, h.template_id, d.line_number;

-- Cleanup.
DROP TABLE IF EXISTS tmp_seed_params;
