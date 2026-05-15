-- =============================================================================
-- SAR-Compliance fase 4 — Multi-tenancy de tablas legacy de facturación
--                       + simplificar adm_establecimiento (drop duplicados)
--                       + FKs compuestas tenant-aware
-- Fecha: 2026-05-07
-- Plan: PLAN_ENTREGA_2026-05-25.md (decisión "A" 2026-05-07)
-- Idempotente. Atómico (BEGIN/COMMIT).
--
-- Tablas que reciben company_id: factura, factura_detalle, transaccion_abonado,
--                                historicomedicion, maestro_medidor.
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) factura — company_id desde cliente_maestro via clientecodigo
-- ============================================================================
ALTER TABLE public.factura ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.factura f
SET company_id = cm.company_id
FROM public.cliente_maestro cm
WHERE TRIM(cm.maestro_cliente_clave) = TRIM(f.clientecodigo)
  AND f.company_id IS NULL;

-- Fallback para huérfanos (cliente eliminado): primer company
UPDATE public.factura
SET company_id = (SELECT MIN(company_id) FROM public.cfg_company)
WHERE company_id IS NULL;

ALTER TABLE public.factura ALTER COLUMN company_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_factura_company') THEN
        ALTER TABLE public.factura ADD CONSTRAINT fk_factura_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_factura_company ON public.factura(company_id);

-- ============================================================================
-- 2) factura_detalle — company_id heredado de factura (ya con company_id)
-- ============================================================================
ALTER TABLE public.factura_detalle ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.factura_detalle fd
SET company_id = f.company_id
FROM public.factura f
WHERE f.id = fd.factura_id AND fd.company_id IS NULL;

UPDATE public.factura_detalle
SET company_id = (SELECT MIN(company_id) FROM public.cfg_company)
WHERE company_id IS NULL;

ALTER TABLE public.factura_detalle ALTER COLUMN company_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_factura_detalle_company') THEN
        ALTER TABLE public.factura_detalle ADD CONSTRAINT fk_factura_detalle_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_factura_detalle_company ON public.factura_detalle(company_id);

-- ============================================================================
-- 3) transaccion_abonado — company_id desde cliente_maestro via cliente_clave
-- ============================================================================
ALTER TABLE public.transaccion_abonado ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.transaccion_abonado ta
SET company_id = cm.company_id
FROM public.cliente_maestro cm
WHERE TRIM(cm.maestro_cliente_clave) = TRIM(ta.cliente_clave)
  AND ta.company_id IS NULL;

UPDATE public.transaccion_abonado
SET company_id = (SELECT MIN(company_id) FROM public.cfg_company)
WHERE company_id IS NULL;

ALTER TABLE public.transaccion_abonado ALTER COLUMN company_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_transaccion_abonado_company') THEN
        ALTER TABLE public.transaccion_abonado ADD CONSTRAINT fk_transaccion_abonado_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_transaccion_abonado_company ON public.transaccion_abonado(company_id);

-- ============================================================================
-- 4) historicomedicion — company_id desde cliente_maestro via columna "clave"
-- ============================================================================
ALTER TABLE public.historicomedicion ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.historicomedicion hm
SET company_id = cm.company_id
FROM public.cliente_maestro cm
WHERE TRIM(cm.maestro_cliente_clave) = TRIM(hm.clave)
  AND hm.company_id IS NULL;

UPDATE public.historicomedicion
SET company_id = (SELECT MIN(company_id) FROM public.cfg_company)
WHERE company_id IS NULL;

ALTER TABLE public.historicomedicion ALTER COLUMN company_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_historicomedicion_company') THEN
        ALTER TABLE public.historicomedicion ADD CONSTRAINT fk_historicomedicion_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_historicomedicion_company ON public.historicomedicion(company_id);

-- ============================================================================
-- 5) maestro_medidor — company_id derivado de cliente_detalle.maestro_medidor_id
--    (un medidor puede estar asignado a un cliente; tomamos el primero)
-- ============================================================================
ALTER TABLE public.maestro_medidor ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.maestro_medidor mm
SET company_id = sub.company_id
FROM (
    SELECT DISTINCT ON (cd.maestro_medidor_id)
           cd.maestro_medidor_id,
           cd.company_id
    FROM public.cliente_detalle cd
    WHERE cd.maestro_medidor_id IS NOT NULL
    ORDER BY cd.maestro_medidor_id, cd.detalle_cliente_id
) sub
WHERE sub.maestro_medidor_id = mm.maestro_medidor_id
  AND mm.company_id IS NULL;

-- Fallback para medidores sin asignación
UPDATE public.maestro_medidor
SET company_id = (SELECT MIN(company_id) FROM public.cfg_company)
WHERE company_id IS NULL;

ALTER TABLE public.maestro_medidor ALTER COLUMN company_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_maestro_medidor_company') THEN
        ALTER TABLE public.maestro_medidor ADD CONSTRAINT fk_maestro_medidor_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_maestro_medidor_company ON public.maestro_medidor(company_id);

-- ============================================================================
-- 6) Simplificar adm_establecimiento
--    RTN y razón social viven en cfg_company. El establecimiento solo
--    guarda lo que varía por sucursal (dirección, código, contacto).
-- ============================================================================
ALTER TABLE public.adm_establecimiento DROP CONSTRAINT IF EXISTS ck_adm_establecimiento_rtn_format;
ALTER TABLE public.adm_establecimiento DROP COLUMN IF EXISTS rtn_emisor;
ALTER TABLE public.adm_establecimiento DROP COLUMN IF EXISTS razon_social_emisor;

-- ============================================================================
-- 7) FK compuesta factura -> adm_establecimiento (tenant-aware)
-- ============================================================================
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_factura_establecimiento') THEN
        ALTER TABLE public.factura ADD CONSTRAINT fk_factura_establecimiento
            FOREIGN KEY (company_id, establecimiento_id)
            REFERENCES public.adm_establecimiento(company_id, establecimiento_id);
    END IF;
END $$;

COMMIT;

-- ============================================================================
-- Verificación post-migración (todos los counts deben ser 0)
-- ============================================================================
SELECT 'factura sin company_id'             AS check, count(*) AS valor FROM public.factura             WHERE company_id IS NULL
UNION ALL
SELECT 'factura_detalle sin company_id',    count(*) FROM public.factura_detalle    WHERE company_id IS NULL
UNION ALL
SELECT 'transaccion_abonado sin company_id', count(*) FROM public.transaccion_abonado WHERE company_id IS NULL
UNION ALL
SELECT 'historicomedicion sin company_id',  count(*) FROM public.historicomedicion  WHERE company_id IS NULL
UNION ALL
SELECT 'maestro_medidor sin company_id',    count(*) FROM public.maestro_medidor    WHERE company_id IS NULL
UNION ALL
SELECT 'adm_establecimiento con rtn_emisor (no debe existir col)',
    (SELECT count(*) FROM information_schema.columns
     WHERE table_schema='public' AND table_name='adm_establecimiento' AND column_name IN ('rtn_emisor','razon_social_emisor'));

-- Distribución por company
SELECT 'factura por company',             company_id::text, count(*)::text FROM public.factura GROUP BY company_id
UNION ALL
SELECT 'transaccion_abonado por company', company_id::text, count(*)::text FROM public.transaccion_abonado GROUP BY company_id
UNION ALL
SELECT 'historicomedicion por company',   company_id::text, count(*)::text FROM public.historicomedicion   GROUP BY company_id
UNION ALL
SELECT 'maestro_medidor por company',     company_id::text, count(*)::text FROM public.maestro_medidor     GROUP BY company_id;
