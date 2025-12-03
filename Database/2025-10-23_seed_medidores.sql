-- Demo de medidores para CLI-DEMO-001
-- Ejecutar con: psql -h 3.208.232.209 -U postgres -d bdnes -f Database/2025-10-23_seed_medidores.sql

DO $$
DECLARE
    v_medidor_id int;
    v_cliente_id int;
    v_detalle_id int;
BEGIN
    -- Crear o actualizar el medidor demo
    SELECT maestro_medidor_id
      INTO v_medidor_id
      FROM maestro_medidor
     WHERE maestro_medidor_numero = 'MED-DEMO-001';

    IF v_medidor_id IS NULL THEN
        INSERT INTO maestro_medidor (
            maestro_medidor_numero,
            maestro_medidor_marca,
            maestro_medidor_fecha_instala,
            maestro_medidor_diametro,
            maestro_medidor_empleado,
            maestro_medidor_acueducto,
            estado,
            usuariocreacion,
            fechacreacion
        )
        VALUES (
            'MED-DEMO-001',
            'Sensus',
            CURRENT_DATE - INTERVAL '180 days',
            0.75,
            'Seed Installer',
            'ACUEDUCTO DEMO',
            TRUE,
            'seed-medidores',
            CURRENT_TIMESTAMP
        )
        RETURNING maestro_medidor_id INTO v_medidor_id;
    ELSE
        UPDATE maestro_medidor
           SET maestro_medidor_marca = 'Sensus',
               maestro_medidor_fecha_instala = CURRENT_DATE - INTERVAL '180 days',
               maestro_medidor_diametro = 0.75,
               maestro_medidor_empleado = 'Seed Installer',
               maestro_medidor_acueducto = 'ACUEDUCTO DEMO',
               estado = TRUE,
               usuariomodificacion = 'seed-medidores',
               fechamodificacion = CURRENT_TIMESTAMP
         WHERE maestro_medidor_id = v_medidor_id;
    END IF;

    -- Asociar al cliente demo
    SELECT maestro_cliente_id
      INTO v_cliente_id
      FROM cliente_maestro
     WHERE maestro_cliente_clave = 'CLI-DEMO-001';

    IF v_cliente_id IS NOT NULL THEN
        SELECT detalle_cliente_id
          INTO v_detalle_id
          FROM cliente_detalle
         WHERE maestro_cliente_id = v_cliente_id
         ORDER BY detalle_cliente_id
         LIMIT 1;

        IF v_detalle_id IS NULL THEN
            INSERT INTO cliente_detalle (
                maestro_cliente_id,
                detalle_cliente_telefono,
                detalle_cliente_direccion,
                estado,
                usuariocreacion,
                fechacreacion,
                maestro_medidor_id
            )
            VALUES (
                v_cliente_id,
                NULL,
                NULL,
                TRUE,
                'seed-medidores',
                CURRENT_TIMESTAMP,
                v_medidor_id
            );
        ELSE
            UPDATE cliente_detalle
               SET maestro_medidor_id = v_medidor_id,
                   estado = TRUE,
                   usuariomodificacion = 'seed-medidores',
                   fechamodificacion = CURRENT_TIMESTAMP
             WHERE detalle_cliente_id = v_detalle_id;
        END IF;

        UPDATE cliente_maestro
           SET maestro_cliente_tiene_medidor = TRUE,
               usuariomodificacion = 'seed-medidores',
               fechamodificacion = CURRENT_TIMESTAMP
         WHERE maestro_cliente_id = v_cliente_id;
    END IF;

    -- Limpiar lecturas anteriores del demo
    DELETE FROM historicomedicion
     WHERE contador = 'MED-DEMO-001'
        OR (clave = 'CLI-DEMO-001' AND usuario = 'seed-medidores');

    -- Lecturas de ejemplo
    INSERT INTO historicomedicion (
        ano,
        mes,
        contador,
        clave,
        fecha,
        usuario,
        lect_act,
        lect_ant,
        consumo,
        condicion,
        observacion
    )
    VALUES 
        (EXTRACT(YEAR FROM CURRENT_DATE) - 1, EXTRACT(MONTH FROM CURRENT_DATE),
         'MED-DEMO-001', 'CLI-DEMO-001', (CURRENT_DATE - INTERVAL '60 days')::date,
         'seed-medidores', 1200, 1150, 50, 'NOR', 'Lectura demo (hace 60 días)'),
        (EXTRACT(YEAR FROM CURRENT_DATE), EXTRACT(MONTH FROM CURRENT_DATE),
         'MED-DEMO-001', 'CLI-DEMO-001', (CURRENT_DATE - INTERVAL '30 days')::date,
         'seed-medidores', 1255, 1200, 55, 'NOR', 'Lectura demo (hace 30 días)');
END;
$$;
