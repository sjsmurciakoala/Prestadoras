-- =============================================================================
-- Compras: backfill de cuentas por pagar de las facturas YA registradas
-- Fecha: 2026-08-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- La generación de CxP (2026-08-12_alm_compra_cxp.sql + binario) es "hacia adelante": las
-- facturas registradas ANTES no tienen su alm_compra_cxp. Este script las genera con el
-- estado que corresponde:
--   · factura vigente (estado <> 9) y no prepagada  → CxP Pendiente, saldo = total
--   · factura ANULADA (estado = 9)                  → NO se genera (no hay CxP que pagar)
--   · condicion_pago = 3 (Prepagada)                → NO se genera (ya está pagada)
-- No hay abonos históricos en el nuevo motor, así que todas nacen Pendiente con saldo = total.
--
-- DATOS idempotente: INSERT … WHERE NOT EXISTS. Re-ejecutable (no duplica).
-- Depende de: alm_compra_cxp (2026-08-12_alm_compra_cxp.sql) y condicion_pago (§3.9).
-- =============================================================================
BEGIN;

INSERT INTO alm_compra_cxp
    (company_id, compra_hdr_id, cod_proveedor, proveedor, numero_factura_sar, fecha, fecha_vencimiento,
     condicion_pago, monto, saldo, estado_id, usuariocreacion, fechacreacion)
SELECT h.company_id, h.id, h.cod_proveedor, h.proveedor, h.numero_factura_sar, h.fecha,
       COALESCE(h.fecha_vencimiento, h.fecha),
       h.condicion_pago,
       h.total,
       h.total,   -- saldo = total (sin abonos previos)
       1,          -- EstadoCompraCxp.Pendiente
       'backfill_cxp', (now() AT TIME ZONE 'utc')
FROM alm_compra_hdr h
WHERE h.estado <> 9            -- las anuladas no llevan cuenta por pagar
  AND h.condicion_pago <> 3    -- las prepagadas ya están pagadas
  AND NOT EXISTS (
      SELECT 1 FROM alm_compra_cxp c
       WHERE c.company_id = h.company_id AND c.compra_hdr_id = h.id);

COMMIT;
