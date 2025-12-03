-- Demo data for Clientes module (tarifas + estado de cuenta)
-- Ejecutar en bdnes

DO $$
DECLARE
    v_barrio_code text := 'D01';
    v_categoria_id integer;
    v_ciclo_id integer;
    v_tipo_codigo text := 'DR';
    v_servicio_id integer;
    v_cliente_id integer;
    v_tarifa_catalogo_id integer;
    v_configuracion_id integer;
BEGIN
    -- barrio
    INSERT INTO public.barrio (barrio_codigo, descripcion, estado, usuariocreacion, fechacreacion)
    VALUES (v_barrio_code, 'Barrio Demo', true, 'seed', NOW())
    ON CONFLICT (barrio_codigo) DO NOTHING;

    -- categoria servicio
    SELECT categoria_servicio_id
      INTO v_categoria_id
      FROM public.categoria_servicio
     WHERE descripcion = 'Demo Residencial'
     LIMIT 1;

    IF v_categoria_id IS NULL THEN
        INSERT INTO public.categoria_servicio (descripcion, estado, usuariocreacion, fechacreacion)
        VALUES ('Demo Residencial', true, 'seed', NOW())
        RETURNING categoria_servicio_id INTO v_categoria_id;
    END IF;

    -- ciclo
    SELECT ciclos_id
      INTO v_ciclo_id
      FROM public.ciclos
     WHERE ciclos_codigo = 'CIC-DEMO-01'
     LIMIT 1;

    IF v_ciclo_id IS NULL THEN
        INSERT INTO public.ciclos (ciclos_codigo, ciclos_descripcioncorta, ciclos_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES ('CIC-DEMO-01', 'Ciclo Demo', 'Ciclo Demo para semillas', true, 'seed', NOW())
        RETURNING ciclos_id INTO v_ciclo_id;
    END IF;

    -- tipo de uso
    INSERT INTO public.tipo_uso_servicio (tipo_uso_codigo, descripcion, estado, usuariocreacion, fechacreacion)
    VALUES (v_tipo_codigo, 'Uso Demo Residencial', true, 'seed', NOW())
    ON CONFLICT (tipo_uso_codigo) DO NOTHING;

    -- servicio base
    SELECT servicios_id
      INTO v_servicio_id
      FROM public.servicios
     WHERE servicios_codigo = 'SRV-DEMO-AGUA'
     LIMIT 1;

    IF v_servicio_id IS NULL THEN
        INSERT INTO public.servicios (servicios_codigo, servicios_descripcioncorta, servicios_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES ('SRV-DEMO-AGUA', 'Servicio Agua Demo', 'Servicio de agua potable para cliente demo', true, 'seed', NOW())
        RETURNING servicios_id INTO v_servicio_id;
    END IF;

    -- cliente maestro
    INSERT INTO public.cliente_maestro (
        maestro_cliente_clave,
        maestro_cliente_identidad,
        maestro_cliente_rtn,
        maestro_cliente_nombre,
        maestro_cliente_tercera_edad,
        categoria_servicio_id,
        barrio_codigo,
        estado,
        usuariocreacion,
        fechacreacion,
        usuariomodificacion,
        fechamodificacion,
        tipo_uso_codigo,
        ciclos_id,
        maestro_cliente_tiene_contrato,
        maestro_cliente_tiene_convenio,
        maestro_cliente_tiene_medidor)
    VALUES (
        'CLI-DEMO-001',
        '0801199000001',
        '08011990000012',
        'Cliente Demo Blazor',
        false,
        v_categoria_id,
        v_barrio_code,
        true,
        'seed', NOW(),
        'seed', NOW(),
        v_tipo_codigo,
        v_ciclo_id,
        true,
        false,
        true)
    ON CONFLICT (maestro_cliente_clave)
    DO UPDATE SET maestro_cliente_nombre = EXCLUDED.maestro_cliente_nombre,
                  categoria_servicio_id    = EXCLUDED.categoria_servicio_id,
                  barrio_codigo            = EXCLUDED.barrio_codigo,
                  tipo_uso_codigo          = EXCLUDED.tipo_uso_codigo
    RETURNING maestro_cliente_id INTO v_cliente_id;

    -- detalle de cliente
    DELETE FROM public.cliente_detalle
          WHERE maestro_cliente_id = v_cliente_id;

    INSERT INTO public.cliente_detalle (
        maestro_cliente_id,
        detalle_cliente_telefono,
        detalle_cliente_movil,
        detalle_cliente_email,
        detalle_cliente_direccion,
        estado,
        usuariocreacion,
        fechacreacion)
    VALUES (v_cliente_id, '22223333', '99998888', 'cliente.demo@apc.test', 'Colonia Demo #123', true, 'seed', NOW());

    -- tarifas catálogo demo
    SELECT tarifa_catalogo_id
      INTO v_tarifa_catalogo_id
      FROM public.tarifas_catalogo
     WHERE nombre = 'Tarifa Demo Residencial'
     LIMIT 1;

    IF v_tarifa_catalogo_id IS NULL THEN
        INSERT INTO public.tarifas_catalogo (nombre, precio_base, cargo_fijo, descripcion, activo, fecha_creacion, fecha_modificacion)
        VALUES ('Tarifa Demo Residencial', 25.50, 5.75, 'Tarifa de pruebas para clientes demo', true, NOW(), NOW())
        RETURNING tarifa_catalogo_id INTO v_tarifa_catalogo_id;
    END IF;

    -- configuración de tasas
    SELECT configuracion_tasas_id
      INTO v_configuracion_id
      FROM public.configuracion_tasas
     WHERE maestro_cliente_id = v_cliente_id
     LIMIT 1;

    IF v_configuracion_id IS NULL THEN
        INSERT INTO public.configuracion_tasas (
            maestro_cliente_id,
            estado,
            usuariocreacion,
            fechacreacion,
            usuariomodificacion,
            fechamodificacion,
            tarifa_catalogo_id)
        VALUES (v_cliente_id, true, 'seed', NOW(), 'seed', NOW(), v_tarifa_catalogo_id)
        RETURNING configuracion_tasas_id INTO v_configuracion_id;
    ELSE
        UPDATE public.configuracion_tasas
           SET estado = true,
               tarifa_catalogo_id = v_tarifa_catalogo_id,
               usuariomodificacion = 'seed',
               fechamodificacion = NOW()
         WHERE configuracion_tasas_id = v_configuracion_id;
    END IF;

    DELETE FROM public.configuracion_tasas_detalle
          WHERE configuracion_tasas_id = v_configuracion_id;

    INSERT INTO public.configuracion_tasas_detalle (
        configuracion_tasas_id,
        servicios_id,
        configuracion_tasas_detalle_aplicaservicio,
        configuracion_tasas_detalle_monto,
        estado,
        usuariocreacion,
        fechacreacion)
    VALUES (v_configuracion_id, v_servicio_id, true, 12.34, true, 'seed', NOW());

    DELETE FROM public.transaccion_abonado
          WHERE cliente_clave = 'CLI-DEMO-001';

    INSERT INTO public.transaccion_abonado (
        cliente_clave,
        recibo,
        tipotransaccion,
        descripcion,
        creditos,
        debitos,
        saldo,
        fecha_docu,
        usuario)
    VALUES
        ('CLI-DEMO-001', 1001, 'PAGO', 'Pago portal bancario', 150.00, 0, 45.25, CURRENT_DATE - INTERVAL '10 days', 'seed'),
        ('CLI-DEMO-001', 1002, 'CARGO', 'Cargo mensual servicio agua', 0, 195.25, 195.25, CURRENT_DATE - INTERVAL '5 days', 'seed');

    DELETE FROM public.historicomedicion
          WHERE clave = 'CLI-DEMO-001';

    INSERT INTO public.historicomedicion (
        ano,
        mes,
        clave,
        fecha,
        consumo)
    VALUES
        (EXTRACT(YEAR FROM CURRENT_DATE)::numeric, EXTRACT(MONTH FROM CURRENT_DATE)::numeric, 'CLI-DEMO-001', CURRENT_DATE - INTERVAL '30 days', 18),
        (EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '1 month')::numeric, EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '1 month')::numeric, 'CLI-DEMO-001', CURRENT_DATE - INTERVAL '60 days', 22);

    RAISE NOTICE 'Cliente demo creado con ID % y clave %', v_cliente_id, 'CLI-DEMO-001';
END
$$;
