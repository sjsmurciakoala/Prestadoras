-- =============================================================================
-- Almacén: flag "notificar por correo" en los conceptos de movimiento (salidas)
-- Fecha: 2026-08-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ
-- Los movimientos genéricos de almacén usan un concepto (alm_tipo_movimiento: "Merma",
-- "Donación"…) de clase ENTRADA/SALIDA/VALOR. Se quiere decidir, POR CONCEPTO, si una salida
-- con ese concepto dispara el aviso por correo (al área ALMACEN) cuando un artículo cruza bajo
-- mínimo. Esta columna es ese interruptor; solo tiene efecto en conceptos de clase SALIDA.
--
-- Cambio ADITIVO: una columna nueva con DEFAULT false. No altera datos ni otras columnas; los
-- conceptos existentes quedan con el flag apagado (comportamiento actual). Idempotente y
-- re-ejecutable (ADD COLUMN IF NOT EXISTS).
-- =============================================================================
BEGIN;

ALTER TABLE alm_tipo_movimiento
    ADD COLUMN IF NOT EXISTS notifica_correo BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN alm_tipo_movimiento.notifica_correo IS
    'Solo aplica a clase SALIDA: si true, un movimiento genérico con este concepto envía aviso por correo al área ALMACEN cuando un artículo cruza bajo mínimo. Default false = no notifica.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- SELECT column_name, data_type, column_default, is_nullable
--   FROM information_schema.columns
--  WHERE table_name = 'alm_tipo_movimiento' AND column_name = 'notifica_correo';
--   -> boolean | false | NO
-- =============================================================================
