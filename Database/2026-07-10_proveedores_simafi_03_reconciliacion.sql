-- =============================================================================
-- Migración Proveedores SIMAFI · Script 03/3 : RECONCILIACIÓN (solo lectura)
-- Ejecutar tras el 02_transform, en SRV y mirror. No modifica nada.
-- =============================================================================
\echo '== 1. Catálogo: origen vs cargado (605 canónicos + 1 SINPROV) =='
SELECT (SELECT count(*) FROM stg_simafi_proveedor WHERE btrim(coalesce(codigo,''))<>'') AS origen,
       (SELECT count(*) FROM stg_simafi_proveedor s
         WHERE btrim(coalesce(codigo,''))<>''
           AND NOT EXISTS (SELECT 1 FROM stg_simafi_proveedor t
                           WHERE btrim(t.codigo)=btrim(s.codigo)
                             AND btrim(coalesce(t.proveedor,''))=btrim(coalesce(s.proveedor,''))
                             AND t.ctid < s.ctid)) AS origen_sin_duplicados,
       (SELECT count(*) FROM prv_proveedores
         WHERE company_id=2 AND usuario_creo='migracion' AND cod_proveedor<>'SINPROV') AS cargado_legacy,
       (SELECT count(*) FROM prv_proveedores
         WHERE company_id=2 AND cod_proveedor='SINPROV') AS generico;

\echo ''
\echo '== 2. Colisiones de código resueltas (sufijo) y duplicados deduplicados =='
SELECT cod_proveedor, nombre, rtn FROM prv_proveedores
WHERE company_id=2 AND (cod_proveedor LIKE '0322%' OR cod_proveedor LIKE '0519%')
ORDER BY cod_proveedor;

\echo ''
\echo '== 3. Cuenta contable: cargada vs vacía (D4) =='
SELECT CASE WHEN cuenta_contable='' THEN 'vacía (sin cuenta válida)' ELSE 'mapeada al plan' END AS estado,
       count(*)
FROM prv_proveedores WHERE company_id=2 AND usuario_creo='migracion' AND cod_proveedor<>'SINPROV'
GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 3b. Pendientes por motivo (vista) =='
SELECT motivo, count(*) FROM vw_stg_proveedores_pendientes GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 4. Tipo de proveedor: todos deben quedar en "Sin clasificar" =='
SELECT t.nombre AS tipo, count(*) AS proveedores
FROM prv_proveedores p JOIN prv_tipoproveedor t ON t.cod_tipoproveedor=p.cod_tipoproveedor
WHERE p.company_id=2 GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 5. Compromisos: órdenes y líneas, origen vs destino =='
SELECT (SELECT count(DISTINCT btrim(ordenp)) FROM stg_simafi_ordenesp
         WHERE btrim(coalesce(ordenp,'')) ~ '^[0-9]+$')                       AS ordenes_origen,
       (SELECT count(*) FROM prv_compromiso_hdr WHERE numero_orden >= 29169)  AS hdr_cargadas,
       (SELECT count(*) FROM stg_simafi_ordenesp
         WHERE btrim(coalesce(renglon,''))<>'' AND btrim(coalesce(ordenp,'')) ~ '^[0-9]+$') AS lineas_presup_origen,
       (SELECT count(*) FROM prv_compromiso_dtl WHERE numero_orden >= 29169)  AS dtl_cargadas;

\echo ''
\echo '== 6. CUADRE DE MONTO: SUM(valorp) origen  ==  SUM(monto) destino =='
SELECT (SELECT round(sum(coalesce(valorp,0)),2) FROM stg_simafi_ordenesp
         WHERE btrim(coalesce(ordenp,'')) ~ '^[0-9]+$')                      AS monto_origen,
       (SELECT round(sum(monto),2) FROM prv_compromiso_hdr WHERE numero_orden >= 29169) AS monto_hdr,
       (SELECT round(sum(monto),2) FROM prv_compromiso_dtl WHERE numero_orden >= 29169) AS monto_dtl;

\echo '   La brecha hdr-dtl debe ser EXACTAMENTE el valorp de las líneas sin renglón'
\echo '   (8 órdenes que traen importe pero no detalle presupuestario).'
SELECT count(*) AS lineas_sin_renglon_con_valorp,
       round(sum(coalesce(valorp,0)),2) AS brecha_esperada,
       (SELECT round(sum(monto),2) FROM prv_compromiso_hdr WHERE numero_orden >= 29169)
       - (SELECT round(sum(monto),2) FROM prv_compromiso_dtl WHERE numero_orden >= 29169) AS brecha_real
FROM stg_simafi_ordenesp
WHERE btrim(coalesce(renglon,''))='' AND coalesce(valorp,0)<>0
  AND btrim(coalesce(ordenp,'')) ~ '^[0-9]+$';

\echo ''
\echo '== 7. Enlace a proveedor: reales vs SINPROV, por categoría =='
SELECT CASE WHEN cod_proveedor='SINPROV' THEN 'SINPROV' ELSE 'proveedor real' END AS tipo,
       count(*) ordenes, round(sum(monto),2) monto
FROM prv_compromiso_hdr WHERE numero_orden >= 29169 GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 7b. Categorías anotadas en el concepto de las SINPROV =='
SELECT substring(concepto from '^\[([A-ZÑ ]+)\]') AS categoria, count(*)
FROM prv_compromiso_hdr WHERE numero_orden >= 29169 AND cod_proveedor='SINPROV'
GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 8. Órdenes con y sin contenido presupuestario (monto 0) =='
SELECT CASE WHEN monto = 0 THEN 'sin línea presupuestaria (monto 0)' ELSE 'con contenido' END AS clase,
       count(*) ordenes
FROM prv_compromiso_hdr WHERE numero_orden >= 29169 GROUP BY 1 ORDER BY 2 DESC;

\echo ''
\echo '== 9. status_transacc: todas históricas => TRUE (D9) =='
SELECT status_transacc, anulado, count(*) FROM prv_compromiso_hdr
WHERE numero_orden >= 29169 GROUP BY 1,2;

\echo ''
\echo '== 10. Integridad: detalle sin cabecera, y cabecera con proveedor inexistente =='
SELECT (SELECT count(*) FROM prv_compromiso_dtl d
         WHERE NOT EXISTS (SELECT 1 FROM prv_compromiso_hdr h WHERE h.numero_orden=d.numero_orden)) AS dtl_huerfano,
       (SELECT count(*) FROM prv_compromiso_hdr h
         WHERE h.numero_orden >= 29169
           AND NOT EXISTS (SELECT 1 FROM prv_proveedores p
                           WHERE p.cod_proveedor=h.cod_proveedor AND p.company_id=2)) AS hdr_sin_proveedor;

\echo ''
\echo '== 11. Backfill del correlativo (la UI continúa desde aquí) =='
\echo '   Solo aplica a los proveedores de la migración; 001001 y PRVGEN traen'
\echo '   su correlativo de las 4 órdenes de prueba y no se tocan.'
SELECT (SELECT count(*) FROM prv_proveedores p
          WHERE p.company_id=2 AND p.usuario_creo='migracion'
            AND p.ultimo_correlativo_compromiso <> COALESCE(
                (SELECT max(h.correlativo_proveedor) FROM prv_compromiso_hdr h
                  WHERE h.cod_proveedor=p.cod_proveedor AND h.numero_orden >= 29169), 0)) AS proveedores_descuadrados,
       (SELECT max(ultimo_correlativo_compromiso) FROM prv_proveedores
         WHERE company_id=2 AND usuario_creo='migracion') AS max_correlativo_migrado;

\echo ''
\echo '== 12. Datos del portal intactos (no los toca la migración) =='
SELECT cod_proveedor, nombre, usuario_creo FROM prv_proveedores
WHERE company_id=2 AND usuario_creo <> 'migracion' ORDER BY 1;
SELECT count(*) AS ordenes_de_prueba_intactas FROM prv_compromiso_hdr WHERE numero_orden < 29169;

\echo ''
\echo '== 13. Líneas de ordenesp descartadas (sin número de orden) =='
SELECT count(*) AS lineas_descartadas FROM vw_stg_compromisos_pendientes;
