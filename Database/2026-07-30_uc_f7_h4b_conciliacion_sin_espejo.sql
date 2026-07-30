-- =============================================================================
-- F7 H4 (parte 2) — la conciliación deja el espejo + reposición de triggers
-- =============================================================================
-- Dos cosas que destapó la prueba del congelamiento:
--
-- 1. `fn_factura_concilia_recibos_pendientes` era un ESCRITOR del espejo que el
--    censo de H2 no vio (es un trigger, no un SP ni un servicio): al saldarse
--    una factura seguía marcando 'CUBIERTO' sobre los 202/'P' legacy. Con el
--    candado de H4 eso reventaría el cobro de cualquier factura con papeles
--    viejos. Se recorta: la conciliación vive SOLO en adm_recibo_banco_pendiente
--    (H1); los 'P' del histórico congelado quedan tal cual (la vista de vigencia
--    ya los excluye por letra).
--
-- 2. La reconstrucción de `factura` en la migración (M4/M3d: DROP + RENAME de
--    la tabla) se llevó sus TRES triggers sin que nada avisara. Se reponen aquí
--    de forma idempotente. Lección para futuras reconstrucciones: los triggers
--    NO viajan con el RENAME — inventariarlos antes del DROP.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ----------------------------------------------------------------------------
-- 1) Conciliación sin espejo
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_factura_concilia_recibos_pendientes()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    UPDATE public.adm_recibo_banco_pendiente r
    SET estado_id = 3,
        anulado_en = now(),
        anulado_por = 'sistema',
        motivo_anulacion = 'CUBIERTO: factura saldada'
    WHERE r.company_id = NEW.company_id
      AND r.factura_id = NEW.id
      AND r.estado_id = 2;

    -- F7 H4: el espejo legacy ya NO se toca. transaccion_abonado está
    -- congelada; sus 202/'P' históricos quedan como estaban (la vista de
    -- vigencia los excluye por la letra 'P').
    RETURN NEW;
END;
$function$;

-- ----------------------------------------------------------------------------
-- 2) Reposición de los triggers de `factura` (perdidos en la reconstrucción)
-- ----------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_factura_sync_estado_id ON public.factura;
CREATE TRIGGER trg_factura_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.factura
    FOR EACH ROW EXECUTE FUNCTION public.fn_factura_sync_estado_id();

DROP TRIGGER IF EXISTS trg_factura_snapshot_dimensional ON public.factura;
CREATE TRIGGER trg_factura_snapshot_dimensional
    BEFORE INSERT ON public.factura
    FOR EACH ROW EXECUTE FUNCTION public.fn_factura_snapshot_dimensional();

DROP TRIGGER IF EXISTS trg_factura_concilia_pendientes ON public.factura;
CREATE TRIGGER trg_factura_concilia_pendientes
    AFTER UPDATE OF estado ON public.factura
    FOR EACH ROW
    WHEN (NEW.estado::text = 'C' AND OLD.estado::text IS DISTINCT FROM 'C')
    EXECUTE FUNCTION public.fn_factura_concilia_recibos_pendientes();

COMMIT;

-- ----------------------------------------------------------------------------
-- Humo: los 3 triggers presentes
-- ----------------------------------------------------------------------------
DO $$
DECLARE v_n int;
BEGIN
    SELECT count(*) INTO v_n
      FROM pg_trigger
     WHERE tgrelid = 'public.factura'::regclass AND NOT tgisinternal;
    IF v_n <> 3 THEN
        RAISE EXCEPTION 'factura debería tener 3 triggers y tiene %', v_n;
    END IF;
    RAISE NOTICE 'H4b OK: los 3 triggers de factura presentes.';
END $$;
