-- =============================================================================
-- Unificación de cobranza — F3: asignación de cajeros a cajas físicas
-- Fecha: 2026-07-26
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md (§5, F3)
--
-- Modelo (definido con el usuario 2026-07-26):
--   * Cada USUARIO pertenece a UNA caja (UNIQUE por usuario/empresa).
--   * Una caja puede tener VARIOS usuarios asignados (turnos).
--   * Al abrir sesión, el sistema resuelve automáticamente la caja del usuario
--     (ya no se elige de un combo). Sin asignación → no puede cobrar.
--   * Una sola sesión ABIERTA por caja (índice de F2).
--
-- Idempotente. Aplicar en local (siad_v3_copia09) y test (siad_v3_test).
-- NO aplicar en 0.9.
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.adm_caja_usuario (
    caja_usuario_id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint NOT NULL REFERENCES public.cfg_company (company_id),
    caja_id         int NOT NULL REFERENCES public.adm_caja (caja_id),
    usuario         varchar(100) NOT NULL,
    creado_en       timestamptz NOT NULL DEFAULT now(),
    updated_by      varchar(100),
    CONSTRAINT uq_adm_caja_usuario UNIQUE (company_id, usuario)
);

CREATE INDEX IF NOT EXISTS ix_adm_caja_usuario_caja
    ON public.adm_caja_usuario (company_id, caja_id);

COMMENT ON TABLE public.adm_caja_usuario IS
'Asignación cajero → caja física (adm_caja). Un usuario pertenece a UNA caja;
una caja admite varios usuarios (turnos). La apertura de sesión resuelve la
caja automáticamente desde aquí. Unificación cobranza F3 (2026-07-26).';

COMMIT;

-- Smoke: un usuario no puede estar en dos cajas
DO $$
DECLARE
    v_caja int;
    v_fallo boolean := false;
BEGIN
    SELECT caja_id INTO v_caja FROM public.adm_caja WHERE company_id = 2 LIMIT 1;
    INSERT INTO public.adm_caja_usuario (company_id, caja_id, usuario)
    VALUES (2, v_caja, 'smoke_asignacion');
    BEGIN
        INSERT INTO public.adm_caja_usuario (company_id, caja_id, usuario)
        VALUES (2, v_caja, 'smoke_asignacion');
    EXCEPTION WHEN unique_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'adm_caja_usuario aceptó al mismo usuario dos veces';
    END IF;
    DELETE FROM public.adm_caja_usuario WHERE usuario = 'smoke_asignacion';
    RAISE NOTICE 'Smoke OK: un usuario pertenece a una sola caja';
END $$;
