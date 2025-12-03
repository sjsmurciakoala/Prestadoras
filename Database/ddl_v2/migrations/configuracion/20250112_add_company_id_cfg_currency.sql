-- Configuración: agrega company_id a cfg_currency
BEGIN;

ALTER TABLE public.cfg_currency
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.cfg_currency cur
SET company_id = cmp.company_id
FROM public.cfg_company cmp
WHERE cmp.currency_code = cur.currency_code
  AND cur.company_id IS NULL;

UPDATE public.cfg_currency cur
SET company_id = (
        SELECT company_id
        FROM public.cfg_company
        ORDER BY company_id
        LIMIT 1
    )
WHERE cur.company_id IS NULL;

ALTER TABLE public.cfg_currency
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_cfg_currency_company'
          AND conrelid = 'public.cfg_currency'::regclass
    ) THEN
        ALTER TABLE public.cfg_currency
            ADD CONSTRAINT fk_cfg_currency_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id) ON DELETE CASCADE;
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_cfg_currency_company ON public.cfg_currency(company_id);

COMMIT;
