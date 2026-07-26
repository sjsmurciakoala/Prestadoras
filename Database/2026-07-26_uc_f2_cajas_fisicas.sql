-- =============================================================================
-- Unificación de cobranza — F2 (complemento): cajas físicas por empresa
-- Fecha: 2026-07-26
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md
--
-- La empresa opera con VARIAS cajas (ventanillas) simultáneas. "Pantalla única
-- de caja" = un solo módulo/motor, N cajas físicas: cada cajero abre su sesión
-- EN una caja concreta y el arqueo sale por caja.
--
-- Hoy: catalogo_cajas existe pero está vacío, sin company_id y sin uso real
-- (solo el combo decorativo de Lectoras); sesion_caja no dice en qué caja está
-- el cajero. Este script crea el catálogo real tenant-scoped y amarra la sesión.
-- catalogo_cajas queda deprecado (retiro en F7; pagos_hdr aún lo referencia).
--
-- Idempotente. Aplicar en local (siad_v3_copia09) y test (siad_v3_test).
-- NO aplicar en 0.9 hasta la ventana de deploy del plan.
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) Catálogo de cajas físicas (tenant-scoped)
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.adm_caja (
    caja_id        int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id     bigint NOT NULL REFERENCES public.cfg_company (company_id),
    codigo         varchar(20) NOT NULL,
    nombre         varchar(120) NOT NULL,
    activo         boolean NOT NULL DEFAULT true,
    creado_en      timestamptz NOT NULL DEFAULT now(),
    actualizado_en timestamptz,
    updated_by     varchar(100),
    CONSTRAINT uq_adm_caja_codigo UNIQUE (company_id, codigo)
);

COMMENT ON TABLE public.adm_caja IS
'Cajas físicas (ventanillas) por empresa. Cada sesión de caja se abre EN una
caja; varias cajas operan simultáneamente, cada una con su cajero y su arqueo.
Reemplaza a catalogo_cajas (vacía, sin tenant, sin uso — retiro en F7).
Unificación cobranza F2 (2026-07-26).';

-- Seed: una caja principal por empresa para que la operación no se detenga;
-- las demás se administran desde el portal (F3).
INSERT INTO public.adm_caja (company_id, codigo, nombre, updated_by)
SELECT c.company_id, 'CAJA-01', 'Caja principal', 'seed-uc-f2'
FROM public.cfg_company c
ON CONFLICT (company_id, codigo) DO NOTHING;

-- ============================================================================
-- 2) La sesión se abre EN una caja física
--    (nullable durante la transición: la UI de apertura la exige desde F3)
-- ============================================================================
ALTER TABLE public.sesion_caja
    ADD COLUMN IF NOT EXISTS caja_fisica_id int
        REFERENCES public.adm_caja (caja_id);

COMMENT ON COLUMN public.sesion_caja.caja_fisica_id IS
'Caja física (adm_caja) donde está abierta la sesión. Una sola sesión ABIERTA
por caja (índice parcial). NULL solo para sesiones legacy pre-F3.';

-- Una sola sesión ABIERTA por caja física (además de la regla existente de
-- una sesión abierta por usuario/empresa).
CREATE UNIQUE INDEX IF NOT EXISTS uq_sesion_caja_fisica_abierta
    ON public.sesion_caja (company_id, caja_fisica_id)
    WHERE estado = 'ABIERTA' AND caja_fisica_id IS NOT NULL;

COMMIT;

-- =============================================================================
-- SMOKE TEST
-- =============================================================================
DO $$
DECLARE
    v_caja_id int;
    v_s1 int;
    v_fallo boolean := false;
BEGIN
    SELECT caja_id INTO v_caja_id FROM public.adm_caja
    WHERE company_id = 2 AND codigo = 'CAJA-01';
    IF v_caja_id IS NULL THEN
        RAISE EXCEPTION 'Seed de adm_caja no creó CAJA-01 para company 2';
    END IF;

    INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado, caja_fisica_id)
    VALUES (2, 'smoke_cajero_1', now(), 'ABIERTA', v_caja_id)
    RETURNING id INTO v_s1;

    -- Segunda sesión abierta en la MISMA caja debe rechazarse
    BEGIN
        INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado, caja_fisica_id)
        VALUES (2, 'smoke_cajero_2', now(), 'ABIERTA', v_caja_id);
    EXCEPTION WHEN unique_violation THEN
        v_fallo := true;
    END;
    IF NOT v_fallo THEN
        RAISE EXCEPTION 'sesion_caja aceptó dos sesiones ABIERTAS en la misma caja física';
    END IF;

    DELETE FROM public.sesion_caja WHERE id = v_s1;
    RAISE NOTICE 'Smoke OK: una sesión abierta por caja física';
END $$;
