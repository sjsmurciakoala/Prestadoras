-- =============================================================================
-- 40 — CORRECCIÓN DE CRITERIO: cargo = debitos > 0 (no tipo_partida 01)
-- =============================================================================
-- Envoltorio de M3d. Repara las dos trampas cuantificadas del origen:
--   * ND/reconexiones/cortes (transaccion 105) con tipo 02 y DÉBITO real:
--     faltaban 17,148 documentos (L 6.68M).
--   * filas tipo 01 con débito CERO: facturas y líneas en cero que sobraban.
-- Cuadre exacto comprobado: 1,414,578,353.51 - 1,407,893,410.61.

SET siad.permitir_escritura_legacy = 'on';
\ir ../2026-07-29_m3d_correccion_cargos_y_pagos.sql
RESET siad.permitir_escritura_legacy;
