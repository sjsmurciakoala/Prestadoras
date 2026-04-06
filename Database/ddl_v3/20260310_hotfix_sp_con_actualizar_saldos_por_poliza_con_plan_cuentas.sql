-- ============================================================
-- Hotfix: referencia correcta a tabla de cuentas contables
-- Fecha: 2026-03-10
-- Motivo:
--   sp_con_actualizar_saldos_por_poliza hacia JOIN con_plan_cuenta (singular),
--   pero el esquema usa con_plan_cuentas (plural).
-- ============================================================

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

