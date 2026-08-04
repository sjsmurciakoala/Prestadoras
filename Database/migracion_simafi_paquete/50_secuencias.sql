-- =============================================================================
-- 50 — SECUENCIAS: resembrar la numeración de recibos tras la carga
-- =============================================================================
-- Envoltorio de M3e. La migración trae la numeración original de SIMAFI
-- (numrecibo hasta 4.19M; recibos de pago hasta 15.43M) y la serie del portal
-- quedaría por debajo → folios duplicados. Esto la deja adelante del máximo.

\ir ../2026-07-29_m3e_resembrar_secuencia_recibo.sql
