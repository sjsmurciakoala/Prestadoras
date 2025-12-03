-- Compras: agrega company_id a órdenes, facturas y pagos
BEGIN;

ALTER TABLE public.com_orden_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.com_orden_linea l
SET company_id = o.company_id
FROM public.com_orden o
WHERE l.orden_id = o.orden_id
  AND l.company_id IS NULL;

ALTER TABLE public.com_orden_linea
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_com_orden_linea_company'
          AND conrelid = 'public.com_orden_linea'::regclass
    ) THEN
        ALTER TABLE public.com_orden_linea
            ADD CONSTRAINT fk_com_orden_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_com_orden_linea_company
    ON public.com_orden_linea(company_id);

ALTER TABLE public.com_factura_linea
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.com_factura_linea l
SET company_id = f.company_id
FROM public.com_factura f
WHERE l.factura_id = f.factura_id
  AND l.company_id IS NULL;

ALTER TABLE public.com_factura_linea
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_com_factura_linea_company'
          AND conrelid = 'public.com_factura_linea'::regclass
    ) THEN
        ALTER TABLE public.com_factura_linea
            ADD CONSTRAINT fk_com_factura_linea_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_com_factura_linea_company
    ON public.com_factura_linea(company_id);

ALTER TABLE public.com_pago_detalle
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.com_pago_detalle d
SET company_id = p.company_id
FROM public.com_pago p
WHERE d.pago_id = p.pago_id
  AND d.company_id IS NULL;

ALTER TABLE public.com_pago_detalle
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_com_pago_detalle_company'
          AND conrelid = 'public.com_pago_detalle'::regclass
    ) THEN
        ALTER TABLE public.com_pago_detalle
            ADD CONSTRAINT fk_com_pago_detalle_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_com_pago_detalle_company
    ON public.com_pago_detalle(company_id);

COMMIT;
