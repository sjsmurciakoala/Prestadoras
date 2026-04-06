-- =============================================================================
-- Migracion: Agregar soporte DETAIL_EXPAND a con_plantilla_partida_dtl
-- Fecha: 2026-03-14
-- Branch: feature/facturacion-miscelaneos-ui-contabilidad-auto
-- Proposito: Permitir lineas dinamicas en plantillas contables
-- =============================================================================

-- 1. Agregar columna line_mode (FIXED o DETAIL_EXPAND)
ALTER TABLE public.con_plantilla_partida_dtl
    ADD COLUMN IF NOT EXISTS line_mode varchar(20) NOT NULL DEFAULT 'FIXED';

-- 2. Agregar entry_side (D=debito, C=credito) para DETAIL_EXPAND
ALTER TABLE public.con_plantilla_partida_dtl
    ADD COLUMN IF NOT EXISTS entry_side char(1) NULL;

-- 3. Campos que indican que leer del JSON details[]
ALTER TABLE public.con_plantilla_partida_dtl
    ADD COLUMN IF NOT EXISTS detail_account_field varchar(50) NULL;

ALTER TABLE public.con_plantilla_partida_dtl
    ADD COLUMN IF NOT EXISTS detail_amount_field varchar(50) NULL;

ALTER TABLE public.con_plantilla_partida_dtl
    ADD COLUMN IF NOT EXISTS detail_description_field varchar(50) NULL;

-- 4. account_id pasa a nullable para soportar DETAIL_EXPAND
ALTER TABLE public.con_plantilla_partida_dtl
    ALTER COLUMN account_id DROP NOT NULL;
