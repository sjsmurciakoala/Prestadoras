-- =============================================================================
-- SAR-Compliance fase 2 — ALTER adm_cai_facturacion + factura
-- Fecha: 2026-05-07
-- Plan: Prestadoras/docs/PLAN_SAR_COMPLIANCE_2026-05-06.md
-- Requisito previo: 20260507_sar_compliance_01_catalogos.sql aplicado.
-- Idempotente.
-- =============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- 1) ALTER adm_cai_facturacion — establecimiento + tipo de documento + leyendas
-- ----------------------------------------------------------------------------
ALTER TABLE public.adm_cai_facturacion
    ADD COLUMN IF NOT EXISTS establecimiento_id bigint,
    ADD COLUMN IF NOT EXISTS tipo_documento_fiscal_id smallint,
    ADD COLUMN IF NOT EXISTS fecha_limite_emision date,
    ADD COLUMN IF NOT EXISTS leyenda_rango varchar(200);

-- Backfill establecimiento_id = principal de la company del CAI
UPDATE public.adm_cai_facturacion cai
SET establecimiento_id = e.establecimiento_id
FROM public.adm_establecimiento e
WHERE e.company_id = cai.company_id
  AND e.es_principal = true
  AND e.status_id = 1
  AND cai.establecimiento_id IS NULL;

-- Backfill tipo_documento_fiscal_id por defecto = 1 (Factura). El operador puede
-- reclasificar manualmente después por la UI de configuración de CAIs.
UPDATE public.adm_cai_facturacion
SET tipo_documento_fiscal_id = 1
WHERE tipo_documento_fiscal_id IS NULL;

-- Backfill fecha_limite_emision: vigencia_hasta si existe, sino vigencia_desde + 1 año.
UPDATE public.adm_cai_facturacion
SET fecha_limite_emision = COALESCE(vigencia_hasta, vigencia_desde + INTERVAL '1 year')::date
WHERE fecha_limite_emision IS NULL;

-- Backfill leyenda_rango (legible humano para imprimir)
UPDATE public.adm_cai_facturacion
SET leyenda_rango = 'CAI ' || codigo_cai
                  || ' | Rango: ' || rango_desde::text || ' al ' || rango_hasta::text
                  || ' | Vigencia: ' || vigencia_desde::text
                  || COALESCE(' al ' || vigencia_hasta::text, '')
                  || ' | Fecha límite emisión: ' || COALESCE(fecha_limite_emision::text, '')
WHERE leyenda_rango IS NULL;

-- Ahora forzamos NOT NULL en las columnas críticas
ALTER TABLE public.adm_cai_facturacion
    ALTER COLUMN establecimiento_id SET NOT NULL,
    ALTER COLUMN tipo_documento_fiscal_id SET NOT NULL,
    ALTER COLUMN fecha_limite_emision SET NOT NULL;

-- FKs y constraints
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_adm_cai_facturacion_establecimiento') THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT fk_adm_cai_facturacion_establecimiento
            FOREIGN KEY (company_id, establecimiento_id)
            REFERENCES public.adm_establecimiento (company_id, establecimiento_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_adm_cai_facturacion_tipo_doc') THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT fk_adm_cai_facturacion_tipo_doc
            FOREIGN KEY (tipo_documento_fiscal_id)
            REFERENCES public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_adm_cai_facturacion_fecha_limite') THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT ck_adm_cai_facturacion_fecha_limite
            CHECK (fecha_limite_emision >= vigencia_desde);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_adm_cai_facturacion_establecimiento
    ON public.adm_cai_facturacion (company_id, establecimiento_id, status_id);

CREATE INDEX IF NOT EXISTS ix_adm_cai_facturacion_tipo_doc
    ON public.adm_cai_facturacion (company_id, tipo_documento_fiscal_id, status_id);

-- ----------------------------------------------------------------------------
-- 2) ALTER factura — datos fiscales + tipo de documento + snapshot CAI
-- ----------------------------------------------------------------------------
ALTER TABLE public.factura
    ADD COLUMN IF NOT EXISTS establecimiento_id bigint,
    ADD COLUMN IF NOT EXISTS rtn_emisor varchar(20),
    ADD COLUMN IF NOT EXISTS razon_social_emisor varchar(200),
    ADD COLUMN IF NOT EXISTS direccion_emisor varchar(300),
    ADD COLUMN IF NOT EXISTS tipo_documento_fiscal_id smallint,
    ADD COLUMN IF NOT EXISTS factura_origen_id int,
    ADD COLUMN IF NOT EXISTS motivo_anulacion_id smallint,
    ADD COLUMN IF NOT EXISTS leyenda_cai_rango varchar(200),
    ADD COLUMN IF NOT EXISTS fecha_limite_cai date;

-- Backfill desde el CAI vinculado a la factura (via adm_cai_correlativo_emitido)
UPDATE public.factura f
SET establecimiento_id    = cai.establecimiento_id,
    rtn_emisor            = e.rtn_emisor,
    razon_social_emisor   = e.razon_social_emisor,
    direccion_emisor      = e.direccion_emisor,
    tipo_documento_fiscal_id = cai.tipo_documento_fiscal_id,
    leyenda_cai_rango     = cai.leyenda_rango,
    fecha_limite_cai      = cai.fecha_limite_emision
FROM public.adm_cai_correlativo_emitido ce
JOIN public.adm_cai_facturacion cai
       ON cai.company_id = ce.company_id AND cai.cai_id = ce.cai_id
JOIN public.adm_establecimiento e
       ON e.company_id = cai.company_id AND e.establecimiento_id = cai.establecimiento_id
WHERE ce.factura_id = f.id
  AND f.tipo_documento_fiscal_id IS NULL;

-- Para facturas sin CAI vinculado (legacy), usar el establecimiento principal de
-- la primera empresa registrada y tipo_documento_fiscal_id = 1 (Factura)
UPDATE public.factura f
SET establecimiento_id      = e.establecimiento_id,
    rtn_emisor              = e.rtn_emisor,
    razon_social_emisor     = e.razon_social_emisor,
    direccion_emisor        = e.direccion_emisor,
    tipo_documento_fiscal_id = 1
FROM public.adm_establecimiento e
WHERE e.es_principal = true
  AND e.status_id = 1
  AND f.tipo_documento_fiscal_id IS NULL
  AND e.company_id = (SELECT MIN(company_id) FROM public.cfg_company);

-- tipo_documento_fiscal_id NOT NULL después del backfill
ALTER TABLE public.factura
    ALTER COLUMN tipo_documento_fiscal_id SET NOT NULL;

-- FKs y constraints
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_factura_tipo_doc') THEN
        ALTER TABLE public.factura
            ADD CONSTRAINT fk_factura_tipo_doc
            FOREIGN KEY (tipo_documento_fiscal_id)
            REFERENCES public.cfg_tipo_documento_fiscal (tipo_documento_fiscal_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_factura_motivo_anulacion') THEN
        ALTER TABLE public.factura
            ADD CONSTRAINT fk_factura_motivo_anulacion
            FOREIGN KEY (motivo_anulacion_id)
            REFERENCES public.cfg_motivo_anulacion (motivo_anulacion_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_factura_origen') THEN
        ALTER TABLE public.factura
            ADD CONSTRAINT fk_factura_origen
            FOREIGN KEY (factura_origen_id)
            REFERENCES public.factura (id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_factura_tipo_doc
    ON public.factura (tipo_documento_fiscal_id);

CREATE INDEX IF NOT EXISTS ix_factura_factura_origen
    ON public.factura (factura_origen_id) WHERE factura_origen_id IS NOT NULL;

COMMIT;

-- ----------------------------------------------------------------------------
-- Verificación post-migración
-- ----------------------------------------------------------------------------
-- CAIs sin establecimiento o tipo de documento (deberían ser 0)
SELECT 'cai_sin_establecimiento' AS check, count(*) AS valor
FROM public.adm_cai_facturacion WHERE establecimiento_id IS NULL
UNION ALL
SELECT 'cai_sin_tipo_doc', count(*)
FROM public.adm_cai_facturacion WHERE tipo_documento_fiscal_id IS NULL
UNION ALL
SELECT 'cai_sin_fecha_limite', count(*)
FROM public.adm_cai_facturacion WHERE fecha_limite_emision IS NULL
UNION ALL
SELECT 'factura_sin_tipo_doc', count(*)
FROM public.factura WHERE tipo_documento_fiscal_id IS NULL
UNION ALL
SELECT 'factura_sin_rtn_emisor', count(*)
FROM public.factura WHERE rtn_emisor IS NULL;
