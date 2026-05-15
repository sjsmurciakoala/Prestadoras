-- =============================================================================
-- adm_cai_facturacion: agregar punto_emision (PPP del prefijo SAR EEE-PPP-TD-NNN)
-- Fecha: 2026-05-08
-- Idempotente.
-- =============================================================================

BEGIN;

ALTER TABLE public.adm_cai_facturacion
    ADD COLUMN IF NOT EXISTS punto_emision varchar(3);

-- Backfill: para CAIs existentes, intentar extraer PPP del prefijo_documento
-- formato actual: "EEE-PPP" o "EEE-PPP-TD" o "EEE-PPP-TD-NNNNNNNN"
UPDATE public.adm_cai_facturacion
SET punto_emision = COALESCE(
    NULLIF(split_part(prefijo_documento, '-', 2), ''),
    '001'
)
WHERE punto_emision IS NULL;

ALTER TABLE public.adm_cai_facturacion
    ALTER COLUMN punto_emision SET NOT NULL,
    ALTER COLUMN punto_emision SET DEFAULT '001';

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_adm_cai_facturacion_punto_emision_format') THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT ck_adm_cai_facturacion_punto_emision_format
            CHECK (punto_emision ~ '^[0-9]{3}$');
    END IF;
END $$;

COMMIT;

-- Verificación
SELECT cai_id, prefijo_documento, punto_emision FROM public.adm_cai_facturacion;
