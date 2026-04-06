-- ============================================================
-- TEST E2E: Flujo por plantilla (sp_con_generar_comprobante)
-- Fecha: 2026-03-10
-- Objetivo:
--   Validar generacion, posteo, idempotencia, impacto en saldos
--   y reversa opcional del flujo de comprobantes por plantilla.
-- ============================================================
--
-- Uso:
-- 1) Ajustar parametros en tmp_con_test_plantilla_params.
-- 2) Ejecutar script completo en una misma sesion.
-- 3) Guardar evidencia de cada seccion.
--

-- ------------------------------------------------------------
-- Parametros de prueba
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_plantilla_params;
CREATE TEMP TABLE tmp_con_test_plantilla_params (
    company_id bigint NOT NULL,
    template_id bigint NULL,
    module text NULL,
    document_type text NULL,
    document_id bigint NULL,
    document_number text NULL,
    poliza_date date NOT NULL,
    description text NULL,
    user_name text NULL,
    type_id bigint NULL,
    journal_id bigint NULL,
    amount_total numeric(18,2) NOT NULL,
    amount_subtotal numeric(18,2) NOT NULL,
    amount_iva numeric(18,2) NOT NULL,
    amount_cobrado numeric(18,2) NOT NULL,
    force_generation boolean NOT NULL DEFAULT false,
    revert_after_test boolean NOT NULL DEFAULT true
);

INSERT INTO tmp_con_test_plantilla_params (
    company_id, template_id, module, document_type,
    document_id, document_number, poliza_date, description, user_name,
    type_id, journal_id,
    amount_total, amount_subtotal, amount_iva, amount_cobrado,
    force_generation, revert_after_test
) VALUES (
    1,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    CURRENT_DATE,
    'TEST E2E plantilla',
    'jmurcia',
    0,
    NULL,
    150.00,
    130.43,
    19.57,
    150.00,
    false,
    true
);

-- ------------------------------------------------------------
-- Resolver plantilla/documento de prueba
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_plantilla_resolved;
CREATE TEMP TABLE tmp_con_test_plantilla_resolved AS
WITH p AS (
    SELECT *
    FROM tmp_con_test_plantilla_params
    LIMIT 1
),
resolved_template AS (
    SELECT
        p.company_id,
        COALESCE(
            p.template_id,
            (
                SELECT t.template_id
                FROM public.con_plantilla_partida_hdr t
                WHERE t.company_id = p.company_id
                  AND t.is_active = true
                  AND (p.module IS NULL OR p.module = '' OR t.module = upper(p.module))
                  AND (p.document_type IS NULL OR p.document_type = '' OR t.document_type = upper(p.document_type))
                ORDER BY t.template_id DESC
                LIMIT 1
            )
        ) AS template_id,
        p.module,
        p.document_type,
        p.document_id,
        p.document_number,
        p.poliza_date,
        p.description,
        p.user_name,
        p.type_id,
        p.journal_id,
        p.amount_total,
        p.amount_subtotal,
        p.amount_iva,
        p.amount_cobrado,
        p.force_generation,
        p.revert_after_test
    FROM p
)
SELECT
    r.company_id,
    r.template_id,
    COALESCE(upper(NULLIF(r.module, '')), th.module) AS module,
    COALESCE(upper(NULLIF(r.document_type, '')), th.document_type) AS document_type,
    r.document_id,
    r.document_number,
    r.poliza_date,
    r.description,
    r.user_name,
    r.type_id,
    r.journal_id,
    r.amount_total,
    r.amount_subtotal,
    r.amount_iva,
    r.amount_cobrado,
    r.force_generation,
    r.revert_after_test
FROM resolved_template r
LEFT JOIN public.con_plantilla_partida_hdr th ON th.template_id = r.template_id;

-- ------------------------------------------------------------
-- A) Precheck: plantilla + lineas + periodo abierto + type_id
-- ------------------------------------------------------------
SELECT
    r.company_id,
    r.template_id,
    r.module,
    r.document_type,
    r.poliza_date,
    COALESCE(
        NULLIF(r.type_id, 0),
        (
            SELECT t.type_id
            FROM public.con_tipo_transaccion t
            WHERE t.company_id = r.company_id
              AND COALESCE(
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
        )
    ) AS type_id_effective,
    (
        SELECT COUNT(*)
        FROM public.con_plantilla_partida_dtl d
        WHERE d.template_id = r.template_id
    ) AS lineas_plantilla,
    (
        SELECT p.period_id
        FROM public.con_periodo_contable p
        WHERE p.company_id = r.company_id
          AND r.poliza_date BETWEEN p.start_date::date AND p.end_date::date
          AND COALESCE(p.status_id, 2) = 0
        ORDER BY p.start_date DESC
        LIMIT 1
    ) AS period_open_id
FROM tmp_con_test_plantilla_resolved r;

DO $$
DECLARE
    v_template_id bigint;
    v_lineas int;
    v_period_id bigint;
    v_type_id bigint;
BEGIN
    SELECT template_id
      INTO v_template_id
      FROM tmp_con_test_plantilla_resolved
     LIMIT 1;

    IF v_template_id IS NULL THEN
        RAISE EXCEPTION 'Precheck: no se encontro plantilla activa para los parametros dados.';
    END IF;

    SELECT COUNT(*)
      INTO v_lineas
      FROM public.con_plantilla_partida_dtl
     WHERE template_id = v_template_id;

    IF v_lineas = 0 THEN
        RAISE EXCEPTION 'Precheck: la plantilla % no tiene lineas.', v_template_id;
    END IF;

    SELECT COALESCE(
               NULLIF(r.type_id, 0),
               (
                   SELECT t.type_id
                   FROM public.con_tipo_transaccion t
                   WHERE t.company_id = r.company_id
                     AND COALESCE(
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
               )
           )
      INTO v_type_id
      FROM tmp_con_test_plantilla_resolved r
     LIMIT 1;

    IF v_type_id IS NULL THEN
        RAISE EXCEPTION 'Precheck: no se encontro type_id activo para company %.', (SELECT company_id FROM tmp_con_test_plantilla_resolved LIMIT 1);
    END IF;

    SELECT p.period_id
      INTO v_period_id
      FROM public.con_periodo_contable p
      JOIN tmp_con_test_plantilla_resolved r ON r.company_id = p.company_id
     WHERE r.poliza_date BETWEEN p.start_date::date AND p.end_date::date
       AND COALESCE(p.status_id, 2) = 0
     LIMIT 1;

    IF v_period_id IS NULL THEN
        RAISE EXCEPTION 'Precheck: no hay periodo abierto (estado 0) que cubra la fecha de prueba.';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- B) Baseline de saldos para cuentas de la plantilla
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_plantilla_saldo_before;
CREATE TEMP TABLE tmp_con_test_plantilla_saldo_before AS
WITH r AS (
    SELECT *
    FROM tmp_con_test_plantilla_resolved
    LIMIT 1
),
period_open AS (
    SELECT p.period_id
    FROM public.con_periodo_contable p
    JOIN r ON r.company_id = p.company_id
    WHERE r.poliza_date BETWEEN p.start_date::date AND p.end_date::date
      AND COALESCE(p.status_id, 2) = 0
    ORDER BY p.start_date DESC
    LIMIT 1
),
acc AS (
    SELECT DISTINCT a.code AS codigo_cuenta
    FROM public.con_plantilla_partida_dtl d
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN r ON r.template_id = d.template_id
)
SELECT
    r.company_id,
    po.period_id,
    a.codigo_cuenta,
    COALESCE(s.debitos, 0)::numeric(18,2) AS debitos_before,
    COALESCE(s.creditos, 0)::numeric(18,2) AS creditos_before
FROM r
JOIN period_open po ON true
JOIN acc a ON true
LEFT JOIN public.con_saldo_cuenta s
  ON s.company_id = r.company_id
 AND s.periodo_id = po.period_id
 AND s.codigo_cuenta = a.codigo_cuenta
 AND s.mes = 13
 AND s.tipo_transaccion = 0;

SELECT * FROM tmp_con_test_plantilla_saldo_before ORDER BY codigo_cuenta;

-- ------------------------------------------------------------
-- C) Generar comprobante (posteo incluido)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_plantilla_result;
CREATE TEMP TABLE tmp_con_test_plantilla_result AS
WITH r AS (
    SELECT *
    FROM tmp_con_test_plantilla_resolved
    LIMIT 1
),
doc AS (
    SELECT
        COALESCE(r.document_id, EXTRACT(EPOCH FROM clock_timestamp())::bigint) AS document_id_used,
        COALESCE(NULLIF(r.document_number, ''), 'TPL-E2E-' || to_char(clock_timestamp(), 'YYYYMMDDHH24MISS')) AS document_number_used
    FROM r
),
type_resolved AS (
    SELECT
        COALESCE(
            NULLIF(r.type_id, 0),
            (
                SELECT t.type_id
                FROM public.con_tipo_transaccion t
                WHERE t.company_id = r.company_id
                  AND COALESCE(
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
            )
        ) AS type_id_used
    FROM r
)
SELECT
    public.sp_con_generar_comprobante(
        r.company_id,
        r.module,
        r.document_type,
        doc.document_id_used,
        doc.document_number_used,
        r.poliza_date,
        COALESCE(r.description, 'TEST E2E plantilla'),
        COALESCE(r.user_name, 'system'),
        r.template_id,
        tr.type_id_used,
        r.journal_id,
        jsonb_build_object(
            'total', r.amount_total,
            'subtotal', r.amount_subtotal,
            'iva', r.amount_iva,
            'cobrado', r.amount_cobrado,
            'monto', r.amount_total,
            'valor', r.amount_total
        ),
        COALESCE(r.force_generation, false)
    ) AS poliza_id,
    r.company_id,
    r.template_id,
    r.module,
    r.document_type,
    doc.document_id_used,
    doc.document_number_used,
    r.poliza_date,
    COALESCE(r.user_name, 'system') AS user_name,
    COALESCE(r.revert_after_test, true) AS revert_after_test
FROM r
JOIN doc ON true
JOIN type_resolved tr ON true;

SELECT * FROM tmp_con_test_plantilla_result;

-- ------------------------------------------------------------
-- D) Validacion de cabecera, detalle y cuadre
-- ------------------------------------------------------------
SELECT
    h.poliza_id,
    h.company_id,
    h.period_id,
    h.template_id,
    h.module,
    h.document_type,
    h.document_id,
    h.document_number,
    h.status,
    h.total_debit,
    h.total_credit,
    h.posted_by,
    h.posted_at
FROM public.con_partida_hdr h
JOIN tmp_con_test_plantilla_result t ON t.poliza_id = h.poliza_id;

SELECT
    d.line_number,
    a.code AS cuenta_codigo,
    a.name AS cuenta_nombre,
    d.debit_amount,
    d.credit_amount,
    d.description
FROM public.con_partida_dtl d
JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
JOIN tmp_con_test_plantilla_result t ON t.poliza_id = d.poliza_id
ORDER BY d.line_number;

SELECT
    h.poliza_id,
    h.total_debit AS hdr_debe,
    h.total_credit AS hdr_haber,
    x.debe_dtl,
    x.haber_dtl,
    CASE WHEN abs(x.debe_dtl - x.haber_dtl) <= 0.01 THEN 'OK' ELSE 'ERROR' END AS balance_dtl
FROM public.con_partida_hdr h
JOIN tmp_con_test_plantilla_result t ON t.poliza_id = h.poliza_id
JOIN LATERAL (
    SELECT
        COALESCE(SUM(d.debit_amount), 0)::numeric(18,2) AS debe_dtl,
        COALESCE(SUM(d.credit_amount), 0)::numeric(18,2) AS haber_dtl
    FROM public.con_partida_dtl d
    WHERE d.poliza_id = h.poliza_id
) x ON true;

-- ------------------------------------------------------------
-- E) Impacto en saldos (delta debe coincidir con movimientos de poliza)
-- ------------------------------------------------------------
WITH t AS (
    SELECT *
    FROM tmp_con_test_plantilla_result
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
    FROM tmp_con_test_plantilla_saldo_before b
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
    JOIN t ON t.company_id = s.company_id
    JOIN public.con_partida_hdr h ON h.poliza_id = t.poliza_id AND h.period_id = s.periodo_id
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
-- F) Idempotencia (mismo documento, force=false => misma poliza)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_test_plantilla_idempotencia;
CREATE TEMP TABLE tmp_con_test_plantilla_idempotencia AS
WITH t AS (
    SELECT *
    FROM tmp_con_test_plantilla_result
    LIMIT 1
)
SELECT
    t.poliza_id AS poliza_original,
    public.sp_con_generar_comprobante(
        t.company_id,
        t.module,
        t.document_type,
        t.document_id_used,
        t.document_number_used,
        t.poliza_date,
        'TEST E2E plantilla - idempotencia',
        t.user_name,
        t.template_id,
        0,
        NULL,
        '{"total":150.00,"subtotal":130.43,"iva":19.57,"cobrado":150.00,"monto":150.00,"valor":150.00}'::jsonb,
        false
    ) AS poliza_reintento
FROM t;

SELECT
    poliza_original,
    poliza_reintento,
    CASE WHEN poliza_original = poliza_reintento THEN 'OK' ELSE 'ERROR' END AS idempotencia
FROM tmp_con_test_plantilla_idempotencia;

-- ------------------------------------------------------------
-- G) Reversa opcional del comprobante de prueba
-- ------------------------------------------------------------
DO $$
DECLARE
    v_revert boolean;
    v_poliza_id bigint;
    v_user text;
BEGIN
    SELECT t.revert_after_test, t.poliza_id, t.user_name
      INTO v_revert, v_poliza_id, v_user
      FROM tmp_con_test_plantilla_result t
     LIMIT 1;

    IF COALESCE(v_revert, true) THEN
        PERFORM public.sp_con_revertir_comprobante(v_poliza_id, COALESCE(v_user, 'system'));
    END IF;
END
$$;

SELECT
    h.poliza_id,
    h.status,
    h.posted_at,
    h.posted_by,
    h.total_debit,
    h.total_credit
FROM public.con_partida_hdr h
JOIN tmp_con_test_plantilla_result t ON t.poliza_id = h.poliza_id;
