-- Demo data for Facturación Miscelánea (CLI-DEMO-001)
-- Ejecutar en bdnes

DO $$
DECLARE
    v_cliente text := 'CLI-DEMO-001';
    v_periodo text := to_char(current_date, 'YYYYMM');
    v_factura_id integer;
    v_numrecibo integer;
    v_total numeric(18,2);
BEGIN
    -- Catálogo de misceláneos demo
    INSERT INTO public.miscelaneos_catalogo (codigo, nombre, valor)
    VALUES
        ('MIS-DEMO-001', 'Derecho de reconexión demo', 150.00),
        ('MIS-DEMO-002', 'Reposición de medidor demo', 275.50)
    ON CONFLICT DO NOTHING;

    -- Limpiar recibos anteriores marcados como demo
    DELETE FROM public.factura_detalle
     WHERE factura_id IN (SELECT id FROM public.factura WHERE referencia = 'MIS-DEMO-SEED');

    DELETE FROM public.factura
     WHERE referencia = 'MIS-DEMO-SEED';

    DELETE FROM public.transaccion_abonado
     WHERE cliente_clave = v_cliente
       AND usuario = 'seed-miscelaneos';

    v_total := 425.50;

    -- Crear recibo misceláneo
    INSERT INTO public.factura (
        numfactura,
        clientecodigo,
        tipofactura,
        ano,
        mes,
        fechaemision,
        fechavence,
        rtn,
        periodo,
        numdei,
        saldototal,
        usuario,
        identidad,
        estado,
        referencia)
    VALUES (
        'MIS-DEMO-' || to_char(NOW(), 'YYYYMMDDHH24MISS'),
        v_cliente,
        'R',
        extract(year FROM current_date)::text,
        lpad(extract(month FROM current_date)::text, 2, '0'),
        current_date,
        current_date,
        '08011990000012',
        v_periodo,
        '',
        v_total,
        'seed-miscelaneos',
        null,
        'A',
        'MIS-DEMO-SEED')
    RETURNING id, numrecibo INTO v_factura_id, v_numrecibo;

    -- Detalle de conceptos
    INSERT INTO public.factura_detalle (numrecibo, codigo, tiposervicio, descripcion, montovalor, factura_id, montovalor_saldo)
    VALUES
        (v_numrecibo, 'MIS-DEMO-001', 'MISC', 'Derecho de reconexión demo', 150.00, v_factura_id, 150.00),
        (v_numrecibo, 'MIS-DEMO-002', 'MISC', 'Reposición de medidor demo', 275.50, v_factura_id, 275.50);

    -- Movimientos del abonado
    INSERT INTO public.transaccion_abonado (
        cliente_clave,
        recibo,
        tipotransaccion,
        docufuente,
        docufuente2,
        fecha_docu,
        tipo_partida,
        descripcion,
        debitos,
        creditos,
        saldo,
        tipo_servicio,
        periodo,
        estado,
        fecha_registro,
        usuario,
        saldo_detalle)
    VALUES
        (v_cliente, v_numrecibo, 'MIS-DEMO-001', v_factura_id, NULL, current_date, '01',
         'Derecho de reconexión demo', 150.00, 0, 150.00, 'E', v_periodo, 'A', current_date, 'seed-miscelaneos', 150.00),
        (v_cliente, v_numrecibo, 'MIS-DEMO-002', v_factura_id, NULL, current_date, '01',
         'Reposición de medidor demo', 275.50, 0, 425.50, 'E', v_periodo, 'A', current_date, 'seed-miscelaneos', 275.50);

    RAISE NOTICE 'Recibo demo % generado para %', v_numrecibo, v_cliente;
END
$$;
