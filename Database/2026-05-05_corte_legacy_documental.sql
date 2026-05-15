-- ============================================================================
-- Corte legacy formal - documentacion idempotente
-- Fecha: 2026-05-05
--
-- Este script documenta y reproduce el corte legacy ejecutado en PROD
-- (172.16.0.9 / siad_v3) entre el 28-abr-2026 y el 5-may-2026.
--
-- Las tablas listadas abajo fueron dropeadas manualmente por el usuario el
-- 28-abr-2026. Hoy ya no existen en PROD; este script las dropea de forma
-- idempotente (IF EXISTS) para reproducir el estado en otros ambientes.
--
-- Antes de correr este script en otro ambiente:
--   1. Asegurar que la BD tenga el modelo V3 desplegado (Database/ddl_v3/*).
--   2. Confirmar que ningun consumidor activo dependa de las tablas/SP/funciones
--      listadas (ver Prestadoras/docs/AUDITORIA_CORTE_LEGACY_2026-04-28.md).
--   3. Backup completo antes de aplicar.
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- 1) Tablas legacy
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS public.configuracion_tasas_detalle CASCADE;
DROP TABLE IF EXISTS public.configuracion_tasas       CASCADE;
DROP TABLE IF EXISTS public.tarifas_contador          CASCADE;
DROP TABLE IF EXISTS public.tarifas                   CASCADE;
DROP TABLE IF EXISTS public.servicios_roles_ws        CASCADE;
DROP TABLE IF EXISTS public.condicion_lectura         CASCADE;
DROP TABLE IF EXISTS public.condicon_lectura          CASCADE;  -- variante con typo
DROP TABLE IF EXISTS public.informativo               CASCADE;
DROP TABLE IF EXISTS public.configuracion_app_lectura_medidores CASCADE;
DROP TABLE IF EXISTS public.cai                       CASCADE;
DROP TABLE IF EXISTS public.letras                    CASCADE;
DROP TABLE IF EXISTS public.letracodigo               CASCADE;

-- ----------------------------------------------------------------------------
-- 2) Stored Procedures / functions legacy
-- ----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.sp_tarifas_ws();
DROP FUNCTION IF EXISTS public.sp_tarifas_contador_ws();
DROP FUNCTION IF EXISTS public.sp_cobros_adicionales_ws();
DROP FUNCTION IF EXISTS public.sp_cobros_adicionales_ws_v2();
DROP FUNCTION IF EXISTS public.sp_servicios_app_ws();
DROP FUNCTION IF EXISTS public.sp_condicion_lectura_ws();
DROP FUNCTION IF EXISTS public.sp_informativo_ws();
DROP FUNCTION IF EXISTS public.sp_configuracion_ws();
DROP FUNCTION IF EXISTS public.sp_cai_por_ruta(varchar);
DROP FUNCTION IF EXISTS public.sp_lectura(integer, integer, varchar, date, varchar, numeric, numeric, numeric, numeric, numeric, numeric, char, char, varchar, char, numeric, char, char, varchar, integer, integer, char, varchar, varchar, bytea, numeric, char);
DROP FUNCTION IF EXISTS public.sp_lectura_v2(integer, integer, varchar, date, varchar, numeric, numeric, char, char, varchar, char, numeric, char, char, varchar, integer, integer, char, varchar, varchar, bytea, numeric, char, jsonb);
DROP FUNCTION IF EXISTS public.sp_hdinfo_configuracion_tasas(varchar);
DROP FUNCTION IF EXISTS public.sp_configuracion_tasas_cliente(varchar);
DROP FUNCTION IF EXISTS public.sp_generar_configuracion_tasas_cliente(varchar, varchar);

-- ----------------------------------------------------------------------------
-- 3) fn_generar_codigo_cliente: removida del backend pero seguia en BD.
--    Drop final.
-- ----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.fn_generar_codigo_cliente();

COMMIT;

-- ----------------------------------------------------------------------------
-- Verificacion post-corte: las tablas y funciones listadas deben devolver 0.
-- ----------------------------------------------------------------------------
SELECT 'tablas_legacy_restantes' AS check, count(*) AS valor
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN (
      'tarifas', 'tarifas_contador',
      'configuracion_tasas', 'configuracion_tasas_detalle',
      'servicios_roles_ws',
      'condicion_lectura', 'condicon_lectura',
      'informativo', 'configuracion_app_lectura_medidores',
      'cai', 'letras', 'letracodigo'
  );

SELECT 'sp_legacy_restantes' AS check, count(*) AS valor
FROM pg_proc
WHERE proname IN (
    'sp_tarifas_ws', 'sp_tarifas_contador_ws',
    'sp_cobros_adicionales_ws', 'sp_cobros_adicionales_ws_v2',
    'sp_servicios_app_ws', 'sp_condicion_lectura_ws',
    'sp_informativo_ws', 'sp_configuracion_ws',
    'sp_cai_por_ruta',
    'sp_lectura', 'sp_lectura_v2',
    'sp_hdinfo_configuracion_tasas',
    'sp_configuracion_tasas_cliente',
    'sp_generar_configuracion_tasas_cliente',
    'fn_generar_codigo_cliente'
);
