-- 2026-03-26_fix_con_periodo_status_mapping.sql
-- Corrige el mapeo canonico de estados de con_periodo_contable:
-- 0 = ABIERTO, 1 = PRECIERRE, 2 = CERRADO

BEGIN;

ALTER TABLE public.con_periodo_contable
    ADD COLUMN IF NOT EXISTS status_id smallint;

UPDATE public.con_periodo_contable
   SET status_id = CASE
       WHEN upper(COALESCE(status, 'ABIERTO')) IN ('OPEN', 'ABIERTO') THEN 0
       WHEN upper(COALESCE(status, 'ABIERTO')) IN ('LOCKED', 'BLOQUEADO', 'PRECIERRE') THEN 1
       WHEN upper(COALESCE(status, 'ABIERTO')) IN ('CLOSED', 'CERRADO') THEN 2
       ELSE COALESCE(status_id, 0)
   END;

ALTER TABLE public.con_periodo_contable
    ALTER COLUMN status_id SET DEFAULT 0;

ALTER TABLE public.con_periodo_contable
    ALTER COLUMN status_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_con_periodo_contable_status_id'
           AND conrelid = 'public.con_periodo_contable'::regclass
    ) THEN
        ALTER TABLE public.con_periodo_contable
            ADD CONSTRAINT ck_con_periodo_contable_status_id
            CHECK (status_id IN (0, 1, 2));
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_con_periodo_contable_status_id
    ON public.con_periodo_contable (company_id, status_id);

UPDATE public.con_periodo_contable
   SET status = CASE status_id
       WHEN 0 THEN 'ABIERTO'
       WHEN 1 THEN 'PRECIERRE'
       WHEN 2 THEN 'CERRADO'
       ELSE 'ABIERTO'
   END;

COMMIT;
