-- =============================================================================
-- Fix bug #8 (detectado 2026-05-15 por SIAD.Tests/AnulacionTests):
--   sp_adm_emitir_nota_credito (20260514_nc_nd_v3_modelo.sql:441) escribe
--   factura.updated_at = now() al anular total una factura via NC, pero la
--   columna NO existía → la anulación TOTAL via NC fallaba con 42703.
--
-- Fecha: 2026-05-16
-- Plan: PLAN_ENTREGA_2026-05-25.md (Sprint 3 día 12)
--
-- Decisión: opción A — agregar la columna (audit trail explícito).
-- Las filas históricas quedan NULL; nuevas anulaciones quedarán timestamped.
--
-- Idempotente.
-- =============================================================================

ALTER TABLE public.factura
    ADD COLUMN IF NOT EXISTS updated_at timestamptz NULL;

COMMENT ON COLUMN public.factura.updated_at IS
'Timestamp de la última modificación (anulación via NC, ajustes auditados). '
'NULL para filas históricas previas al 2026-05-16.';
