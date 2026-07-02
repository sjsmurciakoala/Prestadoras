DROP FUNCTION IF EXISTS public.rep_balance_comprobacion(bigint, date, date, boolean);

CREATE FUNCTION public.rep_balance_comprobacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_incluir_sin_movimiento boolean DEFAULT false
)
RETURNS TABLE(
    empresa_id bigint,
    empresa_codigo text,
    empresa_nombre text,
    empresa_nombre_legal text,
    empresa_rtn text,
    empresa_email text,
    empresa_telefono text,
    empresa_direccion text,
    rubro_orden integer,
    rubro_nombre text,
    cuenta_id bigint,
    cuenta_padre_id bigint,
    cuenta_codigo character varying,
    cuenta_nombre text,
    cuenta_nombre_mostrar text,
    tipo_cuenta character varying,
    categoria character varying,
    nivel smallint,
    permite_movimiento boolean,
    tiene_hijos boolean,
    saldo_anterior numeric,
    saldo_anterior_deudor numeric,
    saldo_anterior_acreedor numeric,
    debitos_periodo numeric,
    creditos_periodo numeric,
    saldo_actual numeric,
    saldo_actual_deudor numeric,
    saldo_actual_acreedor numeric
)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;

    IF p_fecha_desde IS NULL OR p_fecha_hasta IS NULL THEN
        RAISE EXCEPTION 'Los parametros p_fecha_desde y p_fecha_hasta son obligatorios.';
    END IF;

    IF p_fecha_hasta < p_fecha_desde THEN
        RAISE EXCEPTION 'La fecha hasta no puede ser menor que la fecha desde.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.cfg_company co
        WHERE co.company_id = p_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%.', p_company_id;
    END IF;

    RETURN QUERY
    WITH RECURSIVE plan AS
    (
        SELECT
            c.account_id,
            c.parent_account_id,
            c.code,
            c.name,
            c.account_type,
            c.category,
            c.level,
            c.allows_posting,
            c.status
        FROM public.con_plan_cuentas c
        WHERE c.company_id = p_company_id
          AND COALESCE(upper(c.status), 'ACTIVO') NOT IN ('INACTIVO', 'INACTIVE')
    ),
    empresa AS
    (
        SELECT
            co.company_id AS empresa_id,
            co.code::text AS empresa_codigo,
            co.commercial_name::text AS empresa_nombre,
            co.legal_name::text AS empresa_nombre_legal,
            ec.id_fiscal_valor::text AS empresa_rtn,
            co.email::text AS empresa_email,
            co.phone::text AS empresa_telefono,
            co.address::text AS empresa_direccion
        FROM public.cfg_company co
        LEFT JOIN public.con_empresa_configuracion ec
          ON ec.company_id = co.company_id
        WHERE co.company_id = p_company_id
        LIMIT 1
    ),
    descendants AS
    (
        SELECT
            p.account_id AS ancestor_id,
            p.account_id AS descendant_id
        FROM plan p

        UNION ALL

        SELECT
            d.ancestor_id,
            c.account_id AS descendant_id
        FROM descendants d
        JOIN plan c
          ON c.parent_account_id = d.descendant_id
    ),
    movimientos_base AS
    (
        SELECT
            d.account_id,
            COALESCE(SUM(CASE WHEN h.poliza_date::date < p_fecha_desde THEN d.debit_amount ELSE 0 END), 0)::numeric(18,2) AS prev_debits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date < p_fecha_desde THEN d.credit_amount ELSE 0 END), 0)::numeric(18,2) AS prev_credits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date >= p_fecha_desde AND h.poliza_date::date <= p_fecha_hasta THEN d.debit_amount ELSE 0 END), 0)::numeric(18,2) AS period_debits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date >= p_fecha_desde AND h.poliza_date::date <= p_fecha_hasta THEN d.credit_amount ELSE 0 END), 0)::numeric(18,2) AS period_credits
        FROM public.con_partida_hdr h
        JOIN public.con_partida_dtl d
          ON d.company_id = h.company_id
         AND d.poliza_id = h.poliza_id
        WHERE h.company_id = p_company_id
          AND h.status = 1
          AND h.poliza_date::date <= p_fecha_hasta
        GROUP BY d.account_id
    ),
    movimientos_agregados AS
    (
        SELECT
            d.ancestor_id AS account_id,
            COALESCE(SUM(m.prev_debits), 0)::numeric(18,2) AS prev_debits,
            COALESCE(SUM(m.prev_credits), 0)::numeric(18,2) AS prev_credits,
            COALESCE(SUM(m.period_debits), 0)::numeric(18,2) AS period_debits,
            COALESCE(SUM(m.period_credits), 0)::numeric(18,2) AS period_credits
        FROM descendants d
        LEFT JOIN movimientos_base m
          ON m.account_id = d.descendant_id
        GROUP BY d.ancestor_id
    ),
    saldos AS
    (
        SELECT
            p.account_id,
            p.parent_account_id,
            p.code,
            p.name,
            p.account_type,
            p.category,
            p.level,
            p.allows_posting,
            EXISTS(
                SELECT 1
                FROM plan h
                WHERE h.parent_account_id = p.account_id
            ) AS has_children,
            COALESCE(m.prev_debits, 0)::numeric(18,2) AS prev_debits,
            COALESCE(m.prev_credits, 0)::numeric(18,2) AS prev_credits,
            COALESCE(m.period_debits, 0)::numeric(18,2) AS period_debits,
            COALESCE(m.period_credits, 0)::numeric(18,2) AS period_credits,
            ROUND(COALESCE(m.prev_debits, 0) - COALESCE(m.prev_credits, 0), 2) AS prev_balance,
            ROUND(
                (COALESCE(m.prev_debits, 0) - COALESCE(m.prev_credits, 0))
                + (COALESCE(m.period_debits, 0) - COALESCE(m.period_credits, 0)),
                2) AS current_balance
        FROM plan p
        LEFT JOIN movimientos_agregados m
          ON m.account_id = p.account_id
    )
    SELECT
        e.empresa_id,
        e.empresa_codigo,
        e.empresa_nombre,
        e.empresa_nombre_legal,
        e.empresa_rtn,
        e.empresa_email,
        e.empresa_telefono,
        e.empresa_direccion,
        CASE upper(COALESCE(s.account_type, ''))
            WHEN 'ACTIVO' THEN 10
            WHEN 'PASIVO' THEN 20
            WHEN 'CAPITAL' THEN 30
            WHEN 'INGRESO' THEN 40
            WHEN 'GASTO' THEN 50
            WHEN 'MEMORANDA' THEN 60
            ELSE 99
        END AS rubro_orden,
        CASE upper(COALESCE(s.account_type, ''))
            WHEN 'ACTIVO' THEN 'Activo'
            WHEN 'PASIVO' THEN 'Pasivo'
            WHEN 'CAPITAL' THEN 'Patrimonio'
            WHEN 'INGRESO' THEN 'Ingresos'
            WHEN 'GASTO' THEN 'Gastos'
            WHEN 'MEMORANDA' THEN 'Memoranda'
            ELSE 'Otros'
        END AS rubro_nombre,
        s.account_id AS cuenta_id,
        s.parent_account_id AS cuenta_padre_id,
        s.code AS cuenta_codigo,
        s.name::text AS cuenta_nombre,
        concat(repeat('  ', GREATEST(s.level::integer - 1, 0)), s.name)::text AS cuenta_nombre_mostrar,
        s.account_type AS tipo_cuenta,
        s.category AS categoria,
        s.level AS nivel,
        s.allows_posting AS permite_movimiento,
        s.has_children AS tiene_hijos,
        s.prev_balance AS saldo_anterior,
        CASE WHEN s.prev_balance > 0 THEN s.prev_balance ELSE 0 END AS saldo_anterior_deudor,
        CASE WHEN s.prev_balance < 0 THEN abs(s.prev_balance) ELSE 0 END AS saldo_anterior_acreedor,
        s.period_debits AS debitos_periodo,
        s.period_credits AS creditos_periodo,
        s.current_balance AS saldo_actual,
        CASE WHEN s.current_balance > 0 THEN s.current_balance ELSE 0 END AS saldo_actual_deudor,
        CASE WHEN s.current_balance < 0 THEN abs(s.current_balance) ELSE 0 END AS saldo_actual_acreedor
    FROM saldos s
    CROSS JOIN empresa e
    WHERE p_incluir_sin_movimiento
       OR abs(s.prev_balance) > 0.004
       OR abs(s.period_debits) > 0.004
       OR abs(s.period_credits) > 0.004
       OR abs(s.current_balance) > 0.004
    ORDER BY
        rubro_orden,
        s.code;
END;
$function$;

COMMENT ON FUNCTION public.rep_balance_comprobacion(bigint, date, date, boolean)
IS 'Balance de comprobacion de saldos para reporteria web con encabezado dinamico por empresa.';
