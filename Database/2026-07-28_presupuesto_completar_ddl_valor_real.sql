-- 2026-07-28_presupuesto_completar_ddl_valor_real.sql
-- Completa las piezas del mecanismo de afectación de valor_real de presupuesto que
-- el ddl_v3 `20260227_presupuesto_valor_real_triggers.sql` define pero que NUNCA se
-- aplicaron (verificado 2026-07-28 en mirror siad_v3_restore y en siad_v3_desarrollo:
-- no existen). El script multitenant `2026-07-24_presupuesto_multitenant_company_id.sql`
-- (paso 14) se escribió ASUMIENDO que ese ddl_v3 ya estaba aplicado: recreó
-- fn_pst_aplicar_delta_valor_real (que llama a fn_pst_resolver_cuenta_code) y dejó a
-- OrdenesPagoDirectoService.ApplyPresupuestoPartidaAsync dependiendo de
-- sp_pst_aplicar_partida_presupuesto, pero ninguna de esas dependencias existe.
--
-- QUÉ CREA (las 4 piezas faltantes, tal cual del ddl_v3 — ya son company-safe):
--   * fn_pst_resolver_cuenta_code(bigint,bigint)        — resuelve el code de la cuenta; filtra por company_id.
--   * fn_pst_resolver_poliza_fecha(text,bigint,bigint)  — resuelve la fecha de la póliza; filtra por company_id.
--   * fn_pst_aplicar_delta_por_poliza(text,bigint,bigint,bigint,numeric)
--                                                        — envuelve resolver_poliza_fecha + aplicar_delta_valor_real.
--   * sp_pst_aplicar_partida_presupuesto(bigint,date,tipo_linea_partida[])
--                                                        — recorre líneas y aplica el delta por cuenta.
--   Las dos últimas invocan fn_pst_aplicar_delta_valor_real(bigint,bigint,date,numeric),
--   que es la versión COMPANY-AWARE del paso 14: se respeta el multitenant.
--
-- QUÉ **NO** INCLUYE del ddl_v3 (a propósito, para no revertir el paso 14):
--   * fn_pst_recalcular_valor_disponible(varchar)  [1 arg, mono-tenant]  -> el paso 14 tiene la de (bigint,varchar).
--   * fn_pst_aplicar_delta_valor_real  [cuerpo sin filtro company]        -> el paso 14 tiene la company-aware.
--     Reponer cualquiera de esas dos pisaría el paso 14 y reintroduciría fuga entre empresas.
--   * ADD COLUMN valor_disponible + backfill (ddl_v3 líneas 4-29)         -> ya existen.
--
-- PRERREQUISITOS:
--   * Paso 14 aplicado (firma company-aware de fn_pst_aplicar_delta_valor_real). Este script NO la crea.
--   * El tipo compuesto public.tipo_linea_partida debe existir (lo usa el procedimiento).
--   * Debe existir public.con_plan_cuentas (o con_plan_cuenta) con columnas code/account_id/allows_budget/company_id.
--
-- Cambio: solo objetos de código (funciones + 1 procedimiento). No crea/altera tablas,
-- no toca datos, no crea triggers (el mecanismo se invoca explícitamente desde SPs / servicio).
-- IDEMPOTENTE y re-ejecutable (CREATE OR REPLACE en todo). Transaccional.

BEGIN;

-- Guarda dura: si falta el tipo compuesto, abortar con un mensaje claro en vez de un error
-- oscuro en el CREATE del procedimiento.
DO $guard$
BEGIN
    IF to_regtype('public.tipo_linea_partida') IS NULL THEN
        RAISE EXCEPTION 'Falta el tipo public.tipo_linea_partida; aplicá primero el ddl_v3 que lo define. No se crea sp_pst_aplicar_partida_presupuesto.';
    END IF;
    IF to_regprocedure('public.fn_pst_aplicar_delta_valor_real(bigint,bigint,date,numeric)') IS NULL THEN
        RAISE EXCEPTION 'Falta fn_pst_aplicar_delta_valor_real company-aware (paso 14). Aplicá 2026-07-24_presupuesto_multitenant_company_id.sql antes que este script.';
    END IF;
END;
$guard$;

-- ============================================================
-- 1) Resolver el code de la cuenta (filtra por empresa)
-- ============================================================
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

-- ============================================================
-- 2) Resolver la fecha de la póliza (filtra por empresa)
-- ============================================================
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

-- ============================================================
-- 3) Aplicar delta por póliza (envuelve fecha + delta company-aware)
-- ============================================================
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

-- ============================================================
-- 4) Procedimiento de aplicación por partida (lo llama OrdenesPagoDirectoService)
--    Usa fn_pst_aplicar_delta_valor_real (company-aware, paso 14).
-- ============================================================
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

-- ============================================================
-- 5) Limpieza de triggers de la implementación vieja (basada en disparadores).
--    Inofensivo (IF EXISTS): hoy no existen. Deja el estado idéntico al del ddl_v3.
-- ============================================================
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

COMMIT;

-- =============================================================================
-- ¿YA APLICADO? / VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- Las 4 piezas deben existir (ninguna NULL):
-- SELECT to_regprocedure('public.fn_pst_resolver_cuenta_code(bigint,bigint)')                         AS resolver_cuenta,
--        to_regprocedure('public.fn_pst_resolver_poliza_fecha(text,bigint,bigint)')                  AS resolver_fecha,
--        to_regprocedure('public.fn_pst_aplicar_delta_por_poliza(text,bigint,bigint,bigint,numeric)') AS delta_por_poliza,
--        to_regprocedure('public.sp_pst_aplicar_partida_presupuesto(bigint,date,public.tipo_linea_partida[])') AS sp_partida;
--
-- El paso 14 NO debe haberse tocado (estas dos deben seguir company-aware):
-- SELECT pg_get_function_identity_arguments(p.oid) AS firma, p.proname
--   FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
--  WHERE n.nspname='public'
--    AND p.proname IN ('fn_pst_aplicar_delta_valor_real','fn_pst_recalcular_valor_disponible')
--  ORDER BY p.proname;
--   -> aplicar_delta_valor_real: 'p_company_id bigint, p_account_id bigint, p_poliza_date date, p_delta numeric'
--   -> recalcular_valor_disponible: 'p_company_id bigint, p_id_presupuesto character varying'  (una sola fila; NO debe aparecer la de (varchar))
--
-- Resolución de code (empresa 2, una cuenta con allows_budget=TRUE) devuelve su code, no NULL:
-- SELECT public.fn_pst_resolver_cuenta_code(2::bigint, <account_id>::bigint);
-- =============================================================================
