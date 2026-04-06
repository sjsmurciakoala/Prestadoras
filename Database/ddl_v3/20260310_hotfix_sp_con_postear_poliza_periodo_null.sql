-- ============================================================
-- HOTFIX: sp_con_postear_poliza resuelve period_id cuando viene NULL
-- Fecha: 2026-03-10
-- Motivo:
--   Polizas manuales DRAFT creadas sin period_id fallaban al postear con:
--   "No hay periodo abierto para poliza ... (periodo <NULL>, fecha ...)"
-- ============================================================

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
