-- Regla presupuestaria para créditos en partidas.
--
-- Retornos:
--   1 = aplicado, revertido o no aplica (sin bloqueo)
--   0 = bloqueado porque (saldo_real + credito) > saldo_proyectado
--   2 = bloqueado porque existe presupuesto vigente sin aprobar para una afectacion positiva

ALTER TABLE IF EXISTS public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS valor_disponible NUMERIC(18,4) NOT NULL DEFAULT 0;

CREATE OR REPLACE FUNCTION public.fn_pst_afectar_saldo_real_credito(
    p_company_id bigint,
    p_account_id bigint,
    p_poliza_date date,
    p_credito numeric
)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
    v_account_code varchar(30);
    v_allows_budget boolean := false;
    v_id_presupuesto varchar(10);
    v_saldo_real numeric(18,4);
    v_saldo_proyectado numeric(18,4);
    v_estado_aprobado boolean := false;
    v_nuevo_saldo_real numeric(18,4);
BEGIN
    IF p_account_id IS NULL
       OR p_poliza_date IS NULL
       OR COALESCE(p_credito, 0) = 0 THEN
        RETURN 1;
    END IF;

    IF to_regclass('public.con_plan_cuenta') IS NOT NULL THEN
        SELECT c.code,
               COALESCE(c.allows_budget, false)
          INTO v_account_code,
               v_allows_budget
          FROM public.con_plan_cuenta c
         WHERE c.account_id = p_account_id
           AND (p_company_id IS NULL OR c.company_id = p_company_id)
         LIMIT 1;
    ELSIF to_regclass('public.con_plan_cuentas') IS NOT NULL THEN
        SELECT c.code,
               COALESCE(c.allows_budget, false)
          INTO v_account_code,
               v_allows_budget
          FROM public.con_plan_cuentas c
         WHERE c.account_id = p_account_id
           AND (p_company_id IS NULL OR c.company_id = p_company_id)
         LIMIT 1;
    ELSE
        RETURN 1;
    END IF;

    IF v_account_code IS NULL OR NOT v_allows_budget THEN
        RETURN 1;
    END IF;

    SELECT d.id_presupuesto,
           COALESCE(d.valor_real, 0),
           COALESCE(d.valor_proyeccion, 0),
           COALESCE(h.estado_aprobado, FALSE)
      INTO v_id_presupuesto,
           v_saldo_real,
           v_saldo_proyectado,
           v_estado_aprobado
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.id_presupuesto = d.id_presupuesto
     WHERE upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code))
       AND p_poliza_date BETWEEN h.fecha_inicia AND h.fecha_finaliza
     ORDER BY h.fecha_inicia DESC,
              h.id_presupuesto DESC
     LIMIT 1
     FOR UPDATE OF d;

    IF NOT FOUND THEN
        RETURN 1;
    END IF;

    IF p_credito > 0 AND NOT v_estado_aprobado THEN
        RETURN 2;
    END IF;

    IF p_credito > 0 AND v_saldo_real + p_credito > v_saldo_proyectado THEN
        RETURN 0;
    END IF;

    v_nuevo_saldo_real := COALESCE(v_saldo_real, 0) + p_credito;
    IF v_nuevo_saldo_real < 0 THEN
        v_nuevo_saldo_real := 0;
    END IF;

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_real = v_nuevo_saldo_real,
           valor_disponible = GREATEST(COALESCE(d.valor_proyeccion, 0) - v_nuevo_saldo_real, 0)
     WHERE d.id_presupuesto = v_id_presupuesto
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code));

    RETURN 1;
END;
$$;


CREATE OR REPLACE FUNCTION public.fn_pst_afectar_saldo_real_credito_por_poliza(
    p_header_table text,
    p_company_id bigint,
    p_poliza_id bigint,
    p_account_id bigint,
    p_credito numeric
)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
    v_poliza_date date;
    v_sql text;
BEGIN
    IF p_header_table NOT IN ('con_partida_hdr', 'con_poliza')
       OR p_poliza_id IS NULL THEN
        RETURN 1;
    END IF;

    IF to_regclass('public.' || p_header_table) IS NULL THEN
        RETURN 1;
    END IF;

    v_sql := format(
        'SELECT h.poliza_date::date
           FROM public.%I h
          WHERE h.poliza_id = $1
            AND ($2 IS NULL OR h.company_id = $2)
          LIMIT 1',
        p_header_table
    );

    EXECUTE v_sql
       INTO v_poliza_date
      USING p_poliza_id, p_company_id;

    IF v_poliza_date IS NULL THEN
        RETURN 1;
    END IF;

    RETURN public.fn_pst_afectar_saldo_real_credito(
        p_company_id,
        p_account_id,
        v_poliza_date,
        p_credito
    );
END;
$$;





