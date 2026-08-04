-- =============================================================================
-- 01 — VERIFICACIÓN PREVIA (pre-vuelo). Solo LECTURA: no cambia nada.
-- =============================================================================
-- Correr ANTES de cualquier paso, en la base destino. Cada bloque dice qué
-- esperar. Si algo no da lo esperado, resolver ANTES de seguir (ver LEEME).

\echo '=== 1. Staging simafi_stg presente y con volumen (esperado ~18.1M filas en 7 tablas) ==='
SELECT n.nspname AS esquema, c.relname AS tabla, c.reltuples::bigint AS filas_aprox
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'simafi_stg' AND c.relkind = 'r'
ORDER BY c.relname;

\echo '=== 2. Espacio en disco de la base (la carga completa agrega ~8-10 GB) ==='
SELECT pg_size_pretty(pg_database_size(current_database())) AS tamano_actual;

\echo '=== 3. Encoding de la sesión (DEBE decir UTF8 — si no, salir y setear PGCLIENTENCODING) ==='
SHOW client_encoding;

\echo '=== 4. El candado del legacy existe (esperado: 1 trigger congelador) ==='
SELECT count(*) AS trigger_congelador
FROM pg_trigger WHERE tgname = 'trg_transaccion_abonado_congelada';

\echo '=== 5. Estado actual de la cartera destino (para comparar el después) ==='
SELECT (SELECT count(*) FROM cliente_maestro WHERE company_id = 2)      AS clientes,
       (SELECT count(*) FROM factura WHERE company_id = 2)              AS facturas,
       (SELECT count(*) FROM transaccion_abonado WHERE company_id = 2)  AS movimientos,
       (SELECT count(*) FROM adm_pago WHERE company_id = 2)             AS pagos,
       (SELECT count(*) FROM adm_pago_aplicacion)                       AS aplicaciones;

\echo '=== 6. Auditoría letra vs estado_id (esperado: solo pares A/1, B/4, C/2, N/3) ==='
SELECT estado, estado_id, count(*) FROM factura GROUP BY 1, 2 ORDER BY 1, 2;

\echo '=== 7. RECORDATORIO: ¿ya corriste el backup completo? (backup_bd_simple.ps1) ==='
\echo 'Sin backup NO hay rollback total. No continuar sin él.'
