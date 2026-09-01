-- =============================================================================
-- Retenciones en "Pagos a proveedores" (CxP de compras): unificación en el libro fiscal
-- Fecha: 2026-08-18
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Permite RETENER al pagar una factura de compra (alm_compra_cxp_abono) reusando el mismo
-- libro fiscal de retenciones (prv_retencion_hdr/dtl) del flujo de compromisos, con un
-- discriminador de ORIGEN. Así la constancia y la declaración mensual (SAR) toman ambos
-- orígenes sin duplicar reportes ni folios (decisión del usuario: "completo, unificado").
--
-- ADITIVO salvo aflojar el NOT NULL de prv_retencion_hdr.numero_orden (las retenciones de
-- compra no vienen de un compromiso). No borra ni cambia datos: las filas OPD existentes
-- reciben origen=1 por default y cumplen el nuevo CHECK; la FK y el índice de compras solo
-- afectan filas nuevas de origen=2.
-- IDEMPOTENTE: ADD COLUMN IF NOT EXISTS / CREATE INDEX IF NOT EXISTS / guardas por pg_constraint.
-- Depende de: alm_compra_cxp / alm_compra_cxp_abono (2026-08-12) y
--             prv_retencion_hdr / prv_retencion_dtl (2026-08-07).
-- =============================================================================
BEGIN;

-- 1. Monto retenido del pago de compra ---------------------------------------
-- El BRUTO (lo aplicado a la deuda) sigue en alm_compra_cxp_abono.monto; el NETO pagado al
-- banco/caja = monto - retenido. El saldo de la CxP baja por el bruto (= monto).
ALTER TABLE alm_compra_cxp_abono
    ADD COLUMN IF NOT EXISTS retenido NUMERIC(14,2) NOT NULL DEFAULT 0;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_compra_cxp_abono_retenido') THEN
        ALTER TABLE alm_compra_cxp_abono
            ADD CONSTRAINT ck_alm_compra_cxp_abono_retenido CHECK (retenido >= 0);
    END IF;
END $$;

COMMENT ON COLUMN alm_compra_cxp_abono.retenido IS
    'Suma retenida en este pago. El bruto está en monto; el neto pagado al banco/caja = monto - retenido.';

-- 2. Libro fiscal unificado: origen del pago (compromiso / compra) -----------
-- numero_orden pasa a ANULABLE: las retenciones de compra no vienen de un compromiso. La FK
-- fk_prv_retencion_hdr_compromiso existente (company_id, numero_orden) es MATCH SIMPLE, así que
-- una fila con numero_orden NULL queda exenta de esa FK (no requiere compromiso).
ALTER TABLE prv_retencion_hdr ALTER COLUMN numero_orden DROP NOT NULL;

-- Discriminador de origen (1 compromiso/OPD, 2 compra/CxP) + referencia a la CxP.
ALTER TABLE prv_retencion_hdr ADD COLUMN IF NOT EXISTS origen SMALLINT NOT NULL DEFAULT 1;
ALTER TABLE prv_retencion_hdr ADD COLUMN IF NOT EXISTS cxp_id INTEGER NULL;

-- FK a la CxP de compra. MATCH SIMPLE (default): las filas OPD (cxp_id NULL) quedan exentas.
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prv_retencion_hdr_cxp') THEN
        ALTER TABLE prv_retencion_hdr
            ADD CONSTRAINT fk_prv_retencion_hdr_cxp
            FOREIGN KEY (company_id, cxp_id)
            REFERENCES alm_compra_cxp (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

-- Coherencia origen ↔ referencia: OPD lleva numero_orden y no cxp; compra lleva cxp y no orden.
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_prv_retencion_hdr_origen') THEN
        ALTER TABLE prv_retencion_hdr
            ADD CONSTRAINT ck_prv_retencion_hdr_origen CHECK (
                (origen = 1 AND numero_orden IS NOT NULL AND cxp_id IS NULL) OR
                (origen = 2 AND cxp_id IS NOT NULL AND numero_orden IS NULL)
            );
    END IF;
END $$;

-- Unicidad del pago para compras: la UNIQUE existente (company_id, numero_orden, numero_abono)
-- no cubre las filas de compra (numero_orden NULL ⇒ Postgres las trata como distintas).
CREATE UNIQUE INDEX IF NOT EXISTS uq_prv_retencion_hdr_cxp_pago
    ON prv_retencion_hdr (company_id, cxp_id, numero_abono) WHERE origen = 2;

COMMENT ON COLUMN prv_retencion_hdr.origen IS
    'Origen del pago retenido: 1 compromiso (OPD), 2 factura de compra (alm_compra_cxp).';
COMMENT ON COLUMN prv_retencion_hdr.cxp_id IS
    'CxP de compra origen (alm_compra_cxp.id) cuando origen=2; NULL para compromisos.';

COMMIT;
