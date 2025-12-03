-- Bancos: agrega company_id a conciliaciones
BEGIN;

ALTER TABLE public.ban_conciliacion
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.ban_conciliacion c
SET company_id = cuenta.company_id
FROM public.ban_cuenta cuenta
WHERE c.banco_cuenta_id = cuenta.banco_cuenta_id
  AND c.company_id IS NULL;

ALTER TABLE public.ban_conciliacion
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_conciliacion_company'
          AND conrelid = 'public.ban_conciliacion'::regclass
    ) THEN
        ALTER TABLE public.ban_conciliacion
            ADD CONSTRAINT fk_ban_conciliacion_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_company
    ON public.ban_conciliacion(company_id);

ALTER TABLE public.ban_conciliacion_detalle
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.ban_conciliacion_detalle d
SET company_id = conc.company_id
FROM public.ban_conciliacion conc
WHERE d.conciliacion_id = conc.conciliacion_id
  AND d.company_id IS NULL;

ALTER TABLE public.ban_conciliacion_detalle
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_conciliacion_detalle_company'
          AND conrelid = 'public.ban_conciliacion_detalle'::regclass
    ) THEN
        ALTER TABLE public.ban_conciliacion_detalle
            ADD CONSTRAINT fk_ban_conciliacion_detalle_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_detalle_company
    ON public.ban_conciliacion_detalle(company_id);

COMMIT;
