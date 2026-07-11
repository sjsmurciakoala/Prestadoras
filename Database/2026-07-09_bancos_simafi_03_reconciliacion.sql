-- =============================================================================
-- Migración Bancos SIMAFI  ·  Script 03/4 : RECONCILIACIÓN (solo lectura)
-- Cuadres origen (stg_*) vs destino (ban_*). No modifica nada.
-- =============================================================================
\echo '== 1. Bancos: existentes reutilizados vs nuevos SIM =='
SELECT CASE WHEN code LIKE 'SIMB%' THEN 'nuevo_SIM' ELSE 'existente' END AS tipo,
       count(*) AS bancos
FROM public.ban_banco WHERE company_id = 2 GROUP BY 1 ORDER BY 1;

\echo '== 2. Cuentas: existentes reutilizadas vs nuevas SIM (por fuente) =='
SELECT CASE WHEN code LIKE 'SIMC%' THEN 'nueva_ctacheques'
            WHEN code LIKE 'SIMS%' THEN 'nueva_saldobancos'
            ELSE 'existente' END AS tipo,
       count(*) AS cuentas, round(sum(saldo_actual),2) AS saldo_actual
FROM public.ban_cuenta WHERE company_id = 2 GROUP BY 1 ORDER BY 1;

\echo '== 3. Cross-walk contable: cuentas SIM con/sin cuenta mayor =='
SELECT (cont_account_id IS NOT NULL) AS mapeada, count(*)
FROM public.ban_cuenta WHERE company_id=2 AND code LIKE 'SIMC%' GROUP BY 1 ORDER BY 1;

\echo '== 4. Movimientos por origen (destino) =='
SELECT origen_legacy, count(*) AS movimientos,
       round(sum(mto_db),2) AS debe, round(sum(mto_cr),2) AS haber
FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy IS NOT NULL
GROUP BY origen_legacy ORDER BY origen_legacy;

\echo '== 5. maestroche: origen vs cargado (origen = cargado + sin_match) =='
SELECT (SELECT count(*) FROM public.stg_simafi_maestroche) AS origen,
       (SELECT count(*) FROM public.stg_simafi_maestroche mc
         WHERE NOT EXISTS (SELECT 1 FROM public.stg_simafi_ctacheques c
                            WHERE btrim(c.numero)=btrim(mc.cuenta))) AS sin_match,
       (SELECT count(*) FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy='maestroche') AS cargado;

\echo '== 6. maestroche: cuadre de montos origen vs destino =='
SELECT round((SELECT sum(debe) FROM public.stg_simafi_maestroche),2) AS debe_origen,
       round((SELECT sum(mto_db) FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy='maestroche'),2) AS debe_dest,
       round((SELECT sum(haber) FROM public.stg_simafi_maestroche),2) AS haber_origen,
       round((SELECT sum(mto_cr) FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy='maestroche'),2) AS haber_dest;

\echo '== 7. detalleck: cabeceras (una por voucher×cuenta) y detalle =='
-- headers_dest >= vouchers_con_banco por vouchers que tocan 2+ cuentas
SELECT (SELECT count(*) FROM (SELECT 1 FROM public.stg_simafi_detalleck
          WHERE fecha IS NOT NULL GROUP BY empre, btrim(docu), fecha
          HAVING sum(CASE WHEN btrim(cuenta) LIKE '111-02%' THEN 1 ELSE 0 END) > 0) t) AS vouchers_con_banco,
       (SELECT count(*) FROM (SELECT 1 FROM public.stg_simafi_detalleck
          WHERE btrim(cuenta) LIKE '111-02%' AND fecha IS NOT NULL
          GROUP BY empre, btrim(docu), fecha, btrim(cuenta)) t) AS headers_esperados,
       (SELECT count(*) FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy='detalleck') AS headers_dest,
       (SELECT count(*) FROM public.ban_movimiento_detalle d JOIN public.ban_movimiento m ON m.movimiento_id=d.movimiento_id
         WHERE m.company_id=2 AND m.origen_legacy='detalleck') AS lineas_dest;

\echo '== 7b. detalleck: cuadre de monto banco (origen líneas 111-02 vs cabeceras) =='
SELECT round((SELECT sum(COALESCE(debe,0)+COALESCE(haber,0)) FROM public.stg_simafi_detalleck
               WHERE btrim(cuenta) LIKE '111-02%' AND fecha IS NOT NULL),2) AS monto_banco_origen,
       round((SELECT sum(monto) FROM public.ban_movimiento WHERE company_id=2 AND origen_legacy='detalleck'),2) AS monto_headers_dest;

\echo '== 8. detalleck: líneas NO cargadas (vouchers sin línea banco) =='
SELECT count(*) AS lineas_no_cargadas FROM public.stg_simafi_detalleck d
WHERE NOT EXISTS (
    SELECT 1 FROM public.stg_simafi_detalleck d2
    WHERE d2.empre=d.empre AND btrim(d2.docu)=btrim(d.docu) AND d2.fecha=d.fecha
      AND btrim(d2.cuenta) LIKE '111-02%');

\echo '== 9. Pendientes (resumen de la vista) =='
SELECT motivo, count(*) AS grupos, sum(lineas) AS filas
FROM public.vw_stg_bancos_pendientes GROUP BY motivo ORDER BY motivo;

\echo '== 10. Integridad: movimientos con cuenta inexistente (debe ser 0) =='
SELECT count(*) AS movimientos_huerfanos
FROM public.ban_movimiento m
LEFT JOIN public.ban_cuenta c ON c.banco_cuenta_id = m.banco_cuenta_id
WHERE m.company_id=2 AND m.origen_legacy IS NOT NULL AND c.banco_cuenta_id IS NULL;
