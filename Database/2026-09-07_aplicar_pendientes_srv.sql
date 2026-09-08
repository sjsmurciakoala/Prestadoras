-- =============================================================================
-- Tanda consolidada 2026-09-07 — aplicar en el SRV lo pendiente de
-- Inventario, Proveedores, Compras y Presupuesto
--
-- Base destino: siad_v4 @ 172.16.0.9  (siad_v3 está MUERTA: 0 conexiones)
-- Runbook:      Database/2026-09-07_runbook_despliegue_srv.md
--
-- QUÉ ES ESTO
-- Un GUION CONDUCTOR. No contiene SQL propio: incluye, en orden de dependencia, los
-- 16 scripts de las cinco tandas que siguen abiertas (2026-08-20 §3.28, 2026-08-22
-- §3.29, 2026-08-22 CxP, 2026-08-27, 2026-08-31 y 2026-09-01). Cada script incluido
-- trae su propio BEGIN … COMMIT, así que cada paso confirma o revierte por su cuenta;
-- si uno falla, ON_ERROR_STOP corta ahí y los pasos anteriores quedan aplicados.
--
-- CÓMO SE CORRE
--   1) Revisión (NO escribe nada — informa qué falta y qué ya está):
--        psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_aplicar_pendientes_srv.sql
--   2) Aplicación (solo después de leer el informe y de tener el respaldo hecho):
--        psql "$SRV" -v ON_ERROR_STOP=1 -v CONFIRMO=si -f Database/2026-09-07_aplicar_pendientes_srv.sql
--
-- Sin -v CONFIRMO=si el guion se detiene tras el informe. Es deliberado.
--
-- ANTES DE CORRERLO CON CONFIRMO=si
--   pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_tanda_2026_09_07.backup
--
-- GUARDAS QUE TRAE
--   * Aborta si la base conectada no se llama siad_v4.
--   * Aborta si falta cualquier prerrequisito estructural (el informe señala cuál).
--   * Aborta si no se pasa CONFIRMO=si.
--
-- LO QUE **NO** HACE
--   * No aplica 2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql: ya está
--     arriba desde el 2026-09-05.
--   * No aplica ningún script de rollback, ni 2026-08-20_alm_articulos_prueba.sql
--     (datos de prueba: NO va a producción).
--   * No enciende ningún interruptor. La aprobación por niveles y el control
--     presupuestario nacen APAGADOS por empresa; encenderlos es decisión posterior.
-- =============================================================================

\set ON_ERROR_STOP on
\timing on

\echo ''
\echo '================================================================='
\echo ' TANDA 2026-09-07 — pendientes de SRV (16 pasos, 5 tandas)'
\echo '================================================================='

-- -----------------------------------------------------------------------------
-- 0) Guarda de base: solo siad_v4
-- -----------------------------------------------------------------------------
SELECT current_database() AS base_actual,
       pg_size_pretty(pg_database_size(current_database())) AS tamano,
       CASE WHEN current_database() = 'siad_v4' THEN 'on' ELSE 'off' END AS base_ok
\gset

\echo '-- Base conectada:' :base_actual '-- Tamaño:' :tamano

\if :base_ok
\echo '-- Base confirmada: siad_v4'
\else
\echo '!!! ABORTADO: se esperaba siad_v4 y la conexión apunta a otra base.'
\q
\endif

-- -----------------------------------------------------------------------------
-- 1) Prerrequisitos estructurales (todos deben existir ARRIBA ya)
--    Un NULL en la columna "existe" significa que falta.
-- -----------------------------------------------------------------------------
\echo ''
\echo '--- Prerrequisitos ---'

SELECT 'pst_config_presupuesto_hdr' AS objeto, to_regclass('public.pst_config_presupuesto_hdr')::text AS existe
UNION ALL SELECT 'pst_config_presupuesto_dtl', to_regclass('public.pst_config_presupuesto_dtl')::text
UNION ALL SELECT 'pst_config_presupuesto_dtl.company_id',
       (SELECT column_name FROM information_schema.columns
         WHERE table_schema = 'public' AND table_name = 'pst_config_presupuesto_dtl'
           AND column_name = 'company_id')
UNION ALL SELECT 'alm_orden_compra',          to_regclass('public.alm_orden_compra')::text
UNION ALL SELECT 'alm_orden_compra_detalle',  to_regclass('public.alm_orden_compra_detalle')::text
UNION ALL SELECT 'alm_compra_hdr',            to_regclass('public.alm_compra_hdr')::text
UNION ALL SELECT 'alm_compra_cxp',            to_regclass('public.alm_compra_cxp')::text
UNION ALL SELECT 'alm_requisicion_hdr',       to_regclass('public.alm_requisicion_hdr')::text
UNION ALL SELECT 'con_centro_costo',          to_regclass('public.con_centro_costo')::text
UNION ALL SELECT 'con_plan_cuentas',          to_regclass('public.con_plan_cuentas')::text
UNION ALL SELECT 'con_empresa_configuracion', to_regclass('public.con_empresa_configuracion')::text
UNION ALL SELECT 'cfg_compra_isv',            to_regclass('public.cfg_compra_isv')::text
UNION ALL SELECT 'bitacora_maestro_catalogo', to_regclass('public.bitacora_maestro_catalogo')::text
UNION ALL SELECT 'fn_prv_estado_cuenta_documentos',
       (SELECT proname FROM pg_proc WHERE proname = 'fn_prv_estado_cuenta_documentos' LIMIT 1);

SELECT CASE WHEN to_regclass('public.pst_config_presupuesto_hdr') IS NOT NULL
             AND to_regclass('public.pst_config_presupuesto_dtl') IS NOT NULL
             AND to_regclass('public.alm_orden_compra')           IS NOT NULL
             AND to_regclass('public.alm_orden_compra_detalle')   IS NOT NULL
             AND to_regclass('public.alm_compra_hdr')             IS NOT NULL
             AND to_regclass('public.alm_compra_cxp')             IS NOT NULL
             AND to_regclass('public.alm_requisicion_hdr')        IS NOT NULL
             AND to_regclass('public.con_centro_costo')           IS NOT NULL
             AND to_regclass('public.con_plan_cuentas')           IS NOT NULL
             AND to_regclass('public.con_empresa_configuracion')  IS NOT NULL
             AND to_regclass('public.cfg_compra_isv')             IS NOT NULL
             AND to_regclass('public.bitacora_maestro_catalogo')  IS NOT NULL
             AND EXISTS (SELECT 1 FROM pg_proc
                          WHERE proname = 'fn_prv_estado_cuenta_documentos')
             AND EXISTS (SELECT 1 FROM information_schema.columns
                          WHERE table_schema = 'public'
                            AND table_name   = 'pst_config_presupuesto_dtl'
                            AND column_name  = 'company_id')
            THEN 'on' ELSE 'off' END AS req_ok
\gset

\if :req_ok
\echo '-- Prerrequisitos completos.'
\else
\echo '!!! ABORTADO: falta al menos un prerrequisito (el cuadro de arriba lo marca con vacio).'
\echo '!!! Si falta pst_config_presupuesto_dtl.company_id, aplicar primero'
\echo '!!!   2026-07-24_presupuesto_multitenant_company_id.sql y'
\echo '!!!   2026-07-28_presupuesto_completar_ddl_valor_real.sql'
\q
\endif

-- -----------------------------------------------------------------------------
-- 2) Informe «¿ya aplicado?» — una senal por paso, solo lectura
-- -----------------------------------------------------------------------------
\echo ''
\echo '--- Estado de cada paso ---'

SELECT paso, tanda, senal, estado
  FROM (VALUES
   ( 1, '2026-08-20', 'cfg_inventario_negativo eliminada',
        CASE WHEN to_regclass('public.cfg_inventario_negativo') IS NULL
             THEN 'YA' ELSE 'FALTA' END),
   ( 1, '2026-08-20', 'alm_bodega.permite_existencia_negativa eliminada',
        CASE WHEN NOT EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_schema = 'public' AND table_name = 'alm_bodega'
                                 AND column_name = 'permite_existencia_negativa')
             THEN 'YA' ELSE 'FALTA' END),
   ( 2, '2026-09-05', 'con_empresa_configuracion.ciudad con dato',
        CASE WHEN EXISTS (SELECT 1 FROM public.con_empresa_configuracion
                           WHERE coalesce(btrim(ciudad), '') <> '')
             THEN 'YA' ELSE 'FALTA' END),
   ( 3, '2026-08-22', 'cfg_formato_fiscal (tabla)',
        CASE WHEN to_regclass('public.cfg_formato_fiscal') IS NOT NULL
             THEN 'YA' ELSE 'FALTA' END),
   ( 4, '2026-08-22', 'cfg_formato_fiscal con semilla NUMERO_SAR y CAI',
        CASE WHEN to_regclass('public.cfg_formato_fiscal') IS NULL THEN 'FALTA'
             WHEN (SELECT count(*) FROM public.cfg_formato_fiscal
                    WHERE codigo IN ('NUMERO_SAR', 'CAI')) >= 2 THEN 'YA'
             ELSE 'FALTA' END),
   ( 5, '2026-08-22', 'bitacora_maestro_catalogo con cfg_formato_fiscal',
        CASE WHEN EXISTS (SELECT 1 FROM public.bitacora_maestro_catalogo
                           WHERE tabla = 'cfg_formato_fiscal')
             THEN 'YA' ELSE 'FALTA' END),
   ( 6, '2026-08-22', 'fn_prv_cxp_documentos y fn_prv_cxp_resumen',
        CASE WHEN (SELECT count(DISTINCT proname) FROM pg_proc
                    WHERE proname IN ('fn_prv_cxp_documentos', 'fn_prv_cxp_resumen')) = 2
             THEN 'YA' ELSE 'FALTA' END),
   ( 7, '2026-08-27', 'pst_compromiso, pst_movimiento y cfg_presupuesto_control',
        CASE WHEN to_regclass('public.pst_compromiso')          IS NOT NULL
              AND to_regclass('public.pst_movimiento')          IS NOT NULL
              AND to_regclass('public.cfg_presupuesto_control') IS NOT NULL
             THEN 'YA' ELSE 'FALTA' END),
   ( 8, '2026-08-27', 'fn_pst_disponible (funciones)',
        CASE WHEN EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'fn_pst_disponible')
             THEN 'YA' ELSE 'FALTA' END),
   ( 9, '2026-08-27', 'sp_pst_comprometer_documento (procedimientos)',
        CASE WHEN EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'sp_pst_comprometer_documento')
             THEN 'YA' ELSE 'FALTA' END),
   (10, '2026-08-27', 'vw_pst_ejecucion_presupuestaria (vistas)',
        CASE WHEN to_regclass('public.vw_pst_ejecucion_presupuestaria') IS NOT NULL
             THEN 'YA' ELSE 'FALTA' END),
   (11, '2026-08-27', 'sp_pst_afectar_valor_real (proveedores y bancos, paso 05)',
        CASE WHEN EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'sp_pst_afectar_valor_real')
             THEN 'YA' ELSE 'FALTA' END),
   (12, '2026-09-01', 'disponible sin truncar (vista sin GREATEST)',
        CASE WHEN to_regclass('public.vw_pst_ejecucion_presupuestaria') IS NULL THEN 'FALTA'
             WHEN pg_get_viewdef('public.vw_pst_ejecucion_presupuestaria') ILIKE '%greatest%'
             THEN 'FALTA' ELSE 'YA' END),
   (13, '2026-08-31', 'cfg_aprobacion_* y estado 7 en la orden de compra',
        CASE WHEN to_regclass('public.cfg_aprobacion_control') IS NOT NULL
              AND to_regclass('public.cfg_aprobacion_nivel')   IS NOT NULL
              AND coalesce((SELECT pg_get_constraintdef(oid) FROM pg_constraint
                             WHERE conname = 'ck_alm_orden_compra_estado'), '') LIKE '%7%'
             THEN 'YA' ELSE 'FALTA' END),
   (14, '2026-08-31', 'funciones fn_apr_*',
        CASE WHEN EXISTS (SELECT 1 FROM pg_proc WHERE proname LIKE 'fn_apr\_%')
             THEN 'YA' ELSE 'FALTA' END),
   (15, '2026-08-31', 'alm_requisicion_aprobacion',
        CASE WHEN to_regclass('public.alm_requisicion_aprobacion') IS NOT NULL
             THEN 'YA' ELSE 'FALTA' END),
   (16, '2026-09-01', 'cfg_aprobacion_nivel.monto_hasta (limite, ya no cascada)',
        CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'cfg_aprobacion_nivel'
                             AND column_name = 'monto_hasta')
             THEN 'YA' ELSE 'FALTA' END)
  ) AS t(paso, tanda, senal, estado)
 ORDER BY paso, senal;

-- -----------------------------------------------------------------------------
-- 3) Confirmación explícita
-- -----------------------------------------------------------------------------
\if :{?CONFIRMO}
\echo ''
\echo '>>> CONFIRMO recibido. Aplicando los 16 pasos.'
\else
\echo ''
\echo '================================================================='
\echo ' MODO REVISION — no se escribio NADA en la base.'
\echo ' Para aplicar: repetir el comando agregando  -v CONFIRMO=si'
\echo ' (antes: pg_dump de siad_v4)'
\echo '================================================================='
\q
\endif

-- =============================================================================
-- BLOQUE 1 — Independientes: no dependen de nada nuevo
-- =============================================================================
\echo ''
\echo '### Paso 1/16 — 2026-08-20_alm_quitar_existencia_negativa.sql  (DESTRUCTIVO)'
\ir 2026-08-20_alm_quitar_existencia_negativa.sql

\echo ''
\echo '### Paso 2/16 — 2026-09-05_ciudad_empresa_configuracion.sql'
\ir 2026-09-05_ciudad_empresa_configuracion.sql

-- =============================================================================
-- BLOQUE 2 — Formatos fiscales (No. factura SAR y CAI)
--            Orden duro: tabla, luego semilla, luego bitácora.
-- =============================================================================
\echo ''
\echo '### Paso 3/16 — 2026-08-22_cfg_formato_fiscal.sql'
\ir 2026-08-22_cfg_formato_fiscal.sql

-- El paso 4 es la ÚNICA pieza opcional de la tanda: siembra los dos formatos de la
-- empresa 2. Si arriba se prefiere capturarlos desde /mantenimientos/formatos-fiscales,
-- correr el guion con  -v SIN_SEMILLA=si  y este paso se salta.
\if :{?SIN_SEMILLA}
\echo ''
\echo '### Paso 4/16 — OMITIDO por -v SIN_SEMILLA=si (los formatos se capturan en el portal)'
\else
\echo ''
\echo '### Paso 4/16 — 2026-08-22_cfg_formato_fiscal_seed.sql  (asume company_id = 2)'
\ir 2026-08-22_cfg_formato_fiscal_seed.sql
\endif

\echo ''
\echo '### Paso 5/16 — 2026-08-22_bitacora_config_formato_fiscal.sql'
\ir 2026-08-22_bitacora_config_formato_fiscal.sql

-- =============================================================================
-- BLOQUE 3 — Cuentas por pagar unificadas (dos funciones de lectura)
-- =============================================================================
\echo ''
\echo '### Paso 6/16 — 2026-08-22_prv_cxp_unificada.sql'
\ir 2026-08-22_prv_cxp_unificada.sql

-- =============================================================================
-- BLOQUE 4 — Control presupuestario. Orden duro 01, 02, 03, 04, 05 y la corrección.
-- =============================================================================
\echo ''
\echo '### Paso 7/16 — 2026-08-27_pst_compromiso_01_estructura.sql'
\ir 2026-08-27_pst_compromiso_01_estructura.sql

\echo ''
\echo '### Paso 8/16 — 2026-08-27_pst_compromiso_02_funciones.sql'
\ir 2026-08-27_pst_compromiso_02_funciones.sql

\echo ''
\echo '### Paso 9/16 — 2026-08-27_pst_compromiso_03_procedimientos.sql'
\ir 2026-08-27_pst_compromiso_03_procedimientos.sql

\echo ''
\echo '### Paso 10/16 — 2026-08-27_pst_compromiso_04_vistas.sql'
\ir 2026-08-27_pst_compromiso_04_vistas.sql

\echo ''
\echo '### Paso 11/16 — 2026-08-27_pst_compromiso_05_proveedores_bancos.sql  (reemplaza una funcion viva)'
\ir 2026-08-27_pst_compromiso_05_proveedores_bancos.sql

\echo ''
\echo '### Paso 12/16 — 2026-09-01_pst_disponible_sin_truncar.sql'
\ir 2026-09-01_pst_disponible_sin_truncar.sql

-- =============================================================================
-- BLOQUE 5 — Aprobación por niveles. VA DESPUÉS del bloque 4: los dos reemplazan
--            ck_alm_orden_compra_estado y este lo tiene que dejar con el estado 7.
-- =============================================================================
\echo ''
\echo '### Paso 13/16 — 2026-08-31_apr_niveles_01_estructura.sql'
\ir 2026-08-31_apr_niveles_01_estructura.sql

\echo ''
\echo '### Paso 14/16 — 2026-08-31_apr_niveles_02_funciones.sql'
\ir 2026-08-31_apr_niveles_02_funciones.sql

\echo ''
\echo '### Paso 15/16 — 2026-08-31_apr_niveles_03_requisicion.sql'
\ir 2026-08-31_apr_niveles_03_requisicion.sql

\echo ''
\echo '### Paso 16/16 — 2026-09-01_apr_niveles_04_limite_por_aprobador.sql  (RENAME de columna)'
\ir 2026-09-01_apr_niveles_04_limite_por_aprobador.sql

-- =============================================================================
-- 4) Verificación posterior (solo lectura)
-- =============================================================================
\echo ''
\echo '--- Verificacion posterior ---'

SELECT 'interruptores nuevos' AS control,
       (SELECT count(*) FROM public.cfg_presupuesto_control)                 AS filas_pst,
       (SELECT count(*) FROM public.cfg_presupuesto_control WHERE modo <> 0) AS pst_encendidas,
       (SELECT count(*) FROM public.cfg_aprobacion_control)                  AS filas_apr,
       (SELECT count(*) FROM public.cfg_aprobacion_control WHERE modo <> 0)  AS apr_encendidas;

SELECT 'ck_alm_orden_compra_estado' AS restriccion,
       pg_get_constraintdef(oid)    AS definicion
  FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';

SELECT 'formatos fiscales' AS catalogo, company_id, codigo, mascara, activo
  FROM public.cfg_formato_fiscal ORDER BY company_id, codigo;

SELECT 'existencia negativa: piezas que deben haber desaparecido' AS revision,
       to_regclass('public.cfg_inventario_negativo')::text        AS tabla_interruptor,
       (SELECT count(*) FROM information_schema.columns
         WHERE table_schema = 'public' AND table_name = 'alm_bodega'
           AND column_name = 'permite_existencia_negativa')       AS columna_override;

\echo ''
\echo '================================================================='
\echo ' TANDA 2026-09-07 APLICADA. Falta desplegar el binario del portal'
\echo ' y encender por empresa lo que corresponda: todo nace apagado.'
\echo ' Ver la seccion 7 del runbook.'
\echo '================================================================='
