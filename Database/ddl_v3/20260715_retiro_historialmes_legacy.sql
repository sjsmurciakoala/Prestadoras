-- =============================================================================
-- 2026-07-15  Retiro del legacy historialmes (Fase D apertura-ciclo-único)
-- Plan: docs/plans/2026-07-14-plan-apertura-ciclo-unico.md
-- Rama: feat/retiro-legacy-historialmes (apilada sobre la Fase E / PR #25)
-- -----------------------------------------------------------------------------
-- GATE D0 (verificado 2026-07-15): el WS WCF viejo (APCService.svc, app Java)
-- NO está publicado en el IIS de 172.16.0.9 — los sitios son portal:80,
-- apc.BancosWs:8087, apc.MobileApi.Lectores:44817 (API nueva) y
-- apc.MobileApi:8086 (backend de la app de órdenes de trabajo, ajeno a esto).
-- Nadie lee historialmes: el espejo de la transición F7 es peso muerto.
--
-- RESPALDO: backup completo previo en
--   Database/Backups/siad_v3_09_2026-07-15.backup (pg_dump formato custom).
-- Rollback: restaurar historialmes desde ese backup (pg_restore -t historialmes)
-- y recrear triggers/funciones espejo desde 20260704_ci_fase7_periodo_cierre.sql §3.
--
-- CONTENIDO:
--   1. DROP de los triggers espejo y sus funciones (F7 §3).
--   2. DROP de sp_informacion_ciclo (el GetCiclo del WS viejo; la API móvil
--      ya lee adm_periodo_comercial(_ciclo) directo — repunte en esta fase).
--   3. DROP TABLE historialmes.
--
-- FUERA DE ALCANCE (a propósito): usuarioapc y los SP legacy del flujo Java
-- (sp_lectura, sp_lectura_v2, sp_medidores_por_ruta_ws se conserva porque lo
-- usa la API móvil). usuarioapc se retira en la ventana de ops SOLO tras
-- confirmar que el backend de órdenes de trabajo (8086) no la usa para login.
--
-- Idempotente. NO ejecutar en producción fuera de la ventana de deploy.
-- =============================================================================

BEGIN;

-- 1. Espejo F7 → historialmes
DROP TRIGGER IF EXISTS trg_adm_periodo_comercial_ciclo_espejo ON public.adm_periodo_comercial_ciclo;
DROP TRIGGER IF EXISTS trg_adm_periodo_comercial_espejo ON public.adm_periodo_comercial;
DROP FUNCTION IF EXISTS public.fn_adm_periodo_ciclo_espejo_trigger();
DROP FUNCTION IF EXISTS public.fn_adm_periodo_comercial_espejo_trigger();
DROP FUNCTION IF EXISTS public.fn_adm_periodo_ciclo_espejo_sync(bigint);

-- 2. GetCiclo del WS viejo
DROP FUNCTION IF EXISTS public.sp_informacion_ciclo(character varying);

-- 3. La tabla legacy (PK ano+mes+ciclo, mono-empresa, estados en letras)
DROP TABLE IF EXISTS public.historialmes;

COMMIT;
