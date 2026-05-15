-- =============================================================================
-- DROP completo de adm_establecimiento (proyecto multi-empresa pero MONO-SUCURSAL)
-- Fecha: 2026-05-08
-- Decision: el "establecimiento" en este sistema es SOLO el codigo SAR (EEE,
-- texto libre 3 digitos en adm_cai_facturacion.establecimiento_codigo). No hay
-- catalogo de sucursales por empresa porque cada empresa tiene una sola sucursal.
-- Por eso retiramos:
--   - tabla adm_establecimiento
--   - columna adm_cai_facturacion.establecimiento_id (FK a esa tabla)
--   - indice y FK relacionados
-- adm_cai_facturacion.establecimiento_codigo (texto EEE) se conserva.
-- Idempotente.
-- =============================================================================

BEGIN;

-- 1. Drop FK adm_cai_facturacion -> adm_establecimiento si existe
DO $$ BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'fk_adm_cai_facturacion_establecimiento'
           AND conrelid = 'public.adm_cai_facturacion'::regclass
    ) THEN
        ALTER TABLE public.adm_cai_facturacion
            DROP CONSTRAINT fk_adm_cai_facturacion_establecimiento;
    END IF;
END $$;

-- 2. Drop indice de establecimiento_id en adm_cai_facturacion
DROP INDEX IF EXISTS public.ix_adm_cai_facturacion_establecimiento;

-- 3. Drop columna establecimiento_id de adm_cai_facturacion
ALTER TABLE public.adm_cai_facturacion
    DROP COLUMN IF EXISTS establecimiento_id;

-- 4. Drop tabla adm_establecimiento (con CASCADE para las FK que apunten a ella)
DROP TABLE IF EXISTS public.adm_establecimiento CASCADE;

COMMIT;

-- Verificacion: la tabla ya no existe y la columna tampoco
SELECT 'adm_establecimiento exists?' AS check, EXISTS (
    SELECT 1 FROM pg_tables WHERE tablename = 'adm_establecimiento' AND schemaname = 'public'
) AS result
UNION ALL
SELECT 'adm_cai_facturacion.establecimiento_id exists?', EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_schema = 'public' AND table_name = 'adm_cai_facturacion' AND column_name = 'establecimiento_id'
);
