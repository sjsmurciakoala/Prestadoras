DROP FUNCTION IF EXISTS fn_getclientesaldos_posteomanual(character varying);

CREATE OR REPLACE FUNCTION fn_getclientesaldos_posteomanual(
    p_codigo_cliente VARCHAR(50)
) RETURNS TABLE (
    recibo_actual INT,
    recibo_anterior INT,
    valor DECIMAL(18,2),
    distribucion_agua DECIMAL(18,2),
    distribucion_alcantarillado DECIMAL(18,2),
    distribucion_otros DECIMAL(18,2),
    detalle_id BIGINT
) AS $$
BEGIN
    RETURN QUERY
    WITH facturas_pendientes AS (
        SELECT 
            f.numrecibo,
            f.id AS factura_id,
            f.saldototal,
            LAG(f.numrecibo) OVER (ORDER BY f.numrecibo DESC) AS recibo_ant
        FROM factura f
        WHERE f.clientecodigo = p_codigo_cliente
          AND f.estado = 'A'
          AND f.saldototal > 0
        ORDER BY f.numrecibo DESC
    ),
    distribucion_servicios AS (
        SELECT 
            fd.factura_id,
            MIN(fd.id) AS detalle_id,
            SUM(CASE WHEN UPPER(fd.tiposervicio) = 'AGUA' THEN fd.montovalor_saldo ELSE 0 END) AS agua,
            SUM(CASE WHEN UPPER(fd.tiposervicio) = 'ALCANTARILLADO' THEN fd.montovalor_saldo ELSE 0 END) AS alcantarillado,
            SUM(CASE WHEN UPPER(fd.tiposervicio) NOT IN ('AGUA', 'ALCANTARILLADO') THEN fd.montovalor_saldo ELSE 0 END) AS otros
        FROM factura_detalle fd
        GROUP BY fd.factura_id
    )
    SELECT 
        fp.numrecibo::INT AS recibo_actual,
        COALESCE(fp.recibo_ant, 0)::INT AS recibo_anterior,
        fp.saldototal AS valor,
        COALESCE(ds.agua, 0) AS distribucion_agua,
        COALESCE(ds.alcantarillado, 0) AS distribucion_alcantarillado,
        COALESCE(ds.otros, 0) AS distribucion_otros,
        COALESCE(ds.detalle_id, 0)::BIGINT AS detalle_id
    FROM facturas_pendientes fp
    LEFT JOIN distribucion_servicios ds ON ds.factura_id = fp.factura_id
    ORDER BY fp.numrecibo DESC;
END;
$$ LANGUAGE plpgsql;
