-- ============================================================
-- DIAGNÓSTICO Y VERIFICACIÓN DE DATOS PARA POSTEO MANUAL
-- ============================================================

-- 1. VERIFICAR CANTIDAD DE FACTURAS
SELECT 'FACTURAS' as "Categoría", COUNT(*) as "Count"
FROM factura 
WHERE clientecodigo = 'CLI-DEMO-001' AND estado = 'A'
UNION ALL
-- 2. VERIFICAR CANTIDAD DE DETALLES
SELECT 'FACTURA_DETALLE', COUNT(*)
FROM factura_detalle fd
WHERE fd.factura_id IN (
    SELECT id FROM factura WHERE clientecodigo = 'CLI-DEMO-001' AND estado = 'A'
);

-- 3. VER QUÉ RETORNA LA FUNCIÓN
SELECT 
    recibo_actual,
    recibo_anterior,
    valor,
    distribucion_agua,
    distribucion_alcantarillado,
    distribucion_otros,
    detalle_id
FROM fn_getclientesaldos_posteomanual('CLI-DEMO-001');

-- 4. SI DETALLE_ID = 0, VERIFICAR PROBLEMA
SELECT 
    f.id AS factura_id,
    f.numfactura,
    f.numrecibo,
    f.saldototal,
    COUNT(fd.id) AS detalle_count,
    MIN(fd.id) AS min_detalle_id,
    MAX(fd.id) AS max_detalle_id
FROM factura f
LEFT JOIN factura_detalle fd ON fd.factura_id = f.id
WHERE f.clientecodigo = 'CLI-DEMO-001' AND f.estado = 'A'
GROUP BY f.id, f.numfactura, f.numrecibo, f.saldototal
ORDER BY f.numrecibo DESC;

-- 5. VER DETALLE_ID PROBLEMÁTICO
SELECT 'SALDOS CON DETALLE_ID = 0:' AS Análisis
UNION ALL
SELECT CONCAT(recibo_actual, ' - ', COALESCE(valor, 0))
FROM fn_getclientesaldos_posteomanual('CLI-DEMO-001')
WHERE detalle_id = 0;
