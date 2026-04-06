-- =============================================================================
-- Migración: Agregar cont_account_id a miscelaneos_catalogo
-- Fecha: 2026-03-14
-- Branch: feature/facturacion-miscelaneos-ui-contabilidad-auto
-- Propósito: Habilitar cuenta contable por concepto misceláneo
-- =============================================================================

-- 1. Agregar columna cont_account_id (nullable en BD, obligatoria en flujo)
ALTER TABLE public.miscelaneos_catalogo
    ADD COLUMN IF NOT EXISTS cont_account_id bigint NULL;

-- 2. Crear FK hacia con_plan_cuentas
ALTER TABLE public.miscelaneos_catalogo
    DROP CONSTRAINT IF EXISTS fk_miscelaneos_catalogo_cont_account;

ALTER TABLE public.miscelaneos_catalogo
    ADD CONSTRAINT fk_miscelaneos_catalogo_cont_account
    FOREIGN KEY (cont_account_id)
    REFERENCES public.con_plan_cuentas (account_id)
    ON DELETE RESTRICT;

-- 3. Crear índice sobre cont_account_id
CREATE INDEX IF NOT EXISTS ix_miscelaneos_catalogo_cont_account_id
    ON public.miscelaneos_catalogo (cont_account_id);
