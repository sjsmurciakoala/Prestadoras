-- ================================================
-- Stored Procedures para Captación de Pagos
-- Migración desde sistema legado ASP.NET Core 3
-- Fecha: 16 de enero de 2026
-- ================================================

-- ==================== POSTEO LECTORAS ====================

-- 1. Actualizar detalle de posteo lectora (créditos/débitos en factura_detalle)
CREATE OR REPLACE FUNCTION sp_actualizar_detalle_posteolectora(
    p_factura_id BIGINT,
    p_linea INT,
    p_monto_acreditado DECIMAL(18,2),
    p_monto_debitado DECIMAL(18,2)
) RETURNS VOID AS $$
BEGIN
    -- Actualiza saldos en factura_detalle
    -- Acreditar reduce saldo, debitar aumenta saldo (reverso)
    UPDATE factura_detalle
       SET montovalor_saldo = montovalor_saldo - p_monto_acreditado + p_monto_debitado
     WHERE factura_id = p_factura_id
       AND linea = p_linea;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró el detalle de factura % línea %', p_factura_id, p_linea;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- 2. Actualizar estado de factura y registrar pago
CREATE OR REPLACE FUNCTION sp_actualizar_factura_pago(
    p_numfactura VARCHAR(50),
    p_cliente_clave VARCHAR(50),
    p_estado CHAR(1),
    p_banco VARCHAR(100),
    p_usuario VARCHAR(100)
) RETURNS VOID AS $$
BEGIN
    -- Actualiza estado de factura
    -- Estado 'C' = Cobrado/Cerrado
    -- Estado 'A' = Abierto/Activo (reverso)
    UPDATE factura
       SET estado = p_estado,
           recolectora = CASE WHEN p_estado = 'C' THEN p_banco ELSE NULL END,
           fechapago = CASE WHEN p_estado = 'C' THEN NOW() ELSE NULL END,
           usuario = p_usuario
     WHERE numfactura = p_numfactura
       AND clientecodigo = p_cliente_clave;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró la factura % para el cliente %', p_numfactura, p_cliente_clave;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- 3. Obtener saldo actual del cliente
CREATE OR REPLACE FUNCTION sp_obtener_cliente_saldo(
    p_cliente_clave VARCHAR(50)
) RETURNS TABLE (
    cliente_clave VARCHAR(50),
    saldo_actual DECIMAL(18,2),
    saldo_anterior DECIMAL(18,2),
    ultimo_recibo INT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        cd.clientecodigo AS cliente_clave,
        COALESCE(SUM(cd.saldo), 0) AS saldo_actual,
        COALESCE(MAX(ta.saldo), 0) AS saldo_anterior,
        MAX(f.numrecibo) AS ultimo_recibo
    FROM cliente_detalle cd
    LEFT JOIN transaccion_abonado ta ON ta.cliente_clave = cd.clientecodigo
    LEFT JOIN factura f ON f.clientecodigo = cd.clientecodigo
    WHERE cd.clientecodigo = p_cliente_clave
    GROUP BY cd.clientecodigo;
END;
$$ LANGUAGE plpgsql;

-- 4. Búsqueda de facturas/recibos por coincidencia (autocompletado)
CREATE OR REPLACE FUNCTION get_matching_invoices(
    p_term VARCHAR(100)
) RETURNS TABLE (
    numfactura VARCHAR(50),
    cliente_clave VARCHAR(50),
    cliente_nombre VARCHAR(200),
    fecha DATE,
    total DECIMAL(18,2),
    estado VARCHAR(20)
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        f.numfactura,
        f.clientecodigo AS cliente_clave,
        COALESCE(cm.nombre, cd.nombre, 'Cliente sin nombre') AS cliente_nombre,
        f.fechaemision AS fecha,
        f.saldototal AS total,
        f.estado
    FROM factura f
    LEFT JOIN cliente_maestro cm ON cm.clave = f.clientecodigo
    LEFT JOIN cliente_detalle cd ON cd.clientecodigo = f.clientecodigo
    WHERE (f.numfactura ILIKE '%' || p_term || '%'
       OR f.numrecibo::TEXT ILIKE '%' || p_term || '%'
       OR f.clientecodigo ILIKE '%' || p_term || '%')
      AND f.estado IN ('A', 'C')
    ORDER BY f.fechaemision DESC, f.numrecibo DESC
    LIMIT 20;
END;
$$ LANGUAGE plpgsql;

-- ==================== POSTEO MANUAL ====================

-- 5. Obtener saldos pendientes para posteo manual (función crítica del legado)
CREATE OR REPLACE FUNCTION fn_getclientesaldos_posteomanual(
    p_codigo_cliente VARCHAR(50)
) RETURNS TABLE (
    recibo_actual INT,
    recibo_anterior INT,
    valor DECIMAL(18,2),
    distribucion_agua DECIMAL(18,2),
    distribucion_alcantarillado DECIMAL(18,2),
    distribucion_otros DECIMAL(18,2),
    detalle_id BIGINT,
    detalle_id_agua BIGINT,
    detalle_id_alcantarillado BIGINT,
    detalle_id_otros BIGINT
) AS $$
BEGIN
    -- Retorna saldos pendientes de facturas del cliente
    -- con distribución por tipo de servicio
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
            MIN(CASE WHEN UPPER(fd.tiposervicio) = 'AGUA' THEN fd.id END) AS detalle_id_agua,
            MIN(CASE WHEN UPPER(fd.tiposervicio) = 'ALCANTARILLADO' THEN fd.id END) AS detalle_id_alcantarillado,
            MIN(CASE WHEN UPPER(fd.tiposervicio) NOT IN ('AGUA', 'ALCANTARILLADO') THEN fd.id END) AS detalle_id_otros,
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
        COALESCE(ds.detalle_id, 0) AS detalle_id,
        COALESCE(ds.detalle_id_agua, 0) AS detalle_id_agua,
        COALESCE(ds.detalle_id_alcantarillado, 0) AS detalle_id_alcantarillado,
        COALESCE(ds.detalle_id_otros, 0) AS detalle_id_otros
    FROM facturas_pendientes fp
    LEFT JOIN distribucion_servicios ds ON ds.factura_id = fp.factura_id
    ORDER BY fp.numrecibo DESC;
END;
$$ LANGUAGE plpgsql;

-- 6. Actualizar detalle de posteo manual (acreditar/debitar montos distribuidos)
CREATE OR REPLACE FUNCTION sp_actualizar_detalle_posteomanual(
    p_detalle_id BIGINT,
    p_monto_acreditado DECIMAL(18,2),
    p_monto_debitado DECIMAL(18,2)
) RETURNS VOID AS $$
BEGIN
    -- Actualiza el saldo en factura_detalle
    -- Lógica: acreditar reduce saldo, debitar aumenta saldo
    UPDATE factura_detalle
       SET montovalor_saldo = montovalor_saldo - p_monto_acreditado + p_monto_debitado
     WHERE id = p_detalle_id;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró el detalle % para actualizar', p_detalle_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- 7. Registrar posteo manual (crea TransaccionAbonado con tipo 201, partida 002)
CREATE OR REPLACE FUNCTION sp_registrar_posteo_manual(
    p_codigo_cliente VARCHAR(50),
    p_numrecibo INT,
    p_banco VARCHAR(100),
    p_valor DECIMAL(18,2),
    p_tipo_transaccion VARCHAR(10),
    p_partida VARCHAR(10),
    p_usuario VARCHAR(100)
) RETURNS BIGINT AS $$
DECLARE
    v_transaccion_id BIGINT;
    v_saldo_nuevo DECIMAL(18,2);
BEGIN
    -- Obtener saldo actual del cliente
    SELECT COALESCE(MAX(saldo), 0) - p_valor
      INTO v_saldo_nuevo
      FROM transaccion_abonado
     WHERE cliente_clave = p_codigo_cliente;
    
    -- Insertar TransaccionAbonado
    INSERT INTO transaccion_abonado (
        cliente_clave,
        tipo_transaccion,
        partida,
        descripcion,
        debitos,
        creditos,
        saldo,
        tipo_servicio,
        periodo,
        estado,
        fecha_registro,
        usuario,
        saldo_detalle
    ) VALUES (
        p_codigo_cliente,
        p_tipo_transaccion,        -- '201' (código contable de pago)
        p_partida,                  -- '002' (partida contable)
        'Posteo Manual - Recibo ' || p_numrecibo::TEXT || ' - Banco: ' || p_banco,
        0,                          -- débitos (no aplica en pago)
        p_valor,                    -- créditos (monto pagado)
        v_saldo_nuevo,              -- saldo actualizado
        'E',                        -- tipo servicio (Efectivo/entrada)
        TO_CHAR(NOW(), 'YYYYMM'),   -- período actual
        'A',                        -- estado Activo
        NOW(),                      -- fecha registro
        p_usuario,                  -- usuario que registra
        p_valor                     -- saldo detalle
    ) RETURNING id INTO v_transaccion_id;
    
    RETURN v_transaccion_id;
END;
$$ LANGUAGE plpgsql;

-- 8. Reversar posteo manual (borra/registra reversión en TransaccionAbonado)
CREATE OR REPLACE FUNCTION sp_reversar_posteo_manual(
    p_codigo_cliente VARCHAR(50),
    p_numrecibo INT,
    p_usuario VARCHAR(100)
) RETURNS VOID AS $$
DECLARE
    v_valor_original DECIMAL(18,2);
    v_saldo_nuevo DECIMAL(18,2);
BEGIN
    -- Buscar el monto original del pago
    SELECT creditos
      INTO v_valor_original
      FROM transaccion_abonado
     WHERE cliente_clave = p_codigo_cliente
       AND descripcion LIKE '%Recibo ' || p_numrecibo::TEXT || '%'
       AND tipo_transaccion = '201'
     ORDER BY fecha_registro DESC
     LIMIT 1;
    
    IF v_valor_original IS NULL THEN
        RAISE EXCEPTION 'No se encontró el pago original del recibo % para el cliente %', p_numrecibo, p_codigo_cliente;
    END IF;
    
    -- Calcular nuevo saldo (aumenta porque es reversión)
    SELECT COALESCE(MAX(saldo), 0) + v_valor_original
      INTO v_saldo_nuevo
      FROM transaccion_abonado
     WHERE cliente_clave = p_codigo_cliente;
    
    -- Marcar el pago original como reversado (opcional: cambiar estado a 'R')
    UPDATE transaccion_abonado
       SET estado = 'R'
     WHERE cliente_clave = p_codigo_cliente
       AND descripcion LIKE '%Recibo ' || p_numrecibo::TEXT || '%'
       AND tipo_transaccion = '201'
       AND estado = 'A';
    
    -- Insertar transacción de reversión
    INSERT INTO transaccion_abonado (
        cliente_clave,
        tipo_transaccion,
        partida,
        descripcion,
        debitos,
        creditos,
        saldo,
        tipo_servicio,
        periodo,
        estado,
        fecha_registro,
        usuario,
        saldo_detalle
    ) VALUES (
        p_codigo_cliente,
        '202',                      -- código de reversión
        '002',
        'REVERSO - Posteo Manual - Recibo ' || p_numrecibo::TEXT,
        v_valor_original,           -- débitos (devuelve el monto)
        0,                          -- créditos
        v_saldo_nuevo,              -- saldo actualizado
        'R',                        -- tipo servicio (Reverso)
        TO_CHAR(NOW(), 'YYYYMM'),
        'A',
        NOW(),
        p_usuario,
        v_valor_original
    );
END;
$$ LANGUAGE plpgsql;

-- ================================================
-- ÍNDICES Y PERMISOS
-- ================================================

-- Crear índices para optimizar búsquedas
CREATE INDEX IF NOT EXISTS idx_factura_numfactura_cliente ON factura(numfactura, clientecodigo);
CREATE INDEX IF NOT EXISTS idx_factura_estado_fecha ON factura(estado, fechaemision DESC);
CREATE INDEX IF NOT EXISTS idx_transaccion_cliente_tipo ON transaccion_abonado(cliente_clave, tipotransaccion);
CREATE INDEX IF NOT EXISTS idx_factura_detalle_factura ON factura_detalle(factura_id, id);

-- Comentarios de documentación
COMMENT ON FUNCTION sp_actualizar_detalle_posteolectora IS 'Actualiza saldos en factura_detalle para posteo de lector óptico';
COMMENT ON FUNCTION sp_actualizar_factura_pago IS 'Cambia estado de factura y registra banco/fecha de pago';
COMMENT ON FUNCTION sp_obtener_cliente_saldo IS 'Obtiene el saldo actual del cliente desde transaccion_abonado';
COMMENT ON FUNCTION get_matching_invoices IS 'Búsqueda de facturas/recibos por coincidencia para autocompletado';
COMMENT ON FUNCTION fn_getclientesaldos_posteomanual IS 'Retorna saldos pendientes con distribución por tipo de servicio para posteo manual';
COMMENT ON FUNCTION sp_actualizar_detalle_posteomanual IS 'Actualiza detalle de factura en posteo manual con montos distribuidos';
COMMENT ON FUNCTION sp_registrar_posteo_manual IS 'Registra TransaccionAbonado con tipo 201 (pago) y partida 002';
COMMENT ON FUNCTION sp_reversar_posteo_manual IS 'Revierte pago manual marcando original como reversado e insertando transacción 202';

-- ================================================
-- SCRIPT DE EJECUCIÓN
-- ================================================
-- Para ejecutar en desarrollo:
-- psql "host=localhost dbname=bdnes user=postgres password=<pwd>" -f Database/ddl_v3/captacion_pagos_stored_procedures.sql
--
-- Para ejecutar en QA/Producción:
-- Ajustar connection string y ejecutar vía migration tool o manualmente con validación previa
-- ================================================
