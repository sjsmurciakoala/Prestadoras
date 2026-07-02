-- =============================================================================
-- ADD activo TO axl_accion_cobranza AND axl_observacion_cobranza
-- Fecha: 2026-06-05
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- =============================================================================

BEGIN;

ALTER TABLE axl_accion_cobranza
    ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;

ALTER TABLE axl_observacion_cobranza
    ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;

COMMIT;
