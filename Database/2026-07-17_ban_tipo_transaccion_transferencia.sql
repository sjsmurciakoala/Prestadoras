-- =============================================================================
-- Bancos: tipo de transaccion de TRANSFERENCIA (salida) para el tenant
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE
-- El pago a proveedores por TRANSFERENCIA (medio de pago en "Emitir pago") resuelve el
-- tipo de transaccion bancaria via ResolveBankTransactionTypeAsync: busca en
-- ban_tipos_transacciones una fila de la company con entra_sale='S' (salida), estado
-- activo, y cuyo nombre haga match con TRANSFER (nombre ILIKE '%TRANSFER%' o
-- tipo_transaccion ILIKE '%TRF%'/'%TRANSFER%'). El tenant (company 2) NO tenia ninguna,
-- por lo que la transferencia fallaba con "No existe un tipo de transaccion bancaria
-- activo configurado para el metodo de pago TRANSFERENCIA". El cheque si funcionaba
-- (fila CHQ 'Cheques').
--
-- Se crea la fila 'TRF' (Transferencia), modelada sobre la de cheques (CHQ) que ya
-- opera en este flujo, cambiando lo propio de una transferencia:
--   - tipo_transaccion='TRF', nombre='Transferencia' (para el match del filtro).
--   - emite_cheque='N' (una transferencia no emite cheque).
--   - correlativo propio '000000' (no comparte el contador de cheques).
--   - cod_tipopartida='5' igual que CHQ (ambos son pagos de salida). El flujo de
--     OrdenesPagoDirecto genera su PROPIA partida y solo enlaza el kardex por
--     tipo_transaccion, asi que cod_tipopartida/correlativo aqui son metadato para los
--     flujos propios del modulo de Bancos. Ajustar si el contador quiere otro tipo de
--     partida para transferencias.
--
-- Nota: ban_tipo_transaccion_id es IDENTITY ALWAYS -> NO se inserta (lo genera la BD).
-- Idempotente (WHERE NOT EXISTS). Aditivo y reversible. Ajustar company_id si el tenant
-- en el SRV no fuera 2.
-- =============================================================================
BEGIN;

INSERT INTO public.ban_tipos_transacciones
    (company_id, tipo_transaccion, cod_tipopartida, correlativo, nombre,
     entra_sale, del_sistema, emite_cheque, pad, pda, cuenta_alterna, estado, created_by)
SELECT 2, 'TRF', '5', '000000', 'Transferencia',
       'S', 'N', 'N', 'N', 'S', FALSE, 'ACTIVE', 'sistema'
 WHERE NOT EXISTS (
     SELECT 1 FROM public.ban_tipos_transacciones
      WHERE company_id = 2
        AND (upper(btrim(tipo_transaccion)) = 'TRF' OR nombre ILIKE '%transferencia%')
 );

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- Debe devolver la fila TRF (lo mismo que resuelve ResolveBankTransactionTypeAsync):
-- SELECT tipo_transaccion, nombre, entra_sale, estado
--   FROM public.ban_tipos_transacciones
--  WHERE company_id = 2 AND entra_sale = 'S'
--    AND (estado IS NULL OR estado = '' OR upper(estado) IN ('ACTIVE','ACTIVO'))
--    AND (nombre ILIKE '%TRANSFER%' OR tipo_transaccion ILIKE '%TRF%'
--         OR tipo_transaccion ILIKE '%TRANSFER%');
-- =============================================================================
