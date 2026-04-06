-- ============================================================
-- 2026-03-31_normalize_con_periodo_contable_code.sql
-- Ajuste puntual DEV
-- Corrige el periodo abierto actual de la empresa 2.
-- ============================================================

BEGIN;

UPDATE public.con_periodo_contable
   SET code = '202601',
       name = '202601',
       start_date = TIMESTAMPTZ '2026-01-01 00:00:00-06',
       end_date = TIMESTAMPTZ '2026-01-31 23:59:59-06',
       updated_at = NOW(),
       updated_by = 'fix-period-20260331'
 WHERE company_id = 2
   AND period_id = 2;

COMMIT;

SELECT
    period_id,
    company_id,
    code,
    name,
    start_date,
    end_date,
    status,
    status_id
FROM public.con_periodo_contable
WHERE company_id = 2
ORDER BY period_id;

UPDATE public.con_periodo_contable
   SET code = '202601',
       name = '202601',
       start_date = TIMESTAMPTZ '2026-01-01 00:00:00-06',
       end_date = TIMESTAMPTZ '2026-01-31 23:59:59-06',
       updated_at = NOW(),
       updated_by = 'fix-period-20260331'
 WHERE company_id = 2
   AND period_id = 2;


select * from public.con_configuracion_balance

select * from public.con_configuracion_sistema cfc
join public.cfg_company ON cfg_company.company_id = cfc.company_id
where cfg_company.company_id = 2;

