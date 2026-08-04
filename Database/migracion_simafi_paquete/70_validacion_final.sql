-- =============================================================================
-- 70 — ACEPTACIÓN (M6): si esto no cuadra, NO SE CONTINÚA
-- =============================================================================
-- Criterio: saldo por cliente contra el LEDGER RECONSTRUIDO del origen
-- (suma de débitos menos créditos por cliente). NUNCA contra clientesaldos
-- (snapshot desfasado del origen; solo informativo).
-- Resultado de referencia local (2026-07-29): 25,530 de 25,530 clientes con
-- saldo IDÉNTICO, total L 48,858,786.58, cero huérfanos.
-- Si hay diferencias: docs/simafi_m2/m6b.sql las desglosa por cliente/causa.
-- Diferencias conocidas y aceptadas: 5 cargos sin recibo en el origen.

\ir ../../docs/simafi_m2/m6.sql
