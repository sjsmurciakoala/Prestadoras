-- Inventarios: agrega company_id a existencias y movimientos
BEGIN;

ALTER TABLE public.inv_existencia
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.inv_existencia e
SET company_id = alm.company_id
FROM public.inv_almacen alm
WHERE e.almacen_id = alm.almacen_id
  AND e.company_id IS NULL;

ALTER TABLE public.inv_existencia
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_inv_existencia_company'
          AND conrelid = 'public.inv_existencia'::regclass
    ) THEN
        ALTER TABLE public.inv_existencia
            ADD CONSTRAINT fk_inv_existencia_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_inv_existencia_company
    ON public.inv_existencia(company_id);

ALTER TABLE public.inv_movimiento_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.inv_movimiento_linea l
SET company_id = mov.company_id
FROM public.inv_movimiento mov
WHERE l.movimiento_id = mov.movimiento_id
  AND l.company_id IS NULL;

ALTER TABLE public.inv_movimiento_linea
    ALTER COLUMN company_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_inv_movimiento_linea_company'
          AND conrelid = 'public.inv_movimiento_linea'::regclass
    ) THEN
        ALTER TABLE public.inv_movimiento_linea
            ADD CONSTRAINT fk_inv_movimiento_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_inv_movimiento_linea_company
    ON public.inv_movimiento_linea(company_id);

COMMIT;
