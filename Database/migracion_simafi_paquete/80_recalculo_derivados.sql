-- =============================================================================
-- 80 — DERIVADOS POST-CARGA: estado_id y auditoría (lección del 02-08)
-- =============================================================================
-- Las cargas masivas corren con triggers deshabilitados → factura.estado_id
-- queda descuadrado de la letra en las filas tocadas. Este paso lo recalcula
-- y su verificación interna REVIENTA si queda un solo descuadre.

\ir ../2026-08-02_saneo_factura_estado_id.sql

\echo '=== Verificación 80: solo pares A/1, B/4, C/2, N/3 ==='
SELECT estado, estado_id, count(*) FROM factura GROUP BY 1, 2 ORDER BY 1, 2;
