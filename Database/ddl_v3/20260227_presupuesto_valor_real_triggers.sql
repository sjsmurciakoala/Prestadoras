-- Sincroniza pst_config_presupuesto_dtl.valor_real sin usar disparadores.
-- El presupuesto se afecta explicitamente desde procedimientos almacenados.

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS valor_disponible NUMERIC(18,4) NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS valor_disponible NUMERIC(18,4) NOT NULL DEFAULT 0;

UPDATE public.pst_config_presupuesto_dtl
   SET valor_disponible = GREATEST(
       COALESCE(valor_proyeccion, 0) - COALESCE(valor_real, 0),
       0
   );

WITH totales AS (
    SELECT d.id_presupuesto,
           COALESCE(SUM(d.valor_real), 0) AS valor_real_total
      FROM public.pst_config_presupuesto_dtl d
     GROUP BY d.id_presupuesto
)
UPDATE public.pst_config_presupuesto_hdr h
   SET valor_disponible = GREATEST(COALESCE(h.valor_global, 0) - COALESCE(t.valor_real_total, 0), 0)
  FROM totales t
 WHERE h.id_presupuesto = t.id_presupuesto;

UPDATE public.pst_config_presupuesto_hdr
   SET valor_disponible = COALESCE(valor_global, 0)
 WHERE valor_disponible IS NULL;

COMMENT ON COLUMN public.pst_config_presupuesto_dtl.valor_disponible IS
    'Saldo disponible del detalle presupuestario: valor_proyeccion menos valor_real, sin permitir valores negativos.';

CREATE OR REPLACE FUNCTION public.fn_pst_resolver_cuenta_code(
    p_company_id bigint,
    p_account_id bigint
)
RETURNS varchar
LANGUAGE plpgsql
AS $$
DECLARE
    v_code varchar(30);
    v_has_company boolean := false;
    v_sql text;
BEGIN
    IF p_account_id IS NULL THEN
        RETURN NULL;
    END IF;

    IF to_regclass('public.con_plan_cuentas') IS NOT NULL THEN
        SELECT EXISTS (
            SELECT 1
              FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name = 'con_plan_cuentas'
               AND column_name = 'company_id'
        )
        INTO v_has_company;

        v_sql := 'SELECT c.code FROM public.con_plan_cuentas c WHERE c.account_id = $1 AND COALESCE(c.allows_budget, FALSE) = TRUE';
    ELSIF to_regclass('public.con_plan_cuenta') IS NOT NULL THEN
        SELECT EXISTS (
            SELECT 1
              FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name = 'con_plan_cuenta'
               AND column_name = 'company_id'
        )
        INTO v_has_company;

        v_sql := 'SELECT c.code FROM public.con_plan_cuenta c WHERE c.account_id = $1 AND COALESCE(c.allows_budget, FALSE) = TRUE';
    ELSE
        RETURN NULL;
    END IF;

    IF v_has_company THEN
        v_sql := v_sql || ' AND ($2 IS NULL OR c.company_id = $2)';
    ELSE
        v_sql := v_sql || ' AND ($2 IS NULL OR TRUE)';
    END IF;

    v_sql := v_sql || ' LIMIT 1';

    EXECUTE v_sql
       INTO v_code
      USING p_account_id, p_company_id;

    RETURN NULLIF(btrim(v_code), '');
END;
$$;

CREATE OR REPLACE FUNCTION public.fn_pst_resolver_poliza_fecha(
    p_header_table text,
    p_company_id bigint,
    p_poliza_id bigint
)
RETURNS date
LANGUAGE plpgsql
AS $$
DECLARE
    v_poliza_date date;
    v_has_company boolean := false;
    v_sql text;
BEGIN
    IF p_poliza_id IS NULL THEN
        RETURN NULL;
    END IF;

    IF p_header_table NOT IN ('con_partida_hdr', 'con_poliza') THEN
        RETURN NULL;
    END IF;

    IF to_regclass('public.' || p_header_table) IS NULL THEN
        RETURN NULL;
    END IF;

    SELECT EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = p_header_table
           AND column_name = 'company_id'
    )
    INTO v_has_company;

    v_sql := format(
        'SELECT h.poliza_date::date FROM public.%I h WHERE h.poliza_id = $1',
        p_header_table
    );

    IF v_has_company THEN
        v_sql := v_sql || ' AND ($2 IS NULL OR h.company_id = $2)';
    ELSE
        v_sql := v_sql || ' AND ($2 IS NULL OR TRUE)';
    END IF;

    v_sql := v_sql || ' LIMIT 1';

    EXECUTE v_sql
       INTO v_poliza_date
      USING p_poliza_id, p_company_id;

    RETURN v_poliza_date;
END;
$$;

CREATE OR REPLACE FUNCTION public.fn_pst_recalcular_valor_disponible(
    p_id_presupuesto varchar
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_total_real numeric(18, 4);
BEGIN
    IF p_id_presupuesto IS NULL OR btrim(p_id_presupuesto) = '' THEN
        RETURN;
    END IF;

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_disponible = GREATEST(
           COALESCE(d.valor_proyeccion, 0) - COALESCE(d.valor_real, 0),
           0
       )
     WHERE d.id_presupuesto = p_id_presupuesto;

    SELECT COALESCE(SUM(d.valor_real), 0)
      INTO v_total_real
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.id_presupuesto = p_id_presupuesto;

    UPDATE public.pst_config_presupuesto_hdr h
       SET valor_disponible = GREATEST(COALESCE(h.valor_global, 0) - v_total_real, 0)
     WHERE h.id_presupuesto = p_id_presupuesto;
END;
$$;

CREATE OR REPLACE FUNCTION public.fn_pst_aplicar_delta_valor_real(
    p_company_id bigint,
    p_account_id bigint,
    p_poliza_date date,
    p_delta numeric
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_cuenta_code varchar(30);
    v_presupuestos_afectados varchar(10)[];
    i integer;
BEGIN
    IF p_delta IS NULL OR p_delta = 0 THEN
        RETURN;
    END IF;

    IF p_poliza_date IS NULL OR p_account_id IS NULL THEN
        RETURN;
    END IF;

    v_cuenta_code := public.fn_pst_resolver_cuenta_code(p_company_id, p_account_id);

    IF v_cuenta_code IS NULL THEN
        RETURN;
    END IF;

    WITH updated_rows AS (
        UPDATE public.pst_config_presupuesto_dtl d
           SET valor_real = GREATEST(COALESCE(d.valor_real, 0) + p_delta, 0),
               valor_disponible = GREATEST(
                   COALESCE(d.valor_proyeccion, 0) - GREATEST(COALESCE(d.valor_real, 0) + p_delta, 0),
                   0
               )
         FROM public.pst_config_presupuesto_hdr h
         WHERE h.id_presupuesto = d.id_presupuesto
           AND upper(btrim(d.con_cuenta_code)) = upper(v_cuenta_code)
           AND COALESCE(h.estado_aprobado, FALSE) = TRUE
           AND p_poliza_date BETWEEN h.fecha_inicia AND h.fecha_finaliza
        RETURNING d.id_presupuesto
    )
    SELECT array_agg(DISTINCT u.id_presupuesto)
      INTO v_presupuestos_afectados
      FROM updated_rows u;

    IF v_presupuestos_afectados IS NULL OR array_length(v_presupuestos_afectados, 1) IS NULL THEN
        RETURN;
    END IF;

    FOR i IN 1..array_length(v_presupuestos_afectados, 1) LOOP
        PERFORM public.fn_pst_recalcular_valor_disponible(v_presupuestos_afectados[i]);
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION public.fn_pst_aplicar_delta_por_poliza(
    p_header_table text,
    p_company_id bigint,
    p_poliza_id bigint,
    p_account_id bigint,
    p_delta numeric
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_poliza_date date;
BEGIN
    IF p_delta IS NULL OR p_delta = 0 THEN
        RETURN;
    END IF;

    v_poliza_date := public.fn_pst_resolver_poliza_fecha(
        p_header_table,
        p_company_id,
        p_poliza_id
    );

    PERFORM public.fn_pst_aplicar_delta_valor_real(
        p_company_id,
        p_account_id,
        v_poliza_date,
        p_delta
    );
END;
$$;

CREATE OR REPLACE PROCEDURE public.sp_pst_aplicar_partida_presupuesto(
    IN p_company_id bigint,
    IN p_poliza_date date,
    IN p_lineas public.tipo_linea_partida[]
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_linea public.tipo_linea_partida;
    v_delta numeric(28, 4);
BEGIN
    IF p_poliza_date IS NULL OR p_lineas IS NULL OR array_length(p_lineas, 1) IS NULL THEN
        RETURN;
    END IF;

    FOREACH v_linea IN ARRAY p_lineas LOOP
        IF v_linea.account_id IS NULL THEN
            CONTINUE;
        END IF;

        v_delta := COALESCE(v_linea.debit_amount, 0) - COALESCE(v_linea.credit_amount, 0);

        PERFORM public.fn_pst_aplicar_delta_valor_real(
            p_company_id,
            v_linea.account_id,
            p_poliza_date,
            v_delta
        );
    END LOOP;
END;
$$;

DO $$
BEGIN
    IF to_regclass('public.con_partida_dtl') IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER IF EXISTS trg_pst_presupuesto_con_partida_dtl ON public.con_partida_dtl';
    END IF;

    IF to_regclass('public.con_partida_hdr') IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER IF EXISTS trg_pst_presupuesto_con_partida_hdr_fecha ON public.con_partida_hdr';
    END IF;

    IF to_regclass('public.con_poliza_linea') IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER IF EXISTS trg_pst_presupuesto_con_poliza_linea ON public.con_poliza_linea';
    END IF;

    IF to_regclass('public.con_poliza') IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER IF EXISTS trg_pst_presupuesto_con_poliza_fecha ON public.con_poliza';
    END IF;
END;
$$;
