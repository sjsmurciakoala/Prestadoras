-- =============================================================================
-- Migración Presupuesto SIMAFI  ·  Script 03/4 : RECONCILIACIÓN (sólo lectura)
-- Correr después del 02. No modifica nada. Ejecutar en SRV y mirror y comparar.
-- Un presupuesto por año (PRE-<año>) = ingresos + egresos sumados.
-- =============================================================================

\echo '== A) Presupuestos cargados (hdr) + conteo de líneas =='
SELECT h.id_presupuesto,
       h.fecha_inicia, h.fecha_finaliza,
       h.valor_global,
       (SELECT count(*) FROM public.pst_config_presupuesto_dtl d
         WHERE d.id_presupuesto = h.id_presupuesto) AS lineas
  FROM public.pst_config_presupuesto_hdr h
 WHERE h.id_presupuesto LIKE 'PRE-%'
 ORDER BY h.id_presupuesto;

\echo '== A2) Total global de la migración (debe cuadrar con el cargado total) =='
SELECT count(*)                    AS presupuestos,
       round(sum(valor_global), 2) AS valor_global_total
  FROM public.pst_config_presupuesto_hdr
 WHERE id_presupuesto LIKE 'PRE-%';

\echo '== B) Cuadre por año: origen (cuentas válidas) = cargado + pendiente =='
WITH src AS (
    SELECT ano, regexp_replace(contable,'\D','','g') norm, COALESCE(aproba,0) monto
      FROM public.stg_simafi_baseing WHERE ano>0 AND contable IS NOT NULL
    UNION ALL
    SELECT ano, regexp_replace(contable,'\D','','g'), COALESCE(NULLIF(proyeccion,0),aproba,0)
      FROM public.stg_simafi_basepri WHERE ano>0 AND contable IS NOT NULL
),
clasif AS (
    SELECT s.ano, s.monto,
           (m.legacy_cuenta_norm IS NOT NULL) AS mapeada
      FROM src s
      LEFT JOIN public.stg_simafi_cuenta_map m
        ON m.company_id=2 AND m.legacy_cuenta_norm=s.norm AND COALESCE(m.siad_code,'')<>''
     WHERE length(s.norm) >= 3
)
SELECT ano,
       round(sum(monto),2)                                    AS origen_valido,
       round(sum(monto) FILTER (WHERE mapeada),2)             AS cargado,
       round(sum(monto) FILTER (WHERE NOT mapeada),2)         AS pendiente
  FROM clasif
 GROUP BY ano
 ORDER BY ano;

\echo '== C) Cobertura de cuentas (distintas) mapeadas vs pendientes =='
WITH src AS (
    SELECT DISTINCT regexp_replace(contable,'\D','','g') norm
      FROM (SELECT contable FROM public.stg_simafi_baseing WHERE ano>0
            UNION ALL
            SELECT contable FROM public.stg_simafi_basepri WHERE ano>0) x
     WHERE contable IS NOT NULL
)
SELECT count(*) FILTER (WHERE length(norm)>=3)                        AS cuentas_validas,
       count(*) FILTER (WHERE m.legacy_cuenta_norm IS NOT NULL)       AS mapeadas,
       count(*) FILTER (WHERE length(norm)>=3
                          AND m.legacy_cuenta_norm IS NULL)           AS pendientes
  FROM src s
  LEFT JOIN public.stg_simafi_cuenta_map m
    ON m.company_id=2 AND m.legacy_cuenta_norm=s.norm AND COALESCE(m.siad_code,'')<>'';

\echo '== D) Top 20 cuentas PENDIENTES por monto (para completar cuenta_map) =='
SELECT * FROM public.vw_stg_presupuesto_cuentas_pendientes
 ORDER BY monto_total DESC
 LIMIT 20;

\echo '== E) Total pendiente y # de cuentas pendientes =='
SELECT count(*) AS cuentas_pendientes, round(sum(monto_total),2) AS monto_pendiente
  FROM public.vw_stg_presupuesto_cuentas_pendientes;
