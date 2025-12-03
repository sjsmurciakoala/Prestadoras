-- Contabilidad: agrega company_id a tablas de líneas
BEGIN;

ALTER TABLE public.con_plantilla_poliza_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.con_plantilla_poliza_linea l
SET company_id = p.company_id
FROM public.con_plantilla_poliza p
WHERE l.template_id = p.template_id
  AND l.company_id IS NULL;

ALTER TABLE public.con_plantilla_poliza_linea
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_con_plantilla_poliza_linea_company'
          AND conrelid = 'public.con_plantilla_poliza_linea'::regclass
    ) THEN
        ALTER TABLE public.con_plantilla_poliza_linea
            ADD CONSTRAINT fk_con_plantilla_poliza_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_con_plantilla_poliza_linea_company
    ON public.con_plantilla_poliza_linea(company_id);

ALTER TABLE public.con_poliza_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.con_poliza_linea l
SET company_id = p.company_id
FROM public.con_poliza p
WHERE l.poliza_id = p.poliza_id
  AND l.company_id IS NULL;

ALTER TABLE public.con_poliza_linea
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_con_poliza_linea_company'
          AND conrelid = 'public.con_poliza_linea'::regclass
    ) THEN
        ALTER TABLE public.con_poliza_linea
            ADD CONSTRAINT fk_con_poliza_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_con_poliza_linea_company
    ON public.con_poliza_linea(company_id);

COMMIT;
