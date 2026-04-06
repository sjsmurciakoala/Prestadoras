-- 20260309_contabilidad_status_id_migracion.sql
-- Normaliza estados a numeric status_id con compatibilidad temporal de status texto.

BEGIN;

-- =========================================================
-- con_periodo_contable: status_id (0 ABIERTO, 1 PRECIERRE, 2 CERRADO)
-- =========================================================
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

-- =========================================================
-- con_tipo_transaccion: status_id (1 ACTIVE, 0 INACTIVE)
-- =========================================================
ALTER TABLE public.con_tipo_transaccion
    ADD COLUMN IF NOT EXISTS status_id smallint;

UPDATE public.con_tipo_transaccion
   SET status_id = CASE
       WHEN upper(COALESCE(status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
       WHEN upper(COALESCE(status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
       ELSE 1
   END
 WHERE status_id IS NULL;

ALTER TABLE public.con_tipo_transaccion
    ALTER COLUMN status_id SET DEFAULT 1;

ALTER TABLE public.con_tipo_transaccion
    ALTER COLUMN status_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conname = 'ck_con_tipo_transaccion_status_id'
           AND conrelid = 'public.con_tipo_transaccion'::regclass
    ) THEN
        ALTER TABLE public.con_tipo_transaccion
            ADD CONSTRAINT ck_con_tipo_transaccion_status_id
            CHECK (status_id IN (0, 1));
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_con_tipo_transaccion_status_id
    ON public.con_tipo_transaccion (company_id, status_id);

UPDATE public.con_tipo_transaccion
   SET status = CASE status_id
       WHEN 1 THEN 'ACTIVE'
       ELSE 'INACTIVE'
   END;

COMMIT;
