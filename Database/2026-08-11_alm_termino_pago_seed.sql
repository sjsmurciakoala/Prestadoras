-- =============================================================================
-- Almacén / Compras: términos de pago base del proveedor (semilla)
-- Fecha: 2026-08-11
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Datos base para el catálogo alm_termino_pago (creado por 2026-08-11_alm_termino_pago.sql).
-- Contado (0 días) queda como predeterminado. El usuario puede editar/agregar/desactivar
-- desde la pantalla /almacen/terminos-pago.
--
-- ⚠️ Asume company_id = 2 (el tenant del mirror). Ajustar el company_id antes de aplicar en
--    otra empresa. En el SRV es OPCIONAL: la empresa puede preferir definir sus propios términos.
-- IDEMPOTENTE: ON CONFLICT (company_id, nombre) DO NOTHING — re-ejecutable.
-- =============================================================================
BEGIN;

INSERT INTO alm_termino_pago (company_id, nombre, dias, es_default, activo, usuariocreacion, fechacreacion)
VALUES
    (2, 'Contado',          0, true,  true, 'seed_termino_pago', (now() AT TIME ZONE 'utc')),
    (2, 'Crédito 15 días', 15, false, true, 'seed_termino_pago', (now() AT TIME ZONE 'utc')),
    (2, 'Crédito 30 días', 30, false, true, 'seed_termino_pago', (now() AT TIME ZONE 'utc')),
    (2, 'Crédito 45 días', 45, false, true, 'seed_termino_pago', (now() AT TIME ZONE 'utc')),
    (2, 'Crédito 60 días', 60, false, true, 'seed_termino_pago', (now() AT TIME ZONE 'utc'))
ON CONFLICT (company_id, nombre) DO NOTHING;

COMMIT;
