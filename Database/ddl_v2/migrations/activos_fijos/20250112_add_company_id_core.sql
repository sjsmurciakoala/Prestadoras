-- Activos fijos: agrega company_id a depreciaciones y bajas
BEGIN;

ALTER TABLE public.af_depreciacion
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.af_depreciacion d
SET company_id = act.company_id
FROM public.af_activo act
WHERE d.activo_id = act.activo_id
  AND d.company_id IS NULL;

ALTER TABLE public.af_depreciacion
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_af_depreciacion_company'
          AND conrelid = 'public.af_depreciacion'::regclass
    ) THEN
        ALTER TABLE public.af_depreciacion
            ADD CONSTRAINT fk_af_depreciacion_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_af_depreciacion_company
    ON public.af_depreciacion(company_id);

ALTER TABLE public.af_baja
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.af_baja b
SET company_id = act.company_id
FROM public.af_activo act
WHERE b.activo_id = act.activo_id
  AND b.company_id IS NULL;

ALTER TABLE public.af_baja
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_af_baja_company'
          AND conrelid = 'public.af_baja'::regclass
    ) THEN
        ALTER TABLE public.af_baja
            ADD CONSTRAINT fk_af_baja_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_af_baja_company
    ON public.af_baja(company_id);

COMMIT;
