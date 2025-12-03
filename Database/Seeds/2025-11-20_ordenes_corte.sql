-- Seeds demo para órdenes de trabajo (agua y corte)
-- Ejecutar después de: 2025-10-18_seed_cliente_demo.sql, 2025-11-05_dummy_usuarios_miorden.sql, orden_trabajo_estado.sql

DO $$
DECLARE
    v_cliente_corte text := 'CLI-DEMO-001';
    v_cliente_agua  text := 'CLI-DEMO-001';
    v_numero_corte  int  := 9001;
    v_numero_agua   int  := 9002;
BEGIN
    -- Orden de corte demo
    IF NOT EXISTS (SELECT 1 FROM public.orden_trabajo WHERE orden_numero = v_numero_corte) THEN
        INSERT INTO public.orden_trabajo (
            orden_numero,
            maestro_cliente_clave,
            concepto,
            estado,
            fecha,
            fecha_creacion,
            informe,
            ano,
            mes,
            saldo,
            usuario,
            tipo,
            empleado,
            personas)
        VALUES (
            v_numero_corte,
            v_cliente_corte,
            'Corte por morosidad (demo)',
            'P',
            current_date - INTERVAL '2 days',
            now(),
            'Orden generada por seed demo',
            extract(year FROM current_date)::int,
            extract(month FROM current_date)::int,
            425.50,
            'seed-corte',
            '03', -- corte
            'corte.centro',
            'Inspector Corte'
        );
    END IF;

    -- Orden de reconexión / agua demo
    IF NOT EXISTS (SELECT 1 FROM public.orden_trabajo WHERE orden_numero = v_numero_agua) THEN
        INSERT INTO public.orden_trabajo (
            orden_numero,
            maestro_cliente_clave,
            concepto,
            estado,
            fecha,
            fecha_creacion,
            informe,
            ano,
            mes,
            saldo,
            usuario,
            tipo,
            empleado,
            personas)
        VALUES (
            v_numero_agua,
            v_cliente_agua,
            'Reconexión servicio agua (demo)',
            'A',
            current_date - INTERVAL '1 day',
            now(),
            'Orden asignada a cuadrilla Agua Norte',
            extract(year FROM current_date)::int,
            extract(month FROM current_date)::int,
            0,
            'seed-agua',
            '01', -- agua
            'agua.norte',
            'Técnico Reconexión'
        );
    END IF;

    -- Adjuntos demo
    IF NOT EXISTS (SELECT 1 FROM public.orden_trabajo_adjunto WHERE numeroorden = v_numero_corte::text) THEN
        INSERT INTO public.orden_trabajo_adjunto (nombre, tipo, latitud, longitud, numeroorden, fechainicio, fechafin)
        VALUES (
            'Foto-medidor.jpg',
            'image/jpeg',
            '14.102233',
            '-87.204455',
            v_numero_corte::text,
            now() - INTERVAL '1 day',
            now() - INTERVAL '1 day' + INTERVAL '2 minutes'
        );
    END IF;

    -- Materiales usados
    IF NOT EXISTS (SELECT 1 FROM public.ordent_mate WHERE numero = v_numero_corte) THEN
        INSERT INTO public.ordent_mate (cuenta, numero, codproduc, descripcion, cantidad, fecha)
        VALUES
            ('099-001', v_numero_corte, 'VALV-01', 'Válvula corte 1"', 1, current_date - INTERVAL '2 days'),
            ('099-001', v_numero_agua, 'SELLO-AGUA', 'Sello reconexión', 1, current_date - INTERVAL '1 day');
    END IF;

    -- Coordenadas demo para mapa
    IF NOT EXISTS (SELECT 1 FROM public.coordenadas_empleado WHERE nombre = 'corte.centro' AND fecha::date = current_date) THEN
        INSERT INTO public.coordenadas_empleado (nombre, ano, mes, dia, fecha, latitud, longitud)
        VALUES
            ('corte.centro', extract(year FROM current_date)::int, extract(month FROM current_date)::int, extract(day FROM current_date)::int,
             current_date + INTERVAL '08:15', '14.101900', '-87.203900'),
            ('agua.norte', extract(year FROM current_date)::int, extract(month FROM current_date)::int, extract(day FROM current_date)::int,
             current_date + INTERVAL '09:05', '14.105500', '-87.198800');
    END IF;
END
$$;
