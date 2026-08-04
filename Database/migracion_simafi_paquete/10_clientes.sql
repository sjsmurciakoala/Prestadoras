-- =============================================================================
-- 10 — CLIENTES: simafi_stg.maestrosep → cliente_maestro (company_id = 2)
-- =============================================================================
-- Envoltorio del script validado M3a (idempotente: solo inserta claves que no
-- existen; puede repetirse sin duplicar). Resultado de referencia en local:
-- 24,623 insertados, 25,770 totales, 0 faltantes, 0 diferencias contra origen.
-- NULLs esperados y con razón verificada: rtn (el campo origen no es RTN),
-- barrio (catálogos incompatibles), tipo_uso (vacío en origen).
-- Correr DESDE esta carpeta: psql --set ON_ERROR_STOP=1 -f 10_clientes.sql

\ir ../2026-07-28_m3a_carga_clientes_simafi.sql

\echo '=== Verificación 10: conteo y faltantes (esperado: 0 faltantes) ==='
SELECT (SELECT count(*) FROM cliente_maestro WHERE company_id = 2) AS clientes_destino,
       (SELECT count(*) FROM simafi_stg.maestrosep m
         WHERE NOT EXISTS (SELECT 1 FROM cliente_maestro c
                           WHERE c.company_id = 2
                             AND c.maestro_cliente_clave = btrim(m.clave))) AS faltantes;
