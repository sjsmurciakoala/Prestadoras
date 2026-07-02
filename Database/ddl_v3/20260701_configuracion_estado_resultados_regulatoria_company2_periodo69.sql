BEGIN;

DO $$
DECLARE
    v_company_id bigint := 2;
    v_periodo_id bigint := 69;
    v_missing_codes text;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM public.cfg_company
        WHERE company_id = v_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%', v_company_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.con_periodo_contable
        WHERE period_id = v_periodo_id
          AND company_id = v_company_id
    ) THEN
        RAISE EXCEPTION 'No existe con_periodo_contable.period_id=% para company_id=%', v_periodo_id, v_company_id;
    END IF;

    WITH required_config(numero_linea, tipo_linea, codigo_cuenta, descripcion_linea, mostrar_subtotal, nivel_indentacion) AS (
        VALUES
            (1::smallint, 0::smallint, '51000000000'::varchar, 'Ingresos por Venta de Servicios'::varchar, false, 0::smallint),
            (2::smallint, 0::smallint, '51100000000'::varchar, 'Servicio de Agua Potable'::varchar, false, 1::smallint),
            (3::smallint, 0::smallint, '51200000000'::varchar, 'Servicio de Alcantarillado'::varchar, false, 1::smallint),
            (4::smallint, 0::smallint, '51300000000'::varchar, 'Servicios Colaterales Regulados'::varchar, false, 1::smallint),
            (5::smallint, 0::smallint, '51400000000'::varchar, 'Actividades No reguladas'::varchar, false, 1::smallint),
            (6::smallint, 0::smallint, '52000000000'::varchar, 'Ingresos de No Operacion'::varchar, false, 0::smallint),
            (7::smallint, 0::smallint, '53100000000'::varchar, 'Donaciones y Transferencias Corrientes'::varchar, false, 0::smallint),
            (8::smallint, 0::smallint, '59000000000'::varchar, 'Otros Ingresos'::varchar, false, 0::smallint),
            (20::smallint, 1::smallint, '61000000000'::varchar, 'Costo de Agua Potable'::varchar, false, 0::smallint),
            (21::smallint, 1::smallint, '62000000000'::varchar, 'Costo Servicio de Alcantarillado'::varchar, false, 0::smallint),
            (22::smallint, 1::smallint, '63000000000'::varchar, 'Costo por Servicios Diversos'::varchar, false, 0::smallint),
            (23::smallint, 2::smallint, '71000000000'::varchar, 'Gastos de Comercializacion'::varchar, false, 0::smallint),
            (24::smallint, 2::smallint, '72000000000'::varchar, 'Gastos de Administracion'::varchar, false, 0::smallint),
            (25::smallint, 2::smallint, '79000000000'::varchar, 'Otros Gastos'::varchar, false, 0::smallint),
            (27::smallint, 2::smallint, '73000000000'::varchar, 'Gastos de Intereses'::varchar, false, 0::smallint),
            (29::smallint, 2::smallint, '74000000000'::varchar, 'Impuesto sobre la renta'::varchar, false, 0::smallint)
    )
    SELECT string_agg(r.codigo_cuenta, ', ' ORDER BY r.codigo_cuenta)
    INTO v_missing_codes
    FROM required_config r
    LEFT JOIN public.con_plan_cuentas p
      ON p.company_id = v_company_id
     AND p.code = r.codigo_cuenta
    WHERE p.account_id IS NULL;

    IF v_missing_codes IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan cuentas requeridas en con_plan_cuentas para company_id=%: %', v_company_id, v_missing_codes;
    END IF;

    DELETE FROM public.con_configuracion_linea_resultado
    WHERE company_id = v_company_id;

    WITH required_config(numero_linea, tipo_linea, codigo_cuenta, descripcion_linea, mostrar_subtotal, nivel_indentacion) AS (
        VALUES
            (1::smallint, 0::smallint, '51000000000'::varchar, 'Ingresos por Venta de Servicios'::varchar, false, 0::smallint),
            (2::smallint, 0::smallint, '51100000000'::varchar, 'Servicio de Agua Potable'::varchar, false, 1::smallint),
            (3::smallint, 0::smallint, '51200000000'::varchar, 'Servicio de Alcantarillado'::varchar, false, 1::smallint),
            (4::smallint, 0::smallint, '51300000000'::varchar, 'Servicios Colaterales Regulados'::varchar, false, 1::smallint),
            (5::smallint, 0::smallint, '51400000000'::varchar, 'Actividades No reguladas'::varchar, false, 1::smallint),
            (6::smallint, 0::smallint, '52000000000'::varchar, 'Ingresos de No Operacion'::varchar, false, 0::smallint),
            (7::smallint, 0::smallint, '53100000000'::varchar, 'Donaciones y Transferencias Corrientes'::varchar, false, 0::smallint),
            (8::smallint, 0::smallint, '59000000000'::varchar, 'Otros Ingresos'::varchar, false, 0::smallint),
            (20::smallint, 1::smallint, '61000000000'::varchar, 'Costo de Agua Potable'::varchar, false, 0::smallint),
            (21::smallint, 1::smallint, '62000000000'::varchar, 'Costo Servicio de Alcantarillado'::varchar, false, 0::smallint),
            (22::smallint, 1::smallint, '63000000000'::varchar, 'Costo por Servicios Diversos'::varchar, false, 0::smallint),
            (23::smallint, 2::smallint, '71000000000'::varchar, 'Gastos de Comercializacion'::varchar, false, 0::smallint),
            (24::smallint, 2::smallint, '72000000000'::varchar, 'Gastos de Administracion'::varchar, false, 0::smallint),
            (25::smallint, 2::smallint, '79000000000'::varchar, 'Otros Gastos'::varchar, false, 0::smallint),
            (27::smallint, 2::smallint, '73000000000'::varchar, 'Gastos de Intereses'::varchar, false, 0::smallint),
            (29::smallint, 2::smallint, '74000000000'::varchar, 'Impuesto sobre la renta'::varchar, false, 0::smallint)
    )
    INSERT INTO public.con_configuracion_linea_resultado (
        company_id,
        periodo_id,
        numero_linea,
        tipo_linea,
        codigo_cuenta,
        descripcion_linea,
        columna_reporte,
        mostrar_subtotal,
        nivel_indentacion,
        created_at,
        created_by
    )
    SELECT
        v_company_id,
        v_periodo_id,
        r.numero_linea,
        r.tipo_linea,
        r.codigo_cuenta,
        r.descripcion_linea,
        1,
        r.mostrar_subtotal,
        r.nivel_indentacion::smallint,
        timezone('utc', now()),
        'codex'
    FROM required_config r
    ORDER BY r.numero_linea;

    RAISE NOTICE 'Configuracion de Estado de Resultados actualizada para company_id=%, periodo_id=%.', v_company_id, v_periodo_id;
END $$;

COMMIT;
