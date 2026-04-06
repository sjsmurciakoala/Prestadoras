BEGIN;

DROP FUNCTION IF EXISTS public.rep_balance_comprobacion(bigint, date, date, boolean);

CREATE OR REPLACE FUNCTION public.rep_balance_comprobacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_incluir_sin_movimiento boolean DEFAULT false
)
RETURNS TABLE
(
    rubro_orden integer,
    rubro_nombre text,
    cuenta_id bigint,
    cuenta_padre_id bigint,
    cuenta_codigo character varying(30),
    cuenta_nombre text,
    cuenta_nombre_mostrar text,
    tipo_cuenta character varying(30),
    categoria character varying(30),
    nivel smallint,
    permite_movimiento boolean,
    tiene_hijos boolean,
    saldo_anterior numeric(18,2),
    saldo_anterior_deudor numeric(18,2),
    saldo_anterior_acreedor numeric(18,2),
    debitos_periodo numeric(18,2),
    creditos_periodo numeric(18,2),
    saldo_actual numeric(18,2),
    saldo_actual_deudor numeric(18,2),
    saldo_actual_acreedor numeric(18,2)
)
LANGUAGE plpgsql
STABLE
AS
$$
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
    WHERE p_incluir_sin_movimiento
       OR abs(s.prev_balance) > 0.004
       OR abs(s.period_debits) > 0.004
       OR abs(s.period_credits) > 0.004
       OR abs(s.current_balance) > 0.004
    ORDER BY
        rubro_orden,
        s.code;
END;
$$;

COMMENT ON FUNCTION public.rep_balance_comprobacion(bigint, date, date, boolean)
IS 'Balance de comprobacion de saldos para reporteria web y estructura financiera base conforme al manual ERSAPS.';

INSERT INTO public.rep_catalogo_dataset
(
    company_id,
    codigo,
    nombre,
    descripcion,
    tipo_origen,
    origen_clave,
    sql_text,
    connection_name,
    is_active,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    c.company_id,
    'balance-comprobacion',
    'Balance de comprobacion',
    'Dataset base del balance de comprobacion para reporteria financiera ERSAPS.',
    'STORED_PROCEDURE',
    'public.rep_balance_comprobacion',
    NULL,
    'DefaultConnection',
    TRUE,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.cfg_company c
WHERE NOT EXISTS
(
    SELECT 1
    FROM public.rep_catalogo_dataset d
    WHERE d.company_id = c.company_id
      AND d.codigo = 'balance-comprobacion'
);

UPDATE public.rep_catalogo_dataset
SET
    nombre = 'Balance de comprobacion',
    descripcion = 'Dataset base del balance de comprobacion para reporteria financiera ERSAPS.',
    tipo_origen = 'STORED_PROCEDURE',
    origen_clave = 'public.rep_balance_comprobacion',
    sql_text = NULL,
    connection_name = 'DefaultConnection',
    is_active = TRUE,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
WHERE codigo = 'balance-comprobacion';

INSERT INTO public.rep_dataset_parametro
(
    company_id,
    dataset_id,
    nombre,
    nombre_origen,
    etiqueta,
    tipo_dato,
    fuente_valor,
    valor_default,
    visible,
    permite_nulo,
    requerido,
    orden,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    d.company_id,
    d.dataset_id,
    p.nombre,
    p.nombre_origen,
    p.etiqueta,
    p.tipo_dato,
    p.fuente_valor,
    p.valor_default,
    p.visible,
    p.permite_nulo,
    p.requerido,
    p.orden,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
JOIN
(
    VALUES
        ('CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 20),
        ('IncluirSinMovimiento', 'p_incluir_sin_movimiento', 'Incluir cuentas sin movimiento', 'BOOLEAN', 'REPORT', 'false', TRUE, FALSE, FALSE, 30)
) AS p(nombre, nombre_origen, etiqueta, tipo_dato, fuente_valor, valor_default, visible, permite_nulo, requerido, orden)
  ON true
WHERE d.codigo = 'balance-comprobacion'
  AND NOT EXISTS
  (
      SELECT 1
      FROM public.rep_dataset_parametro x
      WHERE x.company_id = d.company_id
        AND x.dataset_id = d.dataset_id
        AND x.nombre = p.nombre
  );

UPDATE public.rep_dataset_parametro p
SET
    nombre_origen = v.nombre_origen,
    etiqueta = v.etiqueta,
    tipo_dato = v.tipo_dato,
    fuente_valor = v.fuente_valor,
    valor_default = v.valor_default,
    visible = v.visible,
    permite_nulo = v.permite_nulo,
    requerido = v.requerido,
    orden = v.orden,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
JOIN
(
    VALUES
        ('CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 20),
        ('IncluirSinMovimiento', 'p_incluir_sin_movimiento', 'Incluir cuentas sin movimiento', 'BOOLEAN', 'REPORT', 'false', TRUE, FALSE, FALSE, 30)
) AS v(nombre, nombre_origen, etiqueta, tipo_dato, fuente_valor, valor_default, visible, permite_nulo, requerido, orden)
  ON true
WHERE d.company_id = p.company_id
  AND d.dataset_id = p.dataset_id
  AND d.codigo = 'balance-comprobacion'
  AND p.nombre = v.nombre;

COMMIT;

-- Verificacion rapida
SELECT
    d.company_id,
    d.codigo,
    d.nombre,
    d.tipo_origen,
    d.origen_clave,
    p.nombre AS parametro,
    p.fuente_valor,
    p.tipo_dato
FROM public.rep_catalogo_dataset d
LEFT JOIN public.rep_dataset_parametro p
  ON p.company_id = d.company_id
 AND p.dataset_id = d.dataset_id
WHERE d.codigo = 'balance-comprobacion'
ORDER BY d.company_id, p.orden, p.nombre;

SELECT *
FROM public.rep_balance_comprobacion(
    COALESCE(
        (
            SELECT company_id
            FROM public.cfg_company
            ORDER BY company_id
            LIMIT 1
        ),
        0
    ),
    date_trunc('month', CURRENT_DATE)::date,
    CURRENT_DATE,
    false
)
LIMIT 100;
