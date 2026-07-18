-- =============================================================================
-- Proveedores: tipo de cuenta bancaria (cheques / ahorro)
-- Fecha: 2026-07-15
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- prv_proveedor_cuenta_bancaria.tipo_cuenta
--   Distingue si la cuenta del proveedor es de cheques o de ahorro, para que los
--   pagos (transferencias / cheques) sepan a qué tipo de cuenta van dirigidos.
--   Valores esperados: 'CHEQUES' | 'AHORRO' — la misma convención que ya usa la
--   cuenta bancaria de la empresa (bnc_cuenta.tipo_cuenta y el catálogo de
--   CuentaBancariaGestion), de modo que IsChequeBankAccount ("CHEQ") la entiende.
--   Se deja NULL en filas existentes = "no especificado"; la validación de valor
--   se aplica en la capa de servicio (C#), no con CHECK, para no romper cargas
--   históricas.
--
-- Cambio aditivo: no altera columnas ni datos existentes.
-- =============================================================================
BEGIN;

ALTER TABLE prv_proveedor_cuenta_bancaria
    ADD COLUMN IF NOT EXISTS tipo_cuenta VARCHAR(20);

COMMENT ON COLUMN prv_proveedor_cuenta_bancaria.tipo_cuenta IS
    'Tipo de la cuenta bancaria del proveedor: CHEQUES | AHORRO. NULL = no especificado (filas previas al 2026-07-15). Validación de valores en la capa de servicio.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- SELECT column_name, data_type, character_maximum_length, is_nullable
--   FROM information_schema.columns
--  WHERE table_name = 'prv_proveedor_cuenta_bancaria' AND column_name = 'tipo_cuenta';
--   -> character varying / 20 / YES
--
-- SELECT tipo_cuenta, count(*) FROM prv_proveedor_cuenta_bancaria GROUP BY 1;
--   -> todas NULL al aplicar (se capturan desde el formulario de proveedor).
-- =============================================================================
