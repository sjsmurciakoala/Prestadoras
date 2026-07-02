-- ============================================================
-- Vinculación corte masivo → órdenes de trabajo
-- Al generar un lote de corte, cada cliente recibe una OT.
-- Este campo guarda el orden_id de la OT generada.
-- Fecha: 2026-06-04
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- ============================================================

ALTER TABLE cln_corte_masivo_dtl
    ADD COLUMN IF NOT EXISTS orden_id INTEGER;

COMMENT ON COLUMN cln_corte_masivo_dtl.orden_id
    IS 'FK → orden_trabajo.orden_id generada al crear el lote de corte';

CREATE INDEX IF NOT EXISTS ix_cln_corte_masivo_dtl_orden_id
    ON cln_corte_masivo_dtl(orden_id)
    WHERE orden_id IS NOT NULL;
