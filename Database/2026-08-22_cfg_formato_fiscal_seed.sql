-- =============================================================================
-- Configuración: semilla del catálogo de formatos fiscales (cfg_formato_fiscal)
-- Fecha: 2026-08-22
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
-- Requiere: 2026-08-22_cfg_formato_fiscal.sql (estructura) — aplicar ANTES.
--
-- Siembra los dos formatos que se cablean en la recepción de compra:
--   NUMERO_SAR — formato SAR de Honduras: EEE-PPP-TD-NNNNNNNN (3+3+2+8 dígitos).
--   CAI        — 32 hexadecimales en 6 grupos: 6-6-6-6-6-2.
-- Ambos en modo 3 (bloquea el guardado), no obligatorios: si el campo se deja vacío
-- se guarda vacío; si se llena, tiene que cumplir el formato.
--
-- ⚠️ ASUME company_id = 2 (el tenant del mirror). En el servidor la empresa puede
-- preferir definir los suyos desde la pantalla /mantenimientos/formatos-fiscales:
-- este script es OPCIONAL.
--
-- Cambio de DATOS, aditivo. No crea ni altera estructura, no borra nada.
-- IDEMPOTENTE: ON CONFLICT (company_id, codigo) DO NOTHING.
-- =============================================================================
BEGIN;

INSERT INTO public.cfg_formato_fiscal
    (company_id, codigo, nombre, mascara, patron, modo_validacion,
     obligatorio, normalizar, mayusculas, activo, usuariocreacion, fechacreacion)
VALUES
    (2, 'NUMERO_SAR', 'No. factura (SAR)',
     '###-###-##-########', NULL, 3,
     false, true, true, true, 'seed_formato_fiscal', (now() AT TIME ZONE 'utc')),
    (2, 'CAI', 'CAI',
     'HHHHHH-HHHHHH-HHHHHH-HHHHHH-HHHHHH-HH', NULL, 3,
     false, true, true, true, 'seed_formato_fiscal', (now() AT TIME ZONE 'utc'))
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- SELECT company_id, codigo, nombre, mascara, modo_validacion, obligatorio, activo
--   FROM public.cfg_formato_fiscal
--  ORDER BY company_id, codigo;      -- esperado: 2 filas para company_id = 2
--
-- Idempotencia — volver a correr no debe mover el conteo:
-- SELECT count(*) FROM public.cfg_formato_fiscal WHERE company_id = 2;   -- esperado: 2
-- =============================================================================
