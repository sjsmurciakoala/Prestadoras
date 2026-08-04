-- =============================================================================
-- 60 — PAGOS: adm_pago + aplicación a facturas por FIFO
-- =============================================================================
-- Envoltorio de M4. SIMAFI NO guarda a qué factura fue cada pago (verificado
-- en M2: docuaplicar vacío, redundante o roto en el 99.99% de los 2.47M) →
-- la aplicación se reconstruye por FIFO (la factura más vieja primero). Esto
-- NO altera el saldo por cliente (el criterio de M6); solo el desglose por
-- factura. Referencia local: 2.8M pagos, 9.4M aplicaciones, ~1 hora.

SET siad.permitir_escritura_legacy = 'on';
\ir ../2026-07-29_m4_aplicacion_pagos_fifo.sql
RESET siad.permitir_escritura_legacy;

\echo '=== Verificación 60 ==='
SELECT (SELECT count(*) FROM adm_pago WHERE company_id = 2) AS pagos,
       (SELECT count(*) FROM adm_pago_aplicacion)           AS aplicaciones;
