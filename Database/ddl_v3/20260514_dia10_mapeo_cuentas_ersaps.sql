-- =============================================================================
-- Sprint 3 Día 10 — Mapeo adm_servicio → cuentas regulatorias ERSAPS (clase 5.1.x)
-- Fecha: 2026-05-14
-- Gap SAR/ERSAPS #8: cada servicio facturable debe tener cuenta contable del
-- catálogo regulatorio (Manual de Contabilidad ERSAPS, ingresos por venta de
-- servicios 5.1.1 / 5.1.2 / 5.1.3).
--
-- Contenido:
--   1. Backfill: mapea los servicios de adm_servicio a su cuenta de ingreso.
--      Se resuelve por `con_plan_cuentas.code` (robusto entre ambientes).
--   2. fn_adm_servicios_sin_cuenta_contable: función de diagnóstico — lista los
--      servicios activos sin cuenta. La usa el portal para alertar y sirve de
--      base a cualquier validación de "enforcement antes de emitir".
--
-- Nota de alcance: el mapeo es a la cuenta REPRESENTATIVA del servicio (ej.
-- AGUA_POTABLE → "Con Medición - Doméstico"). El mapeo fino por categoría×
-- condición (8 cuentas distintas para agua) queda como refinamiento post-25;
-- hoy adm_servicio es un único registro por servicio, no por categoría.
--
-- Idempotente: el UPDATE solo toca servicios con cont_account_id IS NULL.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Backfill del mapeo servicio → cuenta de ingreso 5.1.x
-- -----------------------------------------------------------------------------
UPDATE public.adm_servicio s
SET cont_account_id = cpc.account_id,
    updated_at = now(),
    updated_by = 'dia10-mapeo-ersaps'
FROM public.con_plan_cuentas cpc
WHERE cpc.company_id = s.company_id
  AND s.cont_account_id IS NULL
  AND s.status_id = 1
  AND cpc.code = CASE UPPER(BTRIM(s.codigo))
        -- Servicio de Agua Potable (5.1.1) — cuenta representativa
        WHEN 'AGUA_POTABLE'      THEN '51101000000'  -- Con Medición - Doméstico
        -- Servicio de Alcantarillado (5.1.2)
        WHEN 'ALCANTARILLADO'    THEN '51201000000'  -- Doméstico
        -- Servicios Colaterales Regulados (5.1.3)
        WHEN 'CONEXION'          THEN '51301000000'  -- Cargo por Conexión
        WHEN 'INSTALACION'       THEN '51301000000'  -- Cargo por Conexión
        WHEN 'CORTE_RECONEXION'  THEN '51302000000'  -- Cargo por Corte y Reconexión
        WHEN 'AGUA_CISTERNA'     THEN '51303000000'  -- Cargo por agua potable mediante cisterna
        WHEN 'TASA_AMBIENTAL'    THEN '51304000000'  -- Otros Cargos
        WHEN 'TASA_SVA_ERSAPS'   THEN '51304000000'  -- Otros Cargos
        WHEN 'TRASLADO'          THEN '51304000000'  -- Otros Cargos
        WHEN 'MODIFICACION'      THEN '51304000000'  -- Otros Cargos
        WHEN 'OTROS_COLATERALES' THEN '51304000000'  -- Otros Cargos
        ELSE NULL
      END;

-- -----------------------------------------------------------------------------
-- 2. Función de diagnóstico: servicios activos sin cuenta contable
-- -----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.fn_adm_servicios_sin_cuenta_contable(bigint);

CREATE OR REPLACE FUNCTION public.fn_adm_servicios_sin_cuenta_contable(p_company_id bigint)
RETURNS TABLE (
    servicio_id bigint,
    codigo varchar,
    nombre varchar
)
LANGUAGE sql
STABLE
AS $function$
    SELECT s.servicio_id, s.codigo, s.nombre
    FROM public.adm_servicio s
    WHERE s.company_id = p_company_id
      AND s.status_id = 1
      AND s.cont_account_id IS NULL
    ORDER BY s.codigo;
$function$;

COMMENT ON FUNCTION public.fn_adm_servicios_sin_cuenta_contable(bigint) IS
'Lista los servicios activos sin cuenta contable regulatoria asignada (gap ERSAPS #8). El portal la usa para alertar; un resultado vacío significa que todos los servicios están mapeados.';

-- -----------------------------------------------------------------------------
-- 3. Verificación
-- -----------------------------------------------------------------------------
DO $$
DECLARE
    v_sin_cuenta integer;
BEGIN
    SELECT COUNT(*) INTO v_sin_cuenta
    FROM public.adm_servicio
    WHERE status_id = 1 AND cont_account_id IS NULL;

    RAISE NOTICE 'Servicios activos sin cuenta contable tras el backfill: %', v_sin_cuenta;
END $$;
