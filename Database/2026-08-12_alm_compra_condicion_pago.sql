-- =============================================================================
-- Compras: condición de pago explícita en la factura (alm_compra_hdr.condicion_pago)
-- Fecha: 2026-08-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Campo estructurado que reemplaza la inferencia de contado/crédito por el texto/plazo:
--   1 = Contado · 2 = Crédito · 3 = Prepagado
-- El servidor lo deriva del término de pago (0 días → Contado, >0 → Crédito). Es el
-- prerrequisito de la cuenta por pagar (Fase 1): decide si la factura genera CxP y con qué
-- vencimiento. Ver docs/centura-flujos/README_terminos_pago_factura_proveedor.md §7.4 / §9.
--
-- ADITIVO / bajo riesgo: ADD COLUMN con DEFAULT + CHECK. No borra ni reescribe otros datos;
-- las facturas existentes quedan en 1 (Contado) por el default.
-- IDEMPOTENTE: ADD COLUMN IF NOT EXISTS + guard del CHECK por pg_constraint.
-- Depende de: alm_compra_hdr (paso 25).
-- =============================================================================
BEGIN;

ALTER TABLE alm_compra_hdr
    ADD COLUMN IF NOT EXISTS condicion_pago SMALLINT NOT NULL DEFAULT 1;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_compra_hdr_condicion_pago') THEN
        ALTER TABLE alm_compra_hdr
            ADD CONSTRAINT ck_alm_compra_hdr_condicion_pago CHECK (condicion_pago IN (1, 2, 3));
    END IF;
END $$;

COMMENT ON COLUMN alm_compra_hdr.condicion_pago IS 'Condición de pago de la factura: 1=Contado, 2=Crédito, 3=Prepagado. Derivada del término (0 días=Contado, >0=Crédito). Decide la generación de CxP.';

COMMIT;
