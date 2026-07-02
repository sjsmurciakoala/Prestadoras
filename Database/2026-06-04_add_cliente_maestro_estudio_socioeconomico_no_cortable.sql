-- =============================================================================
-- ALTER: cliente_maestro — agregar columnas estudio_socioeconomico y no_cortable
-- Fecha: 2026-06-04
-- Motivo: campos necesarios para módulo de cobranza (visualización en ClienteDetail)
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- =============================================================================

BEGIN;

ALTER TABLE cliente_maestro
    ADD COLUMN IF NOT EXISTS maestro_cliente_estudio_socioeconomico BOOLEAN,
    ADD COLUMN IF NOT EXISTS no_cortable                             BOOLEAN;

COMMENT ON COLUMN cliente_maestro.maestro_cliente_estudio_socioeconomico
    IS 'Indica si el cliente tiene estudio socioeconómico realizado';

COMMENT ON COLUMN cliente_maestro.no_cortable
    IS 'Indica que el cliente no puede ser sujeto de corte de servicio';

COMMIT;
