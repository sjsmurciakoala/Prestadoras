-- =============================================================================
-- Bancos: agrega 'COMPRA_CXP' al CHECK ck_ban_cheque_origen.
-- Fecha: 2026-08-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor (siad_v3).
--
-- Bug: el pago de una cuenta por pagar de compra con método CHEQUE emite el cheque con
-- origen ChequeOrigen.CompraCxp = 'COMPRA_CXP' (SIAD.Core/DTOs/Bancos/ChequesDtos.cs).
-- El CHECK ck_ban_cheque_origen sólo permitía PROCESAR / ABONO / TRANSACCION / MANUAL, así
-- que el INSERT en ban_cheque fallaba (violación de ck_ban_cheque_origen) y el pago con
-- cheque se revertía por completo. Los pagos en efectivo y por transferencia no tocan
-- ban_cheque, por eso no se veía; los tests de CompraCxp usan transferencia, no cheque.
--
-- Este script amplía el CHECK para incluir 'COMPRA_CXP'. Aditivo (sólo agrega un valor
-- permitido), NO destructivo, idempotente (DROP IF EXISTS + ADD con la lista completa),
-- re-ejecutable. No asume ningún company_id.
-- =============================================================================
BEGIN;

ALTER TABLE public.ban_cheque DROP CONSTRAINT IF EXISTS ck_ban_cheque_origen;

ALTER TABLE public.ban_cheque ADD CONSTRAINT ck_ban_cheque_origen
    CHECK (origen::text = ANY (ARRAY['PROCESAR','ABONO','TRANSACCION','MANUAL','COMPRA_CXP']::text[]));

COMMIT;

-- =============================================================================
-- Verificación (¿ya aplicado?): debe listar el CHECK con 'COMPRA_CXP' incluido.
-- =============================================================================
-- SELECT pg_get_constraintdef(oid) AS def
--   FROM pg_constraint WHERE conname = 'ck_ban_cheque_origen';
