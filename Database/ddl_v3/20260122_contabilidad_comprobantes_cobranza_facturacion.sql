-- ============================================================
-- Procedimientos: Comprobantes automaticos (Cobranza/Facturacion)
-- Motor: PostgreSQL
-- Fecha: 2026-01-22
-- Notas:
--  - Usa con_plantilla_partida_hdr + con_plantilla_partida_dtl.
--  - Actualiza con_saldo_cuenta al postear o revertir.
--  - No toca tablas legacy (factura/transaccion_abonado) directamente;
--    el caller debe pasar los importes calculados (total, iva, cobrado, etc).
-- ============================================================

-- Evalua formulas simples basadas en un JSON de valores.
-- Soporta tokens {total} o total, y operaciones aritmeticas basicas.
CREATE OR REPLACE FUNCTION public.fn_con_eval_formula(
    p_formula text,
    p_values jsonb
) RETURNS numeric AS $$
DECLARE
    v_expr text;
    v_key text;
    v_val text;
    v_result numeric;
BEGIN
    IF p_formula IS NULL OR btrim(p_formula) = '' THEN
        RETURN 0;
    END IF;

    v_expr := p_formula;

    -- Reemplazar tokens {key} y key por su valor numerico.
    FOR v_key, v_val IN
        SELECT key, value::text FROM jsonb_each_text(COALESCE(p_values, '{}'::jsonb))
    LOOP
        v_expr := replace(v_expr, '{' || v_key || '}', v_val);
        v_expr := regexp_replace(v_expr, '\m' || v_key || '\M', v_val, 'g');
    END LOOP;

    v_expr := regexp_replace(v_expr, '[{}]', '', 'g');
    v_expr := btrim(v_expr);

    -- Si queda un token sin reemplazar, devolver 0.
    IF v_expr ~ '[A-Za-z_]' THEN
        RETURN 0;
    END IF;

    -- Validacion basica de expresion.
    IF v_expr !~ '^[0-9\.\+\-\*/\(\)\s]+$' THEN
        RAISE EXCEPTION 'Formula invalida: %', p_formula;
    END IF;

    EXECUTE 'SELECT (' || v_expr || ')::numeric' INTO v_result;
    RETURN COALESCE(v_result, 0);
END;
$$ LANGUAGE plpgsql;

-- Actualiza saldos contables a partir de una poliza.
CREATE OR REPLACE FUNCTION public.sp_con_actualizar_saldos_por_poliza(
    p_poliza_id bigint,
    p_sumar boolean
) RETURNS void AS $$
DECLARE
    v_company_id bigint;
    v_period_id bigint;
    r record;
    v_saldo_id bigint;
BEGIN
    SELECT company_id, period_id
      INTO v_company_id, v_period_id
      FROM public.con_partida_hdr
     WHERE poliza_id = p_poliza_id;

    IF v_period_id IS NULL THEN
        RAISE EXCEPTION 'La poliza % no tiene periodo asociado.', p_poliza_id;
    END IF;

    FOR r IN
        SELECT l.debit_amount, l.credit_amount, a.code AS codigo_cuenta
          FROM public.con_partida_dtl l
          JOIN public.con_plan_cuentas a ON a.account_id = l.account_id
         WHERE l.poliza_id = p_poliza_id
    LOOP
        SELECT saldo_id
          INTO v_saldo_id
          FROM public.con_saldo_cuenta
         WHERE company_id = v_company_id
           AND periodo_id = v_period_id
           AND codigo_cuenta = r.codigo_cuenta
           AND mes = 13
           AND tipo_transaccion = 0;

        IF NOT FOUND THEN
            INSERT INTO public.con_saldo_cuenta (
                company_id, periodo_id, codigo_cuenta, mes, tipo_transaccion,
                debitos, creditos, cantidad_debitos, cantidad_creditos, presupuesto, created_at
            ) VALUES (
                v_company_id, v_period_id, r.codigo_cuenta, 13, 0,
                0, 0, 0, 0, 0, now()
            )
            RETURNING saldo_id INTO v_saldo_id;
        END IF;

        IF p_sumar THEN
            UPDATE public.con_saldo_cuenta
               SET debitos = debitos + COALESCE(r.debit_amount, 0),
                   creditos = creditos + COALESCE(r.credit_amount, 0),
                   cantidad_debitos = cantidad_debitos + CASE WHEN COALESCE(r.debit_amount, 0) > 0 THEN 1 ELSE 0 END,
                   cantidad_creditos = cantidad_creditos + CASE WHEN COALESCE(r.credit_amount, 0) > 0 THEN 1 ELSE 0 END,
                   updated_at = now()
             WHERE saldo_id = v_saldo_id;
        ELSE
            UPDATE public.con_saldo_cuenta
               SET debitos = debitos - COALESCE(r.debit_amount, 0),
                   creditos = creditos - COALESCE(r.credit_amount, 0),
                   cantidad_debitos = cantidad_debitos - CASE WHEN COALESCE(r.debit_amount, 0) > 0 THEN 1 ELSE 0 END,
                   cantidad_creditos = cantidad_creditos - CASE WHEN COALESCE(r.credit_amount, 0) > 0 THEN 1 ELSE 0 END,
                   updated_at = now()
             WHERE saldo_id = v_saldo_id;
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- Punto unico de posteo/mayorizacion.
CREATE OR REPLACE FUNCTION public.sp_con_postear_poliza(
    p_company_id bigint,
    p_poliza_id bigint,
    p_user text
) RETURNS void AS $$
DECLARE
    v_status smallint;
    v_period_id bigint;
    v_poliza_date date;
    v_period_open_id bigint;
    v_total_debit numeric := 0;
    v_total_credit numeric := 0;
    v_has_lines boolean := false;
    v_posted_by bigint := NULL;
BEGIN
    SELECT h.status, h.period_id, h.poliza_date::date
      INTO v_status, v_period_id, v_poliza_date
      FROM public.con_partida_hdr h
     WHERE h.company_id = p_company_id
       AND h.poliza_id = p_poliza_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Poliza % no encontrada para empresa %', p_poliza_id, p_company_id;
    END IF;

    -- Idempotencia: si ya esta posteada, no hacer nada.
    IF v_status = 1 THEN
        RETURN;
    END IF;

    IF v_status <> 0 THEN
        RAISE EXCEPTION 'Solo se puede postear poliza en DRAFT (0). Estado actual: %', v_status;
    END IF;

    SELECT EXISTS (
        SELECT 1
          FROM public.con_partida_dtl d
         WHERE d.poliza_id = p_poliza_id
    )
      INTO v_has_lines;

    IF NOT v_has_lines THEN
        RAISE EXCEPTION 'La poliza % no tiene lineas contables.', p_poliza_id;
    END IF;

    SELECT COALESCE(SUM(d.debit_amount), 0),
           COALESCE(SUM(d.credit_amount), 0)
      INTO v_total_debit, v_total_credit
      FROM public.con_partida_dtl d
     WHERE d.poliza_id = p_poliza_id;

    IF abs(v_total_debit - v_total_credit) > 0.01 THEN
        RAISE EXCEPTION 'La poliza % no esta balanceada. Debe: %, Haber: %',
            p_poliza_id, v_total_debit, v_total_credit;
    END IF;

    v_poliza_date := COALESCE(v_poliza_date, current_date);

    IF v_period_id IS NULL THEN
        -- Para polizas DRAFT manuales sin periodo, resolver periodo abierto por fecha.
        SELECT p.period_id
          INTO v_period_open_id
          FROM public.con_periodo_contable p
         WHERE p.company_id = p_company_id
           AND v_poliza_date BETWEEN p.start_date::date AND p.end_date::date
          AND COALESCE(p.status_id, 2) = 0
         ORDER BY p.start_date DESC
         LIMIT 1;
    ELSE
        SELECT p.period_id
          INTO v_period_open_id
          FROM public.con_periodo_contable p
         WHERE p.company_id = p_company_id
           AND p.period_id = v_period_id
           AND v_poliza_date BETWEEN p.start_date::date AND p.end_date::date
          AND COALESCE(p.status_id, 2) = 0
         LIMIT 1;
    END IF;

    IF v_period_open_id IS NULL THEN
        RAISE EXCEPTION 'No hay periodo abierto (estado 0) para poliza % (periodo %, fecha %)',
            p_poliza_id, v_period_id, v_poliza_date;
    END IF;

    v_period_id := COALESCE(v_period_id, v_period_open_id);

    -- Auditoria de posted_by (best-effort):
    -- 1) si p_user es numerico, usarlo como user_id.
    -- 2) si no, intentar resolver desde usuarioapc.usuario -> usuarioapc.ide.
    IF p_user IS NOT NULL AND btrim(p_user) <> '' THEN
        IF btrim(p_user) ~ '^[0-9]+$' THEN
            v_posted_by := btrim(p_user)::bigint;
        ELSE
            SELECT u.ide::bigint
              INTO v_posted_by
              FROM public.usuarioapc u
             WHERE upper(btrim(COALESCE(u.usuario, ''))) = upper(btrim(p_user))
             LIMIT 1;
        END IF;
    END IF;

    UPDATE public.con_partida_hdr
       SET total_debit = v_total_debit,
           total_credit = v_total_credit,
           period_id = v_period_id,
           status = 1,
           posted_at = now(),
           posted_by = COALESCE(v_posted_by, posted_by),
           updated_at = now(),
           updated_by = COALESCE(p_user, current_user)
     WHERE poliza_id = p_poliza_id;

    PERFORM public.sp_con_actualizar_saldos_por_poliza(p_poliza_id, true);
END;
$$ LANGUAGE plpgsql;

-- Punto unico de reversa de posteo/mayorizacion.
CREATE OR REPLACE FUNCTION public.sp_con_revertir_poliza(
    p_company_id bigint,
    p_poliza_id bigint,
    p_user text
) RETURNS void AS $$
DECLARE
    v_status smallint;
BEGIN
    SELECT h.status
      INTO v_status
      FROM public.con_partida_hdr h
     WHERE h.company_id = p_company_id
       AND h.poliza_id = p_poliza_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Poliza % no encontrada para empresa %', p_poliza_id, p_company_id;
    END IF;

    -- Idempotencia: si ya esta en draft, no hacer nada.
    IF v_status = 0 THEN
        RETURN;
    END IF;

    IF v_status <> 1 THEN
        RAISE EXCEPTION 'Solo se pueden revertir polizas en estado POSTED (1). Estado actual: %', v_status;
    END IF;

    UPDATE public.con_partida_hdr
       SET status = 0,
           posted_at = NULL,
           updated_at = now(),
           updated_by = COALESCE(p_user, current_user)
     WHERE poliza_id = p_poliza_id;

    PERFORM public.sp_con_actualizar_saldos_por_poliza(p_poliza_id, false);
END;
$$ LANGUAGE plpgsql;

-- Genera comprobante contable automatico en base a plantilla.
CREATE OR REPLACE FUNCTION public.sp_con_generar_comprobante(
    p_company_id bigint,
    p_module text,
    p_document_type text,
    p_document_id bigint,
    p_document_number text,
    p_poliza_date date,
    p_description text,
    p_user text,
    p_template_id bigint DEFAULT NULL,
    p_type_id bigint DEFAULT 0,
    p_journal_id bigint DEFAULT NULL,
    p_values jsonb DEFAULT '{}'::jsonb,
    p_force boolean DEFAULT false
) RETURNS bigint AS $$
DECLARE
    v_poliza_id bigint;
    v_template_id bigint;
    v_period_id bigint;
    v_poliza_number text;
    v_seq bigint;
    v_total_debit numeric := 0;
    v_total_credit numeric := 0;
    r record;
    v_debit numeric;
    v_credit numeric;
    v_line_seq int := 0;
    v_detail record;
    v_detail_account bigint;
    v_detail_amount numeric;
    v_detail_desc text;
BEGIN
    -- Si ya existe comprobante para el documento, devolverlo (salvo force).
    IF NOT p_force THEN
        SELECT poliza_id
          INTO v_poliza_id
          FROM public.con_partida_hdr
         WHERE company_id = p_company_id
           AND module = upper(p_module)
           AND document_type = upper(p_document_type)
           AND document_id = p_document_id
         LIMIT 1;

        IF v_poliza_id IS NOT NULL THEN
            RETURN v_poliza_id;
        END IF;
    END IF;

    -- Plantilla contable.
    v_template_id := p_template_id;
    IF v_template_id IS NULL THEN
        SELECT template_id
          INTO v_template_id
          FROM public.con_plantilla_partida_hdr
         WHERE company_id = p_company_id
           AND module = upper(p_module)
           AND document_type = upper(p_document_type)
           AND is_active = true
         ORDER BY template_id DESC
         LIMIT 1;
    END IF;

    IF v_template_id IS NULL THEN
        RAISE EXCEPTION 'No existe plantilla contable para modulo %, documento %', p_module, p_document_type;
    END IF;

        -- Periodo contable.
    SELECT period_id
      INTO v_period_id
      FROM public.con_periodo_contable
     WHERE company_id = p_company_id
       AND COALESCE(
            status_id,
            COALESCE(status_id, 2)
       ) = 0
       AND p_poliza_date BETWEEN start_date::date AND end_date::date
     ORDER BY start_date DESC
     LIMIT 1;

    IF v_period_id IS NULL THEN
        RAISE EXCEPTION 'No hay periodo abierto (estado 0) para la fecha %', p_poliza_date;
    END IF;

    -- Numero de poliza (simple: empresa-anio-secuencia).
    SELECT COUNT(*) + 1
      INTO v_seq
      FROM public.con_partida_hdr
     WHERE company_id = p_company_id;

    v_poliza_number := p_company_id::text || '-' ||
                       EXTRACT(YEAR FROM p_poliza_date)::text || '-' ||
                       lpad(v_seq::text, 6, '0');

    -- Insertar encabezado.
    INSERT INTO public.con_partida_hdr (
        company_id, journal_id, period_id, template_id,
        module, document_type, document_id, document_number,
        poliza_number, sequence_number, poliza_date, description,
        status, source_reference, created_at, created_by, type_id,
        total_debit, total_credit
    ) VALUES (
        p_company_id, p_journal_id, v_period_id, v_template_id,
        upper(p_module), upper(p_document_type), p_document_id, p_document_number,
        v_poliza_number, v_seq, p_poliza_date, p_description,
        0, p_document_number, now(), COALESCE(p_user, current_user), p_type_id,
        0, 0
    )
    RETURNING poliza_id INTO v_poliza_id;

    -- Insertar lineas desde plantilla.
    FOR r IN
        SELECT line_number, account_id, cost_center_id, debit_formula, credit_formula, description,
               COALESCE(line_mode, 'FIXED') AS line_mode,
               entry_side, detail_account_field, detail_amount_field, detail_description_field
          FROM public.con_plantilla_partida_dtl
         WHERE template_id = v_template_id
         ORDER BY line_number
    LOOP
        IF r.line_mode = 'DETAIL_EXPAND' THEN
            -- Expandir una linea por cada item en p_values->'details'
            FOR v_detail IN
                SELECT value FROM jsonb_array_elements(COALESCE(p_values->'details', '[]'::jsonb))
            LOOP
                v_line_seq := v_line_seq + 1;

                v_detail_account := (v_detail.value->>COALESCE(r.detail_account_field, 'account_id'))::bigint;
                v_detail_amount := COALESCE((v_detail.value->>COALESCE(r.detail_amount_field, 'total'))::numeric, 0);
                v_detail_desc := COALESCE(
                    v_detail.value->>COALESCE(r.detail_description_field, 'description'),
                    r.description
                );

                IF v_detail_amount = 0 THEN
                    CONTINUE;
                END IF;

                IF r.entry_side = 'D' THEN
                    v_debit := v_detail_amount;
                    v_credit := 0;
                ELSE
                    v_debit := 0;
                    v_credit := v_detail_amount;
                END IF;

                INSERT INTO public.con_partida_dtl (
                    company_id, poliza_id, line_number, account_id, cost_center_id,
                    description, debit_amount, credit_amount, source_document
                ) VALUES (
                    p_company_id, v_poliza_id, v_line_seq, v_detail_account, r.cost_center_id,
                    v_detail_desc, v_debit, v_credit, p_document_number
                );

                v_total_debit := v_total_debit + v_debit;
                v_total_credit := v_total_credit + v_credit;
            END LOOP;
        ELSE
            -- Modo FIXED: comportamiento original
            v_line_seq := v_line_seq + 1;

            v_debit := public.fn_con_eval_formula(r.debit_formula, p_values);
            v_credit := public.fn_con_eval_formula(r.credit_formula, p_values);

            IF COALESCE(v_debit, 0) = 0 AND COALESCE(v_credit, 0) = 0 THEN
                CONTINUE;
            END IF;

            INSERT INTO public.con_partida_dtl (
                company_id, poliza_id, line_number, account_id, cost_center_id,
                description, debit_amount, credit_amount, source_document
            ) VALUES (
                p_company_id, v_poliza_id, v_line_seq, r.account_id, r.cost_center_id,
                r.description, COALESCE(v_debit, 0), COALESCE(v_credit, 0), p_document_number
            );

            v_total_debit := v_total_debit + COALESCE(v_debit, 0);
            v_total_credit := v_total_credit + COALESCE(v_credit, 0);
        END IF;
    END LOOP;

        -- Totales en cabecera.
    UPDATE public.con_partida_hdr
       SET total_debit = v_total_debit,
           total_credit = v_total_credit,
           updated_at = now(),
           updated_by = COALESCE(p_user, current_user)
     WHERE poliza_id = v_poliza_id;

    -- Delegar posteo al punto unico.
    PERFORM public.sp_con_postear_poliza(p_company_id, v_poliza_id, p_user);

    RETURN v_poliza_id;
END;
$$ LANGUAGE plpgsql;

-- Revierte comprobante y saldos.
CREATE OR REPLACE FUNCTION public.sp_con_revertir_comprobante(
    p_poliza_id bigint,
    p_user text
) RETURNS void AS $$
DECLARE
    v_company_id bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.con_partida_hdr
     WHERE poliza_id = p_poliza_id;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Poliza % no encontrada', p_poliza_id;
    END IF;

    PERFORM public.sp_con_revertir_poliza(v_company_id, p_poliza_id, p_user);
END;
$$ LANGUAGE plpgsql;

-- ============================================================
-- Ejemplo de uso:
-- SELECT public.sp_con_generar_comprobante(
--   1, 'VENTAS', 'REC', 123, 'REC-000123', CURRENT_DATE,
--   'Cobro recibo 000123', 'system',
--   NULL, 0, NULL,
--   '{"cobrado": 150.00, "total": 150.00, "subtotal": 130.43, "iva": 19.57}'::jsonb,
--   false
-- );
-- ============================================================



