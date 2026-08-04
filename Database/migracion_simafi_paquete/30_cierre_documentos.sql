-- =============================================================================
-- 30 — CIERRE: las dos brechas conocidas del control de documentos
-- =============================================================================
-- Envoltorio de M3c. Cierra: (a) el doble conteo con lo que el PORTAL emitió
-- durante el piloto (847 clientes, L 1.82M) y (b) los clientes con historia en
-- el ledger pero ausentes del maestro volcado (124, L 0.20M). Si en el
-- servidor hubo MÁS actividad de portal que en julio, este paso la reconcilia
-- con la misma técnica — revisar su salida con calma antes de seguir.

SET siad.permitir_escritura_legacy = 'on';
\ir ../2026-07-29_m3c_cierre_migracion_documentos.sql
RESET siad.permitir_escritura_legacy;
