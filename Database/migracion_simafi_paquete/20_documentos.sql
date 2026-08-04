-- =============================================================================
-- 20 — DOCUMENTOS: ledger SIMAFI → factura + factura_detalle + movimientos
-- =============================================================================
-- Envoltorio de: prep (tabla de trabajo _m3_factura: cabeceras cliente+recibo)
-- + M3b (la carga pesada). De 3.5 a 4.7 HORAS medidas en disco mecánico.
-- Estrategia por bloques (un NOT EXISTS por fila proyectaba DÍAS; no cambiarla).
--
-- CANDADO: estos scripts son ANTERIORES al congelamiento de transaccion_abonado
-- (F7 H4) y escriben en ella. El SET de sesión de abajo es el interruptor
-- oficial para migraciones; se cierra al final del archivo.
-- Correr DESDE esta carpeta.

SET siad.permitir_escritura_legacy = 'on';

\ir ../../docs/simafi_m2/m3b_prep.sql
\ir ../2026-07-28_m3b_carga_documentos_simafi.sql

RESET siad.permitir_escritura_legacy;

\echo '=== Verificación 20 — referencia local: 3.90M facturas / 9.38M líneas / 12.17M movimientos ==='
SELECT (SELECT count(*) FROM factura WHERE company_id = 2)             AS facturas,
       (SELECT count(*) FROM factura_detalle WHERE company_id = 2)     AS lineas,
       (SELECT count(*) FROM transaccion_abonado WHERE company_id = 2) AS movimientos;
