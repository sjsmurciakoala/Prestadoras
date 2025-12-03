-- Demo data for Notas Crédito/Débito y Cobranza
-- Ejecutar después de Database/2025-10-18_seed_cliente_demo.sql

DO $$
DECLARE
    v_cliente        text    := 'CLI-DEMO-001';
    v_servicio       text    := 'SRV-DEMO-AGUA';
    v_cliente_id     integer;
    v_servicio_id    integer;
    v_motivo_id      integer;
    v_ajuste_id      integer;
    v_plan_id        integer;
    v_recibo         numeric := 8001;
BEGIN
    SELECT maestro_cliente_id
      INTO v_cliente_id
      FROM public.cliente_maestro
     WHERE maestro_cliente_clave = v_cliente;

    IF v_cliente_id IS NULL THEN
        RAISE NOTICE 'Cliente demo % no existe, ejecutar primero 2025-10-18_seed_cliente_demo.sql', v_cliente;
        RETURN;
    END IF;

    SELECT servicios_id
      INTO v_servicio_id
      FROM public.servicios
     WHERE servicios_codigo = v_servicio;

    IF v_servicio_id IS NULL THEN
        INSERT INTO public.servicios (servicios_codigo, servicios_descripcioncorta, servicios_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES (v_servicio, 'Servicio Demo Notas', 'Servicio demo para notas y cobranza', true, 'seed-notas', now())
        RETURNING servicios_id INTO v_servicio_id;
    END IF;

    INSERT INTO public.causa_refacturacion (codigo, descripcion, tipo)
    VALUES ('DEM-NOTA', 'Ajuste demo migración Blazor', 'D')
    ON CONFLICT DO NOTHING;

    SELECT ide INTO v_motivo_id
      FROM public.causa_refacturacion
     WHERE codigo = 'DEM-NOTA'
     LIMIT 1;

    -- limpiar registros previos marcados como demo
    DELETE FROM public.ajustes_detalle
     WHERE documento IN (SELECT documento FROM public.ajustes WHERE usuario = 'seed-notas');

    DELETE FROM public.ajustes
     WHERE usuario = 'seed-notas';

    INSERT INTO public.ajustes (
        fecha,
        estado,
        observacion,
        total,
        motivo,
        tipo_nota,
        saldo,
        periodo,
        lectura,
        usuario,
        cliente_clave,
        correlativo)
    VALUES (
        current_date,
        'A',
        'Nota demo migración',
        75.00,
        v_motivo_id,
        2,
        75.00,
        to_char(current_date, 'YYYYMM'),
        0,
        'seed-notas',
        v_cliente,
        'NDEM01')
    RETURNING documento INTO v_ajuste_id;

    INSERT INTO public.ajustes_detalle (documento, tipo_servicio, monto)
    VALUES (v_ajuste_id, v_servicio, 75.00);

    -- Plan de pago demo
    DELETE FROM public.cln_plan_pago_dtl
     WHERE idhdr IN (SELECT id FROM public.cln_plan_pago_hdr WHERE usuariocreacion = 'seed-cobranza');

    DELETE FROM public.cln_plan_pago_hdr
     WHERE usuariocreacion = 'seed-cobranza';

    INSERT INTO public.cln_plan_pago_hdr (
        correlativo,
        clienteid,
        monto,
        direccion,
        representante,
        docrepresentante,
        numrepresentante,
        fecha,
        fechappago,
        comentario,
        porcprima,
        vprima,
        montofinanc,
        meses,
        estadopago,
        usuariocreacion,
        fechacreacion,
        usuariomodificacion,
        fechamodificacion)
    VALUES (
        '000101',
        v_cliente_id,
        420.00,
        'Colonia Demo #123',
        'Jane Demo',
        '08011990000012',
        '99998888',
        current_date,
        (current_date + INTERVAL '1 month')::date,
        'Plan demo migración',
        10,
        42.00,
        378.00,
        6,
        'Pendiente',
        'seed-cobranza',
        now(),
        'seed-cobranza',
        now())
    RETURNING id INTO v_plan_id;

    FOR i IN 0..5 LOOP
        INSERT INTO public.cln_plan_pago_dtl (
            idhdr,
            valorcuota,
            fechacuota,
            mes,
            estadopago,
            usuariocreacion,
            fechacreacion,
            usuariomodificacion,
            fechamodificacion)
        VALUES (
            v_plan_id,
            63.00,
            (current_date + INTERVAL '1 month' + (i * INTERVAL '1 month'))::date,
            i + 1,
            'Pendiente',
            'seed-cobranza',
            now(),
            'seed-cobranza',
            now());
    END LOOP;

    DELETE FROM public.transaccion_abonado
     WHERE cliente_clave = v_cliente
       AND usuario = 'seed-cobranza';

    INSERT INTO public.transaccion_abonado (
        cliente_clave,
        recibo,
        tipotransaccion,
        docufuente,
        fecha_docu,
        tipo_partida,
        descripcion,
        debitos,
        creditos,
        saldo,
        periodo,
        estado,
        fecha_registro,
        usuario,
        saldo_detalle)
    VALUES
        (v_cliente, v_recibo, 'PLAN', v_plan_id, current_date, '01', 'Traslado de Fondos', 0, 378.00, 0, to_char(current_date, 'YYYY/MM'), 'C', current_date, 'seed-cobranza', 0),
        (v_cliente, v_recibo, 'PLAN-PR', v_plan_id, current_date, '01', 'Concepto de Prima', 42.00, 0, 42.00, to_char(current_date, 'YYYY/MM'), 'A', current_date, 'seed-cobranza', 42.00);

    RAISE NOTICE 'Nota demo % y plan % creados para %', v_ajuste_id, v_plan_id, v_cliente;
END
$$;
