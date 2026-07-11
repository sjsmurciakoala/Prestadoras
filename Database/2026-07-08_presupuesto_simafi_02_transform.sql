-- =============================================================================
-- Migración Presupuesto SIMAFI  ·  Script 02/4 : TRANSFORM + UPSERT idempotente
-- staging (stg_simafi_baseing/basepri) -> pst_config_presupuesto_hdr/_dtl
--
-- Reglas (ver memoria project-migracion-presupuesto-simafi):
--   · UN solo presupuesto por año: id = 'PRE-<año>'. Se SUMAN ingresos (baseing)
--     y egresos (basepri) del mismo año en un único presupuesto (decisión del
--     usuario 2026-07-08: un presupuesto por año, no ING-/EGR- por separado).
--   · monto: ingresos = aproba ; egresos = COALESCE(NULLIF(proyeccion,0), aproba)
--   · cuenta: contable legacy normalizado -> stg_simafi_cuenta_map.siad_code
--     SÓLO se cargan las cuentas mapeadas; las no mapeadas van al reporte de
--     pendientes (vw_stg_presupuesto_cuentas_pendientes).
--   · consolida (SUM) por (año, con_cuenta_code) -> respeta el PK del dtl y
--     suma ingreso+egreso si una misma cuenta apareciera en ambos orígenes.
--   · reemplazo completo idempotente: borra los ids de la migración
--     (PRE-/ING-/EGR-) y reinserta; NO usa TRUNCATE CASCADE.
--   · valor_real = 0 (el legacy no tiene ejecución) ; valor_disponible = proyección
-- Aplicar en SRV y mirror tras el 00 (DDL) y el 01 (landing).
-- =============================================================================

BEGIN;

-- 1) Consolidado por (año, cuenta) sumando ingresos + egresos --------------------
DROP TABLE IF EXISTS _cons;
CREATE TEMP TABLE _cons ON COMMIT DROP AS
WITH src AS (
    SELECT ano,
           regexp_replace(contable, '\D', '', 'g') AS norm,
           COALESCE(aproba, 0)                      AS monto
      FROM public.stg_simafi_baseing
     WHERE ano > 0 AND contable IS NOT NULL
    UNION ALL
    SELECT ano,
           regexp_replace(contable, '\D', '', 'g') AS norm,
           COALESCE(NULLIF(proyeccion, 0), aproba, 0) AS monto
      FROM public.stg_simafi_basepri
     WHERE ano > 0 AND contable IS NOT NULL
),
resolved AS (
    SELECT s.ano, m.siad_code AS con_cuenta_code, s.monto
      FROM src s
      JOIN public.stg_simafi_cuenta_map m
        ON m.company_id = 2
       AND m.legacy_cuenta_norm = s.norm
       AND COALESCE(m.siad_code, '') <> ''
     WHERE length(s.norm) >= 3
)
SELECT ano,
       ('PRE-' || ano)::varchar(10) AS id_presupuesto,
       con_cuenta_code,
       SUM(monto) AS monto
  FROM resolved
 GROUP BY ano, con_cuenta_code;

-- 2) Reemplazo idempotente: borra los presupuestos de la migración --------------
--    (PRE- que vamos a recargar + los viejos ING-/EGR- que este script sustituye).
--    La FK dtl->hdr es ON DELETE CASCADE, así que borra su detalle también.
DELETE FROM public.pst_config_presupuesto_hdr
 WHERE id_presupuesto LIKE 'PRE-%'
    OR id_presupuesto LIKE 'ING-%'
    OR id_presupuesto LIKE 'EGR-%';

-- 3) Encabezado: uno por año ----------------------------------------------------
INSERT INTO public.pst_config_presupuesto_hdr
       (id_presupuesto, valor_global, valor_disponible,
        rango_periodo, fecha_inicia, fecha_finaliza, estado_aprobado)
SELECT id_presupuesto,
       SUM(monto)                 AS valor_global,
       SUM(monto)                 AS valor_disponible,   -- real = 0
       12                         AS rango_periodo,
       make_date(ano, 1, 1)       AS fecha_inicia,
       make_date(ano, 12, 31)     AS fecha_finaliza,
       true                       AS estado_aprobado     -- presupuestos históricos aprobados
  FROM _cons
 GROUP BY id_presupuesto, ano;

-- 4) Detalle: id_presupuesto_dtl (surrogate sin default) = max global + fila ----
INSERT INTO public.pst_config_presupuesto_dtl
       (id_presupuesto, con_cuenta_code,
        valor_proyeccion, valor_real, valor_disponible, id_presupuesto_dtl)
SELECT c.id_presupuesto,
       c.con_cuenta_code,
       c.monto           AS valor_proyeccion,
       0                 AS valor_real,
       c.monto           AS valor_disponible,
       base.mx + row_number() OVER (ORDER BY c.id_presupuesto, c.con_cuenta_code)
  FROM _cons c
 CROSS JOIN (SELECT COALESCE(max(id_presupuesto_dtl), 0) AS mx
               FROM public.pst_config_presupuesto_dtl) base;

COMMIT;

-- 5) Reporte de cuentas legacy SIN mapa (pendientes de completar en cuenta_map) --
--    Se mantiene el desglose por tipo (ING/EGR) sólo con fines de diagnóstico.
CREATE OR REPLACE VIEW public.vw_stg_presupuesto_cuentas_pendientes AS
WITH src AS (
    SELECT 'ING'::text AS tipo, ano, contable,
           regexp_replace(contable, '\D', '', 'g') AS norm,
           COALESCE(aproba, 0) AS monto
      FROM public.stg_simafi_baseing WHERE ano > 0 AND contable IS NOT NULL
    UNION ALL
    SELECT 'EGR'::text AS tipo, ano, contable,
           regexp_replace(contable, '\D', '', 'g') AS norm,
           COALESCE(NULLIF(proyeccion, 0), aproba, 0) AS monto
      FROM public.stg_simafi_basepri WHERE ano > 0 AND contable IS NOT NULL
)
SELECT s.tipo,
       s.norm                       AS legacy_cuenta_norm,
       min(s.contable)              AS ejemplo_contable,
       count(*)                     AS lineas,
       count(DISTINCT s.ano)        AS anios,
       round(sum(s.monto), 2)       AS monto_total
  FROM src s
  LEFT JOIN public.stg_simafi_cuenta_map m
         ON m.company_id = 2
        AND m.legacy_cuenta_norm = s.norm
        AND COALESCE(m.siad_code, '') <> ''
 WHERE m.legacy_cuenta_norm IS NULL     -- sin mapa
   AND length(s.norm) >= 3              -- descarta basura (- -, vacíos)
 GROUP BY s.tipo, s.norm;
