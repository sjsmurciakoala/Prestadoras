-- ============================================================
-- Reconciliacion y rebuild de saldos contables acumulados
-- Tabla objetivo: public.con_saldo_cuenta (mes=13, tipo_transaccion=0)
-- Fecha: 2026-03-10
-- ============================================================
--
-- Uso recomendado:
-- 1) Ajustar parametros en tmp_con_recon_params (NULL = todos).
-- 2) Ejecutar seccion A (diagnostico previo).
-- 3) Ejecutar seccion B (backup + rebuild) en ventana de mantenimiento.
-- 4) Ejecutar seccion C (validacion final).
--
-- Notas:
-- - Este script no usa hardcodes de company/period.
-- - Se reconstruye desde polizas POSTED (status=1).
--

-- ------------------------------------------------------------
-- Parametros de alcance (NULL = todos)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_recon_params;
CREATE TEMP TABLE tmp_con_recon_params (
    company_id bigint NULL,
    period_id bigint NULL
);

INSERT INTO tmp_con_recon_params (company_id, period_id)
VALUES (NULL, NULL);


-- ------------------------------------------------------------
-- A) Diagnostico previo: posted vs con_saldo_cuenta
-- ------------------------------------------------------------
WITH params AS (
    SELECT company_id, period_id
    FROM tmp_con_recon_params
    LIMIT 1
),
posted AS (
    SELECT
        h.company_id,
        h.period_id,
        a.code AS codigo_cuenta,
        ROUND(SUM(COALESCE(d.debit_amount, 0)), 2) AS debitos_calc,
        ROUND(SUM(COALESCE(d.credit_amount, 0)), 2) AS creditos_calc,
        SUM(CASE WHEN COALESCE(d.debit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_debitos_calc,
        SUM(CASE WHEN COALESCE(d.credit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_creditos_calc
    FROM public.con_partida_hdr h
    JOIN public.con_partida_dtl d ON d.poliza_id = h.poliza_id
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN params p ON true
    WHERE h.status = 1
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.period_id IS NULL OR h.period_id = p.period_id)
    GROUP BY h.company_id, h.period_id, a.code
),
saldo AS (
    SELECT
        s.company_id,
        s.periodo_id AS period_id,
        s.codigo_cuenta,
        ROUND(COALESCE(s.debitos, 0), 2) AS debitos,
        ROUND(COALESCE(s.creditos, 0), 2) AS creditos,
        COALESCE(s.cantidad_debitos, 0) AS cant_debitos,
        COALESCE(s.cantidad_creditos, 0) AS cant_creditos
    FROM public.con_saldo_cuenta s
    JOIN params p ON true
    WHERE s.mes = 13
      AND s.tipo_transaccion = 0
      AND (p.company_id IS NULL OR s.company_id = p.company_id)
      AND (p.period_id IS NULL OR s.periodo_id = p.period_id)
)
SELECT
    COALESCE(p.company_id, s.company_id) AS company_id,
    COALESCE(p.period_id, s.period_id) AS period_id,
    COALESCE(p.codigo_cuenta, s.codigo_cuenta) AS codigo_cuenta,
    COALESCE(p.debitos_calc, 0) AS debitos_calc,
    COALESCE(s.debitos, 0) AS debitos_saldo,
    COALESCE(p.creditos_calc, 0) AS creditos_calc,
    COALESCE(s.creditos, 0) AS creditos_saldo,
    COALESCE(p.cant_debitos_calc, 0) AS cant_debitos_calc,
    COALESCE(s.cant_debitos, 0) AS cant_debitos_saldo,
    COALESCE(p.cant_creditos_calc, 0) AS cant_creditos_calc,
    COALESCE(s.cant_creditos, 0) AS cant_creditos_saldo
FROM posted p
FULL OUTER JOIN saldo s
  ON s.company_id = p.company_id
 AND s.period_id = p.period_id
 AND s.codigo_cuenta = p.codigo_cuenta
WHERE ABS(COALESCE(p.debitos_calc, 0) - COALESCE(s.debitos, 0)) > 0.01
   OR ABS(COALESCE(p.creditos_calc, 0) - COALESCE(s.creditos, 0)) > 0.01
   OR COALESCE(p.cant_debitos_calc, 0) <> COALESCE(s.cant_debitos, 0)
   OR COALESCE(p.cant_creditos_calc, 0) <> COALESCE(s.cant_creditos, 0)
ORDER BY 1, 2, 3;
/*resultado:*
company_id	period_id	codigo_cuenta	debitos_calc	debitos_saldo	creditos_calc	creditos_saldo	cant_debitos_calc	cant_debitos_saldo	cant_creditos_calc	cant_creditos_saldo
1	1	110302	29100.00	14400.00	0.00	0.00	4	1	0	0
1	1	510102	0.00	0.00	29100.00	14400.00	0	0	8	2*/


-- ------------------------------------------------------------
-- B) Backup + rebuild de saldos acumulados
-- ------------------------------------------------------------
BEGIN;

LOCK TABLE public.con_saldo_cuenta IN SHARE ROW EXCLUSIVE MODE;

CREATE TABLE IF NOT EXISTS public.con_saldo_cuenta_backup_hist AS
SELECT
    now()::timestamptz AS backup_at,
    ''::text AS backup_tag,
    s.*
FROM public.con_saldo_cuenta s
WHERE false;

WITH params AS (
    SELECT company_id, period_id
    FROM tmp_con_recon_params
    LIMIT 1
)
INSERT INTO public.con_saldo_cuenta_backup_hist
SELECT
    now()::timestamptz AS backup_at,
    'rebuild_20260310_mes13_tipo0'::text AS backup_tag,
    s.*
FROM public.con_saldo_cuenta s
JOIN params p ON true
WHERE s.mes = 13
  AND s.tipo_transaccion = 0
  AND (p.company_id IS NULL OR s.company_id = p.company_id)
  AND (p.period_id IS NULL OR s.periodo_id = p.period_id);

WITH params AS (
    SELECT company_id, period_id
    FROM tmp_con_recon_params
    LIMIT 1
)
DELETE FROM public.con_saldo_cuenta s
USING params p
WHERE s.mes = 13
  AND s.tipo_transaccion = 0
  AND (p.company_id IS NULL OR s.company_id = p.company_id)
  AND (p.period_id IS NULL OR s.periodo_id = p.period_id);

WITH params AS (
    SELECT company_id, period_id
    FROM tmp_con_recon_params
    LIMIT 1
),
posted AS (
    SELECT
        h.company_id,
        h.period_id,
        a.code AS codigo_cuenta,
        ROUND(SUM(COALESCE(d.debit_amount, 0)), 2) AS debitos_calc,
        ROUND(SUM(COALESCE(d.credit_amount, 0)), 2) AS creditos_calc,
        SUM(CASE WHEN COALESCE(d.debit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_debitos_calc,
        SUM(CASE WHEN COALESCE(d.credit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_creditos_calc
    FROM public.con_partida_hdr h
    JOIN public.con_partida_dtl d ON d.poliza_id = h.poliza_id
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN params p ON true
    WHERE h.status = 1
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.period_id IS NULL OR h.period_id = p.period_id)
    GROUP BY h.company_id, h.period_id, a.code
)
INSERT INTO public.con_saldo_cuenta (
    company_id,
    periodo_id,
    codigo_cuenta,
    mes,
    tipo_transaccion,
    debitos,
    creditos,
    cantidad_debitos,
    cantidad_creditos,
    presupuesto,
    created_at,
    updated_at
)
SELECT
    p.company_id,
    p.period_id,
    p.codigo_cuenta,
    13,
    0,
    p.debitos_calc,
    p.creditos_calc,
    p.cant_debitos_calc,
    p.cant_creditos_calc,
    0,
    now(),
    now()
FROM posted p;

COMMIT;


-- ------------------------------------------------------------
-- C) Validacion final: debe retornar 0 filas
-- ------------------------------------------------------------
WITH params AS (
    SELECT company_id, period_id
    FROM tmp_con_recon_params
    LIMIT 1
),
posted AS (
    SELECT
        h.company_id,
        h.period_id,
        a.code AS codigo_cuenta,
        ROUND(SUM(COALESCE(d.debit_amount, 0)), 2) AS debitos_calc,
        ROUND(SUM(COALESCE(d.credit_amount, 0)), 2) AS creditos_calc,
        SUM(CASE WHEN COALESCE(d.debit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_debitos_calc,
        SUM(CASE WHEN COALESCE(d.credit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cant_creditos_calc
    FROM public.con_partida_hdr h
    JOIN public.con_partida_dtl d ON d.poliza_id = h.poliza_id
    JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
    JOIN params p ON true
    WHERE h.status = 1
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.period_id IS NULL OR h.period_id = p.period_id)
    GROUP BY h.company_id, h.period_id, a.code
),
saldo AS (
    SELECT
        s.company_id,
        s.periodo_id AS period_id,
        s.codigo_cuenta,
        ROUND(COALESCE(s.debitos, 0), 2) AS debitos,
        ROUND(COALESCE(s.creditos, 0), 2) AS creditos,
        COALESCE(s.cantidad_debitos, 0) AS cant_debitos,
        COALESCE(s.cantidad_creditos, 0) AS cant_creditos
    FROM public.con_saldo_cuenta s
    JOIN params p ON true
    WHERE s.mes = 13
      AND s.tipo_transaccion = 0
      AND (p.company_id IS NULL OR s.company_id = p.company_id)
      AND (p.period_id IS NULL OR s.periodo_id = p.period_id)
)
SELECT
    COALESCE(p.company_id, s.company_id) AS company_id,
    COALESCE(p.period_id, s.period_id) AS period_id,
    COALESCE(p.codigo_cuenta, s.codigo_cuenta) AS codigo_cuenta,
    COALESCE(p.debitos_calc, 0) AS debitos_calc,
    COALESCE(s.debitos, 0) AS debitos_saldo,
    COALESCE(p.creditos_calc, 0) AS creditos_calc,
    COALESCE(s.creditos, 0) AS creditos_saldo,
    COALESCE(p.cant_debitos_calc, 0) AS cant_debitos_calc,
    COALESCE(s.cant_debitos, 0) AS cant_debitos_saldo,
    COALESCE(p.cant_creditos_calc, 0) AS cant_creditos_calc,
    COALESCE(s.cant_creditos, 0) AS cant_creditos_saldo
FROM posted p
FULL OUTER JOIN saldo s
  ON s.company_id = p.company_id
 AND s.period_id = p.period_id
 AND s.codigo_cuenta = p.codigo_cuenta
WHERE ABS(COALESCE(p.debitos_calc, 0) - COALESCE(s.debitos, 0)) > 0.01
   OR ABS(COALESCE(p.creditos_calc, 0) - COALESCE(s.creditos, 0)) > 0.01
   OR COALESCE(p.cant_debitos_calc, 0) <> COALESCE(s.cant_debitos, 0)
   OR COALESCE(p.cant_creditos_calc, 0) <> COALESCE(s.cant_creditos, 0)
ORDER BY 1, 2, 3;
