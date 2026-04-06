-- ============================================================
-- TEST E2E: Flujo contable de bancos
-- Fecha: 2026-03-10
-- Objetivo:
--   Validar generacion DRAFT por ruta de bancos (sp_registrar_partida_contable),
--   posteo unico (sp_con_postear_poliza), no doble posteo y reversa exacta.
-- ============================================================
--
-- Uso:
-- 1) Si no hay catalogos de bancos, ejecutar primero:
--    20260310_seed_bancos_minimo_e2e.sql
-- 2) Ajustar parametros en tmp_con_test_bancos_params.
-- 3) Ejecutar este script completo en una misma sesion.
-- 4) Guardar evidencia de cada seccion.
--

-- ------------------------------------------------------------
-- Parametros
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_bancos_params;
CREATE TEMP TABLE tmp_con_test_bancos_params (
    company_id bigint NOT NULL,
    banco_cuenta_id bigint NULL,
    tipo_transaccion_code varchar(3) NULL,
    poliza_date date NOT NULL,
    user_name text NOT NULL,
    amount_total numeric(18,2) NOT NULL,
    revert_after_test boolean NOT NULL
);

INSERT INTO tmp_con_test_bancos_params (
    company_id,
    banco_cuenta_id,
    tipo_transaccion_code,
    poliza_date,
    user_name,
    amount_total,
    revert_after_test
) VALUES (
    1,
    NULL,
    NULL,
    CURRENT_DATE,
    'jmurcia',
    125.00,
    FALSE --- Cambiar a true para validar reversa exacta automatica
);

-- ------------------------------------------------------------
-- Resolver contexto de prueba
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_bancos_resolved;
CREATE TEMP TABLE tmp_con_test_bancos_resolved AS
WITH p AS (
    SELECT *
    FROM tmp_con_test_bancos_params
    LIMIT 1
),
banco_cuenta_resolved AS (
    SELECT
        c.banco_cuenta_id,
        c.cont_account_id AS bank_account_id,
        upper(COALESCE(NULLIF(btrim(c.currency_code), ''), 'HNL')) AS currency_code
    FROM public.ban_cuenta c
    JOIN p ON p.company_id = c.company_id
    WHERE COALESCE(c.activo, true) = true
      AND c.cont_account_id IS NOT NULL
      AND (p.banco_cuenta_id IS NULL OR c.banco_cuenta_id = p.banco_cuenta_id)
    ORDER BY c.banco_cuenta_id
    LIMIT 1
),
tipo_banco_resolved AS (
    SELECT
        upper(bt.tipo_transaccion) AS tipo_transaccion_code,
        COALESCE(NULLIF(upper(btrim(bt.entra_sale::text)), ''), 'E')::bpchar AS entra_sale
    FROM public.ban_tipos_transacciones bt
    JOIN p ON p.company_id = bt.company_id
    WHERE COALESCE(upper(bt.estado), 'ACTIVE') IN ('ACTIVE', 'ACTIVO')
      AND (
            p.tipo_transaccion_code IS NULL
         OR p.tipo_transaccion_code = ''
         OR upper(bt.tipo_transaccion) = upper(p.tipo_transaccion_code)
      )
    ORDER BY bt.ban_tipo_transaccion_id
    LIMIT 1
),
period_open AS (
    SELECT cp.period_id
    FROM public.con_periodo_contable cp
    JOIN p ON p.company_id = cp.company_id
    WHERE p.poliza_date BETWEEN cp.start_date::date AND cp.end_date::date
      AND COALESCE(cp.status_id, 2) = 0
    ORDER BY cp.start_date DESC
    LIMIT 1
),
journal_resolved AS (
    SELECT d.journal_id
    FROM public.con_diario d
    JOIN p ON p.company_id = d.company_id
    WHERE d.is_active = true
    ORDER BY
      CASE WHEN upper(COALESCE(d.code, '')) = 'BAN' THEN 0 ELSE 1 END,
      d.journal_id
    LIMIT 1
),
type_resolved AS (
    SELECT t.type_id
    FROM public.con_tipo_transaccion t
    JOIN p ON p.company_id = t.company_id
    WHERE COALESCE(
            t.status_id,
            CASE
                WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                ELSE 1
            END
          ) = 1
    ORDER BY
      CASE WHEN COALESCE(t.is_default, false) THEN 0 ELSE 1 END,
      t.type_id
    LIMIT 1
),
contra_resolved AS (
    SELECT a.account_id AS contra_account_id
    FROM public.con_plan_cuentas a
    JOIN p ON p.company_id = a.company_id
    JOIN banco_cuenta_resolved bc ON true
    WHERE COALESCE(a.allows_posting, false) = true
      AND a.account_id <> bc.bank_account_id
    ORDER BY a.account_id
    LIMIT 1
)
SELECT
    p.company_id,
    COALESCE(NULLIF(btrim(p.user_name), ''), current_user) AS user_name,
    p.poliza_date,
    p.amount_total,
    p.revert_after_test,
    bc.banco_cuenta_id,
    bc.bank_account_id,
    bc.currency_code,
    cr.contra_account_id,
    tr.tipo_transaccion_code AS document_type_used,
    tr.entra_sale,
    po.period_id,
    jr.journal_id,
    ty.type_id
FROM p
LEFT JOIN banco_cuenta_resolved bc ON true
LEFT JOIN tipo_banco_resolved tr ON true
LEFT JOIN period_open po ON true
LEFT JOIN journal_resolved jr ON true
LEFT JOIN type_resolved ty ON true
LEFT JOIN contra_resolved cr ON true;

SELECT
    company_id,
    user_name,
    banco_cuenta_id,
    bank_account_id,
    contra_account_id,
    document_type_used,
    entra_sale,
    period_id,
    journal_id,
    type_id,
    poliza_date,
    amount_total
FROM tmp_con_test_bancos_resolved;

DO $$
DECLARE
    r record;
BEGIN
    SELECT *
      INTO r
      FROM tmp_con_test_bancos_resolved
     LIMIT 1;

    IF r.banco_cuenta_id IS NULL OR r.bank_account_id IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe ban_cuenta activa con cont_account_id. Ejecutar 20260310_seed_bancos_minimo_e2e.sql.';
    END IF;

    IF r.contra_account_id IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe cuenta de contrapartida posting para company %.', r.company_id;
    END IF;

    IF r.document_type_used IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe ban_tipos_transacciones activo para company %.', r.company_id;
    END IF;

    IF r.entra_sale NOT IN ('E', 'S') THEN
        RAISE EXCEPTION 'Precheck bancos: entra_sale invalido (%).', r.entra_sale;
    END IF;

    IF r.period_id IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe periodo abierto (estado 0) para fecha %.', r.poliza_date;
    END IF;

    IF r.journal_id IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe con_diario activo para company %.', r.company_id;
    END IF;

    IF r.type_id IS NULL THEN
        RAISE EXCEPTION 'Precheck bancos: no existe con_tipo_transaccion activo para company %.', r.company_id;
    END IF;
END
$$;

-- ------------------------------------------------------------
-- Baseline de saldos (cuenta banco + contra)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_bancos_saldo_before;
CREATE TEMP TABLE tmp_con_test_bancos_saldo_before AS
WITH r AS (
    SELECT *
    FROM tmp_con_test_bancos_resolved
    LIMIT 1
),
acc AS (
    SELECT 'BANCO'::text AS rol, a.code AS codigo_cuenta
    FROM public.con_plan_cuentas a
    JOIN r ON a.account_id = r.bank_account_id
    UNION ALL
    SELECT 'CONTRA'::text AS rol, a.code AS codigo_cuenta
    FROM public.con_plan_cuentas a
    JOIN r ON a.account_id = r.contra_account_id
)
SELECT
    r.company_id,
    r.period_id,
    a.rol,
    a.codigo_cuenta,
    COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_before,
    COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_before
FROM r
JOIN acc a ON true
LEFT JOIN public.con_saldo_cuenta s
  ON s.company_id = r.company_id
 AND s.periodo_id = r.period_id
 AND s.codigo_cuenta = a.codigo_cuenta
 AND s.mes = 13
 AND s.tipo_transaccion = 0;

SELECT * FROM tmp_con_test_bancos_saldo_before ORDER BY rol;

-- ------------------------------------------------------------
-- Generar partida DRAFT por ruta de bancos y postear
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_bancos_exec_ctx;
CREATE TEMP TABLE tmp_con_test_bancos_exec_ctx AS
SELECT
    r.*,
    ('BAN-E2E-' || to_char(clock_timestamp(), 'YYYYMMDDHH24MISSMS'))::text AS document_number_used,
    ('BAN-E2E-' || to_char(clock_timestamp(), 'HH24MISSMS'))::text AS partida_number_used,
    ('TEST E2E bancos ' || to_char(clock_timestamp(), 'YYYY-MM-DD HH24:MI:SS'))::text AS description_used
FROM tmp_con_test_bancos_resolved r
LIMIT 1;

DO $$
DECLARE
    r record;
    v_lineas public.tipo_linea_partida[];
BEGIN
    SELECT *
      INTO r
      FROM tmp_con_test_bancos_exec_ctx
     LIMIT 1;

    IF r.company_id IS NULL THEN
        RAISE EXCEPTION 'No existe contexto para ejecutar la prueba E2E de bancos.';
    END IF;

    v_lineas := ARRAY[
        ROW(
            r.bank_account_id,
            NULL::bigint,
            'Banco ' || r.document_number_used,
            CASE WHEN r.entra_sale = 'E'::bpchar THEN r.amount_total ELSE 0 END,
            CASE WHEN r.entra_sale = 'S'::bpchar THEN r.amount_total ELSE 0 END,
            NULL::bigint,
            r.currency_code::bpchar,
            1::numeric
        )::public.tipo_linea_partida,
        ROW(
            r.contra_account_id,
            NULL::bigint,
            'Contra ' || r.document_number_used,
            CASE WHEN r.entra_sale = 'S'::bpchar THEN r.amount_total ELSE 0 END,
            CASE WHEN r.entra_sale = 'E'::bpchar THEN r.amount_total ELSE 0 END,
            NULL::bigint,
            r.currency_code::bpchar,
            1::numeric
        )::public.tipo_linea_partida
    ];

    CALL public.sp_registrar_partida_contable(
        r.company_id,
        r.journal_id,
        r.period_id,
        'BANCOS',
        r.document_type_used,
        r.document_number_used,
        r.partida_number_used,
        (r.poliza_date::timestamp AT TIME ZONE 'UTC'),
        r.description_used,
        r.user_name,
        r.type_id,
        v_lineas
    );
END
$$;

DROP TABLE IF EXISTS tmp_con_test_bancos_result;
CREATE TEMP TABLE tmp_con_test_bancos_result AS
WITH x AS (
    SELECT *
    FROM tmp_con_test_bancos_exec_ctx
    LIMIT 1
)
SELECT
    h.poliza_id,
    x.company_id,
    x.user_name,
    x.period_id,
    x.document_type_used,
    x.document_number_used,
    x.poliza_date,
    x.amount_total,
    x.revert_after_test
FROM x
JOIN LATERAL (
    SELECT ph.poliza_id
    FROM public.con_partida_hdr ph
    WHERE ph.company_id = x.company_id
      AND ph.module = 'BANCOS'
      AND ph.document_type = x.document_type_used
      AND btrim(ph.document_number) = btrim(x.document_number_used)
      AND ph.created_by = x.user_name
    ORDER BY ph.poliza_id DESC
    LIMIT 1
) h ON true;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM tmp_con_test_bancos_result) THEN
        RAISE EXCEPTION 'No fue posible resolver la poliza DRAFT creada por ruta de bancos.';
    END IF;
END
$$;

SELECT public.sp_con_postear_poliza(t.company_id, t.poliza_id, t.user_name)
FROM tmp_con_test_bancos_result t;

SELECT * FROM tmp_con_test_bancos_result;

-- ------------------------------------------------------------
-- Validacion de cabecera, detalle y cuadre
-- ------------------------------------------------------------
SELECT
    h.poliza_id,
    h.company_id,
    h.period_id,
    h.module,
    h.document_type,
    h.document_number,
    h.status,
    h.total_debit,
    h.total_credit,
    h.posted_by,
    h.posted_at
FROM public.con_partida_hdr h
JOIN tmp_con_test_bancos_result t ON t.poliza_id = h.poliza_id;

SELECT
    d.line_number,
    a.code AS cuenta_codigo,
    a.name AS cuenta_nombre,
    d.debit_amount,
    d.credit_amount,
    d.description
FROM public.con_partida_dtl d
JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
JOIN tmp_con_test_bancos_result t ON t.poliza_id = d.poliza_id
ORDER BY d.line_number;

SELECT
    h.poliza_id,
    h.total_debit AS hdr_debe,
    h.total_credit AS hdr_haber,
    x.debe_dtl,
    x.haber_dtl,
    CASE WHEN abs(x.debe_dtl - x.haber_dtl) <= 0.01 THEN 'OK' ELSE 'ERROR' END AS balance_dtl
FROM public.con_partida_hdr h
JOIN tmp_con_test_bancos_result t ON t.poliza_id = h.poliza_id
JOIN LATERAL (
    SELECT
        COALESCE(SUM(d.debit_amount), 0)::numeric(18,2) AS debe_dtl,
        COALESCE(SUM(d.credit_amount), 0)::numeric(18,2) AS haber_dtl
    FROM public.con_partida_dtl d
    WHERE d.poliza_id = h.poliza_id
) x ON true;

-- ------------------------------------------------------------
-- Impacto en saldos (delta esperado == movimientos de poliza)
-- ------------------------------------------------------------
WITH t AS (
    SELECT *
    FROM tmp_con_test_bancos_result
    LIMIT 1
),
mov AS (
    SELECT
        a.code AS codigo_cuenta,
        COALESCE(SUM(d.debit_amount), 0)::numeric(18,2) AS debito_poliza,
        COALESCE(SUM(d.credit_amount), 0)::numeric(18,2) AS credito_poliza
    FROM public.con_partida_dtl d
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN t ON t.poliza_id = d.poliza_id
    GROUP BY a.code
),
before_saldo AS (
    SELECT
        b.codigo_cuenta,
        b.debitos_before,
        b.creditos_before
    FROM tmp_con_test_bancos_saldo_before b
),
scope_accounts AS (
    SELECT codigo_cuenta FROM mov
    UNION
    SELECT codigo_cuenta FROM before_saldo
),
after_saldo AS (
    SELECT
        s.codigo_cuenta,
        COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_after,
        COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_after
    FROM public.con_saldo_cuenta s
    JOIN t ON t.company_id = s.company_id AND t.period_id = s.periodo_id
    JOIN scope_accounts sa ON sa.codigo_cuenta = s.codigo_cuenta
    WHERE s.mes = 13
      AND s.tipo_transaccion = 0
)
SELECT
    COALESCE(m.codigo_cuenta, a.codigo_cuenta) AS codigo_cuenta,
    COALESCE(m.debito_poliza, 0) AS debito_poliza,
    COALESCE(m.credito_poliza, 0) AS credito_poliza,
    COALESCE(b.debitos_before, 0) AS debitos_before,
    COALESCE(a.debitos_after, 0) AS debitos_after,
    (COALESCE(a.debitos_after, 0) - COALESCE(b.debitos_before, 0))::numeric(18,2) AS delta_debitos,
    COALESCE(b.creditos_before, 0) AS creditos_before,
    COALESCE(a.creditos_after, 0) AS creditos_after,
    (COALESCE(a.creditos_after, 0) - COALESCE(b.creditos_before, 0))::numeric(18,2) AS delta_creditos
FROM mov m
FULL OUTER JOIN after_saldo a ON a.codigo_cuenta = m.codigo_cuenta
FULL OUTER JOIN before_saldo b ON b.codigo_cuenta = COALESCE(m.codigo_cuenta, a.codigo_cuenta)
ORDER BY 1;

-- ------------------------------------------------------------
-- No doble posteo: reintento de posteo no debe mover saldos
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_bancos_saldo_before_repost;
CREATE TEMP TABLE tmp_con_test_bancos_saldo_before_repost AS
WITH t AS (
    SELECT *
    FROM tmp_con_test_bancos_result
    LIMIT 1
),
acc AS (
    SELECT DISTINCT a.code AS codigo_cuenta
    FROM public.con_partida_dtl d
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN t ON t.poliza_id = d.poliza_id
)
SELECT
    t.company_id,
    t.period_id,
    a.codigo_cuenta,
    COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_before_repost,
    COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_before_repost
FROM t
JOIN acc a ON true
LEFT JOIN public.con_saldo_cuenta s
  ON s.company_id = t.company_id
 AND s.periodo_id = t.period_id
 AND s.codigo_cuenta = a.codigo_cuenta
 AND s.mes = 13
 AND s.tipo_transaccion = 0;

SELECT public.sp_con_postear_poliza(t.company_id, t.poliza_id, t.user_name)
FROM tmp_con_test_bancos_result t;

WITH t AS (
    SELECT *
    FROM tmp_con_test_bancos_result
    LIMIT 1
),
after_repost AS (
    SELECT
        s.codigo_cuenta,
        COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_after_repost,
        COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_after_repost
    FROM public.con_saldo_cuenta s
    JOIN t ON t.company_id = s.company_id AND t.period_id = s.periodo_id
    WHERE s.mes = 13
      AND s.tipo_transaccion = 0
)
SELECT
    b.codigo_cuenta,
    b.debitos_before_repost,
    COALESCE(a.debitos_after_repost, 0) AS debitos_after_repost,
    (COALESCE(a.debitos_after_repost, 0) - b.debitos_before_repost)::numeric(18,2) AS delta_debitos_repost,
    b.creditos_before_repost,
    COALESCE(a.creditos_after_repost, 0) AS creditos_after_repost,
    (COALESCE(a.creditos_after_repost, 0) - b.creditos_before_repost)::numeric(18,2) AS delta_creditos_repost,
    CASE
        WHEN abs(COALESCE(a.debitos_after_repost, 0) - b.debitos_before_repost) <= 0.01
         AND abs(COALESCE(a.creditos_after_repost, 0) - b.creditos_before_repost) <= 0.01
        THEN 'OK'
        ELSE 'ERROR'
    END AS no_doble_posteo
FROM tmp_con_test_bancos_saldo_before_repost b
LEFT JOIN after_repost a ON a.codigo_cuenta = b.codigo_cuenta
ORDER BY b.codigo_cuenta;

-- ------------------------------------------------------------
-- Reversa exacta opcional
-- ------------------------------------------------------------
SELECT public.sp_con_revertir_poliza(t.company_id, t.poliza_id, t.user_name)
FROM tmp_con_test_bancos_result t
WHERE COALESCE(t.revert_after_test, false) = true;

SELECT
    h.poliza_id,
    h.status,
    h.posted_at,
    h.posted_by,
    h.total_debit,
    h.total_credit
FROM public.con_partida_hdr h
JOIN tmp_con_test_bancos_result t ON t.poliza_id = h.poliza_id;

WITH t AS (
    SELECT *
    FROM tmp_con_test_bancos_result
    LIMIT 1
),
after_reverse AS (
    SELECT
        s.codigo_cuenta,
        COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_after_reverse,
        COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_after_reverse
    FROM public.con_saldo_cuenta s
    JOIN t ON t.company_id = s.company_id AND t.period_id = s.periodo_id
    WHERE s.mes = 13
      AND s.tipo_transaccion = 0
)
SELECT
    b.rol,
    b.codigo_cuenta,
    b.debitos_before,
    COALESCE(a.debitos_after_reverse, 0) AS debitos_after_reverse,
    (COALESCE(a.debitos_after_reverse, 0) - b.debitos_before)::numeric(18,2) AS delta_debitos_reverse,
    b.creditos_before,
    COALESCE(a.creditos_after_reverse, 0) AS creditos_after_reverse,
    (COALESCE(a.creditos_after_reverse, 0) - b.creditos_before)::numeric(18,2) AS delta_creditos_reverse,
    CASE
        WHEN abs(COALESCE(a.debitos_after_reverse, 0) - b.debitos_before) <= 0.01
         AND abs(COALESCE(a.creditos_after_reverse, 0) - b.creditos_before) <= 0.01
        THEN 'OK'
        ELSE 'ERROR'
    END AS reversa_exacta
FROM tmp_con_test_bancos_saldo_before b
LEFT JOIN after_reverse a ON a.codigo_cuenta = b.codigo_cuenta
ORDER BY b.rol;
