-- =============================================================================
-- BUGFIX #3 — corregir backfill anterior
-- Fecha: 2026-05-14
--
-- Problema con backfill del script anterior:
--   adm_cai_facturacion.correlativo_actual quedo "inflado" porque tomo
--   MAX(b.correlativo_actual) de bloques, pero los bloques no usados tienen
--   correlativo_actual = correlativo_desde (ej: bloque ruta 01005 con
--   correlativo_actual=500 porque su rango_desde=500 y nadie emitio).
--
-- Fix:
--   Ambos contadores deben backfillearse desde adm_cai_correlativo_emitido,
--   la unica fuente de verdad de "correlativo realmente emitido como factura".
--   Considerar CONFIRMADO + ANULADO (la anulada consume correlativo legal).
--   NO considerar SYNC_CONFLICT (no es factura emitida valida).
--
-- Primero RESETEAMOS los contadores a (correlativo_desde - 1) para que el
-- nuevo backfill suba desde abajo sin arrastrar los valores inflados.
-- =============================================================================

-- Paso 1: reset de bloque y CAI a estado "sin emisiones" (correlativo_desde - 1)
UPDATE public.adm_cai_bloque_reservado
SET correlativo_actual = correlativo_desde - 1,
    updated_at = now(),
    updated_by = current_user
WHERE company_id = 2;

UPDATE public.adm_cai_facturacion
SET correlativo_actual = 0,
    updated_at = now(),
    updated_by = current_user
WHERE company_id = 2;

-- Paso 2: backfill bloque desde correlativos emitidos (CONFIRMADO o ANULADO).
UPDATE public.adm_cai_bloque_reservado b
SET correlativo_actual = sub.max_correlativo,
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        e.company_id,
        e.cai_bloque_id,
        MAX(e.correlativo) AS max_correlativo
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.estado_codigo IN ('CONFIRMADO', 'ANULADO')
      AND e.company_id = 2
    GROUP BY e.company_id, e.cai_bloque_id
) sub
WHERE b.company_id = sub.company_id
  AND b.cai_bloque_id = sub.cai_bloque_id
  AND b.correlativo_actual < sub.max_correlativo;

-- Paso 3: backfill CAI desde correlativos emitidos (no desde bloques).
UPDATE public.adm_cai_facturacion c
SET correlativo_actual = sub.max_correlativo,
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        e.company_id,
        e.cai_id,
        MAX(e.correlativo) AS max_correlativo
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.estado_codigo IN ('CONFIRMADO', 'ANULADO')
      AND e.company_id = 2
    GROUP BY e.company_id, e.cai_id
) sub
WHERE c.company_id = sub.company_id
  AND c.cai_id = sub.cai_id
  AND c.correlativo_actual < sub.max_correlativo;

-- =============================================================================
-- Validacion automatica
-- =============================================================================

DO $$
DECLARE
    v_msg text;
BEGIN
    SELECT string_agg(
        format('CAI %s (%s): correlativo_actual=%s, max_emitido=%s',
            c.cai_id, c.codigo_cai, c.correlativo_actual,
            COALESCE((SELECT MAX(e.correlativo)
                      FROM public.adm_cai_correlativo_emitido e
                      WHERE e.cai_id = c.cai_id
                        AND e.estado_codigo IN ('CONFIRMADO', 'ANULADO')), 0)
        ),
        E'\n'
    )
    INTO v_msg
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = 2
    ORDER BY c.cai_id;

    RAISE NOTICE E'Estado post-backfill:\n%', v_msg;
END $$;
