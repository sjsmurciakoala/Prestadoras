-- Ventas: agrega company_id a líneas de facturas, notas y cobros
BEGIN;

ALTER TABLE public.ven_factura_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.ven_factura_linea l
SET company_id = f.company_id
FROM public.ven_factura f
WHERE l.factura_id = f.factura_id
  AND l.company_id IS NULL;

ALTER TABLE public.ven_factura_linea
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ven_factura_linea_company'
          AND conrelid = 'public.ven_factura_linea'::regclass
    ) THEN
        ALTER TABLE public.ven_factura_linea
            ADD CONSTRAINT fk_ven_factura_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_ven_factura_linea_company
    ON public.ven_factura_linea(company_id);

ALTER TABLE public.ven_nota
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.ven_nota n
SET company_id = f.company_id
FROM public.ven_factura f
WHERE n.factura_id = f.factura_id
  AND n.company_id IS NULL;

ALTER TABLE public.ven_nota
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ven_nota_company'
          AND conrelid = 'public.ven_nota'::regclass
    ) THEN
        ALTER TABLE public.ven_nota
            ADD CONSTRAINT fk_ven_nota_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_ven_nota_company ON public.ven_nota(company_id);

ALTER TABLE public.ven_cobro_detalle
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.ven_cobro_detalle d
SET company_id = c.company_id
FROM public.ven_cobro c
WHERE d.cobro_id = c.cobro_id
  AND d.company_id IS NULL;

ALTER TABLE public.ven_cobro_detalle
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ven_cobro_detalle_company'
          AND conrelid = 'public.ven_cobro_detalle'::regclass
    ) THEN
        ALTER TABLE public.ven_cobro_detalle
            ADD CONSTRAINT fk_ven_cobro_detalle_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_ven_cobro_detalle_company
    ON public.ven_cobro_detalle(company_id);

COMMIT;
