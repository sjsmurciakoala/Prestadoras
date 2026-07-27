-- =============================================================================
-- Unificación de cobranza — Ajustes de caja (feedback revisión 2026-07-27)
-- Fecha: 2026-07-27
--
-- 1) Una caja pertenece a UN solo cajero (además de: un cajero en una sola
--    caja). Reasignar = quitar la asignación actual y asignar el nuevo.
-- 2) sesion_caja.monto_apertura: fondo inicial de la caja al abrir el turno
--    (opcional), visible en el cierre.
--
-- Idempotente. Aplicar en local (siad_v3_copia09) y test (siad_v3_test).
-- NO aplicar en 0.9.
-- =============================================================================

BEGIN;

-- Si hubiera datos con varios cajeros por caja, conservar el más reciente.
DELETE FROM public.adm_caja_usuario a
USING public.adm_caja_usuario b
WHERE a.company_id = b.company_id
  AND a.caja_id = b.caja_id
  AND a.caja_usuario_id < b.caja_usuario_id;

CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_caja_usuario_caja
    ON public.adm_caja_usuario (company_id, caja_id);

COMMENT ON TABLE public.adm_caja_usuario IS
'Asignación cajero ↔ caja física 1:1: un usuario pertenece a UNA caja y una
caja tiene UN cajero. Reasignar = quitar la asignación y crear la nueva.
Unificación cobranza (ajuste 2026-07-27).';

ALTER TABLE public.sesion_caja
    ADD COLUMN IF NOT EXISTS monto_apertura numeric(18,2);

COMMENT ON COLUMN public.sesion_caja.monto_apertura IS
'Fondo inicial de la caja al abrir el turno (opcional). Se muestra en el cierre
junto al total cobrado del sistema.';

COMMIT;

-- Smoke: una caja no acepta dos cajeros
DO $$
DECLARE
    v_caja int;
    v_fallo boolean := false;
BEGIN
    SELECT caja_id INTO v_caja FROM public.adm_caja WHERE company_id = 2 LIMIT 1;
    DELETE FROM public.adm_caja_usuario WHERE usuario IN ('smoke_a', 'smoke_b');
    INSERT INTO public.adm_caja_usuario (company_id, caja_id, usuario) VALUES (2, v_caja, 'smoke_a');
    BEGIN
        INSERT INTO public.adm_caja_usuario (company_id, caja_id, usuario) VALUES (2, v_caja, 'smoke_b');
    EXCEPTION WHEN unique_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_caja_usuario aceptó dos cajeros en la misma caja';
    END IF;
    DELETE FROM public.adm_caja_usuario WHERE usuario = 'smoke_a';
    RAISE NOTICE 'Smoke OK: una caja, un cajero';
END $$;
