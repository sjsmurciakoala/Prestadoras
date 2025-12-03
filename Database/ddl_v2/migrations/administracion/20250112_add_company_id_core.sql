-- Administración: agrega company_id a catálogos, reportes y movimientos
BEGIN;

ALTER TABLE public.adm_lista_precio_detalle
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_lista_precio_detalle d
SET company_id = lp.company_id
FROM public.adm_lista_precio lp
WHERE d.lista_precio_id = lp.lista_precio_id
  AND d.company_id IS NULL;

ALTER TABLE public.adm_lista_precio_detalle
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_lista_precio_detalle_company'
          AND conrelid = 'public.adm_lista_precio_detalle'::regclass
    ) THEN
        ALTER TABLE public.adm_lista_precio_detalle
            ADD CONSTRAINT fk_adm_lista_precio_detalle_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_detalle_company
    ON public.adm_lista_precio_detalle(company_id);

ALTER TABLE public.adm_cliente_contacto
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_cliente_contacto c
SET company_id = cli.company_id
FROM public.adm_cliente cli
WHERE c.cliente_id = cli.cliente_id
  AND c.company_id IS NULL;

ALTER TABLE public.adm_cliente_contacto
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_cliente_contacto_company'
          AND conrelid = 'public.adm_cliente_contacto'::regclass
    ) THEN
        ALTER TABLE public.adm_cliente_contacto
            ADD CONSTRAINT fk_adm_cliente_contacto_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_cliente_contacto_company
    ON public.adm_cliente_contacto(company_id);

ALTER TABLE public.adm_proveedor_contacto
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_proveedor_contacto c
SET company_id = prov.company_id
FROM public.adm_proveedor prov
WHERE c.proveedor_id = prov.proveedor_id
  AND c.company_id IS NULL;

ALTER TABLE public.adm_proveedor_contacto
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_proveedor_contacto_company'
          AND conrelid = 'public.adm_proveedor_contacto'::regclass
    ) THEN
        ALTER TABLE public.adm_proveedor_contacto
            ADD CONSTRAINT fk_adm_proveedor_contacto_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_proveedor_contacto_company
    ON public.adm_proveedor_contacto(company_id);

ALTER TABLE public.adm_reporte_parametro
    ADD COLUMN IF NOT EXISTS company_id bigint;

ALTER TABLE public.adm_reporte_programacion
    ADD COLUMN IF NOT EXISTS company_id bigint;

ALTER TABLE public.adm_reporte_ejecucion
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_reporte_parametro p
SET company_id = r.company_id
FROM public.adm_reporte_definicion r
WHERE p.reporte_id = r.reporte_id
  AND p.company_id IS NULL;

UPDATE public.adm_reporte_programacion p
SET company_id = r.company_id
FROM public.adm_reporte_definicion r
WHERE p.reporte_id = r.reporte_id
  AND p.company_id IS NULL;

UPDATE public.adm_reporte_ejecucion e
SET company_id = r.company_id
FROM public.adm_reporte_definicion r
WHERE e.reporte_id = r.reporte_id
  AND e.company_id IS NULL;

ALTER TABLE public.adm_reporte_parametro
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_reporte_parametro_company'
          AND conrelid = 'public.adm_reporte_parametro'::regclass
    ) THEN
        ALTER TABLE public.adm_reporte_parametro
            ADD CONSTRAINT fk_adm_reporte_parametro_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

ALTER TABLE public.adm_reporte_programacion
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_reporte_programacion_company'
          AND conrelid = 'public.adm_reporte_programacion'::regclass
    ) THEN
        ALTER TABLE public.adm_reporte_programacion
            ADD CONSTRAINT fk_adm_reporte_programacion_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

ALTER TABLE public.adm_reporte_ejecucion
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_reporte_ejecucion_company'
          AND conrelid = 'public.adm_reporte_ejecucion'::regclass
    ) THEN
        ALTER TABLE public.adm_reporte_ejecucion
            ADD CONSTRAINT fk_adm_reporte_ejecucion_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_reporte_parametro_company
    ON public.adm_reporte_parametro(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_programacion_company
    ON public.adm_reporte_programacion(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_ejecucion_company
    ON public.adm_reporte_ejecucion(company_id);

ALTER TABLE public.adm_cxc_movimiento
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_cxc_movimiento m
SET company_id = cxc.company_id
FROM public.adm_cxc_resumen cxc
WHERE m.cxc_id = cxc.cxc_id
  AND m.company_id IS NULL;

ALTER TABLE public.adm_cxc_movimiento
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_cxc_mov_company'
          AND conrelid = 'public.adm_cxc_movimiento'::regclass
    ) THEN
        ALTER TABLE public.adm_cxc_movimiento
            ADD CONSTRAINT fk_adm_cxc_mov_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_cxc_mov_company
    ON public.adm_cxc_movimiento(company_id);

ALTER TABLE public.adm_cxp_movimiento
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_cxp_movimiento m
SET company_id = cxp.company_id
FROM public.adm_cxp_resumen cxp
WHERE m.cxp_id = cxp.cxp_id
  AND m.company_id IS NULL;

ALTER TABLE public.adm_cxp_movimiento
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_cxp_mov_company'
          AND conrelid = 'public.adm_cxp_movimiento'::regclass
    ) THEN
        ALTER TABLE public.adm_cxp_movimiento
            ADD CONSTRAINT fk_adm_cxp_mov_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_cxp_mov_company
    ON public.adm_cxp_movimiento(company_id);

ALTER TABLE public.adm_interes_mora
    ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.adm_interes_mora m
SET company_id = cxc.company_id
FROM public.adm_cxc_resumen cxc
WHERE m.cxc_id = cxc.cxc_id
  AND m.company_id IS NULL;

ALTER TABLE public.adm_interes_mora
    ALTER COLUMN company_id SET NOT NULL;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_adm_interes_mora_company'
          AND conrelid = 'public.adm_interes_mora'::regclass
    ) THEN
        ALTER TABLE public.adm_interes_mora
            ADD CONSTRAINT fk_adm_interes_mora_company FOREIGN KEY (company_id)
                REFERENCES public.cfg_company(company_id);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_adm_interes_mora_company
    ON public.adm_interes_mora(company_id);

COMMIT;
