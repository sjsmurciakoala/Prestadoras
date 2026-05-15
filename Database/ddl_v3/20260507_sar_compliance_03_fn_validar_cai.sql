-- =============================================================================
-- SAR-Compliance fase 3 — Función validadora de CAI emitible
-- Fecha: 2026-05-07
-- Plan: Prestadoras/docs/PLAN_SAR_COMPLIANCE_2026-05-06.md §4.3
-- Idempotente.
-- =============================================================================

-- Devuelve true si el CAI está activo, dentro de vigencia, y no superó la fecha
-- límite de emisión exigida por SAR. Llamado desde sp_lectura_v3 y SPs
-- emisores de NC/ND antes de tomar correlativo.

CREATE OR REPLACE FUNCTION public.fn_adm_validar_cai_emitible(
    p_company_id bigint,
    p_cai_id bigint
) RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1
        FROM public.adm_cai_facturacion
        WHERE company_id = p_company_id
          AND cai_id = p_cai_id
          AND status_id = 1
          AND vigencia_desde <= current_date
          AND (vigencia_hasta IS NULL OR vigencia_hasta >= current_date)
          AND fecha_limite_emision >= current_date
    );
$$;

COMMENT ON FUNCTION public.fn_adm_validar_cai_emitible(bigint, bigint) IS
'SAR-compliance: valida que el CAI esté activo, vigente y dentro de fecha límite
de emisión. Si devuelve false, NO se debe emitir documento con ese CAI
(SAR Acuerdo 481-2017). Llamada antes de reservar correlativo.';

-- Smoke test: validar con un CAI conocido
-- SELECT public.fn_adm_validar_cai_emitible(1, 1);
