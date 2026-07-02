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

    WITH required_config(numero_linea, clase, codigo_cuenta, descripcion_linea, mostrar_en_reporte) AS (
        VALUES
            (1::smallint, 1::smallint, '11100000000'::varchar, 'Efectivo y Equivalentes'::varchar, true),
            (2::smallint, 1::smallint, '11200000000'::varchar, 'Inversiones Financieras Corrientes'::varchar, true),
            (3::smallint, 1::smallint, '11300000000'::varchar, 'Cuentas y Documentos por Cobrar'::varchar, true),
            (4::smallint, 1::smallint, '11400000000'::varchar, 'Inventarios'::varchar, true),
            (5::smallint, 1::smallint, '11500000000'::varchar, 'Gastos Pagados por Adelantado'::varchar, true),
            (6::smallint, 2::smallint, '12100000000'::varchar, 'Cuentas y Documentos por cobrar de Largo Plazo'::varchar, true),
            (7::smallint, 2::smallint, '12200000000'::varchar, 'Inversiones Financieras no Corrientes'::varchar, true),
            (8::smallint, 2::smallint, '12300000000'::varchar, 'Propiedad, Planta y Equipo'::varchar, true),
            (9::smallint, 2::smallint, '12400000000'::varchar, 'Construcciones'::varchar, true),
            (10::smallint, 2::smallint, '12500000000'::varchar, 'Bienes Inmateriales'::varchar, true),
            (11::smallint, 3::smallint, '21100000000'::varchar, 'Cuentas por Pagar'::varchar, true),
            (12::smallint, 3::smallint, '21200000000'::varchar, 'Endeudamiento de Corto Plazo'::varchar, true),
            (13::smallint, 3::smallint, '21900000000'::varchar, 'Otros Pasivos Corrientes'::varchar, true),
            (14::smallint, 4::smallint, '22100000000'::varchar, 'Deudas de Largo Plazo'::varchar, true),
            (15::smallint, 4::smallint, '22200000000'::varchar, 'Provisiones para Pasivos'::varchar, true),
            (16::smallint, 5::smallint, '31100000000'::varchar, 'Capital Social'::varchar, true),
            (17::smallint, 5::smallint, '31200000000'::varchar, 'Donaciones y Transferencias de Capital Recibidas'::varchar, true),
            (18::smallint, 5::smallint, '31300000000'::varchar, 'Reservas'::varchar, true),
            (19::smallint, 5::smallint, '31400000000'::varchar, 'Revaluos'::varchar, true),
            (20::smallint, 5::smallint, '31500000000'::varchar, 'Resultados'::varchar, true),
            (90::smallint, 7::smallint, '41000000000'::varchar, 'Cuentas de Orden Deudoras'::varchar, false),
            (91::smallint, 8::smallint, '42000000000'::varchar, 'Cuentas de Orden Acreedoras'::varchar, false)
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

    DELETE FROM public.con_configuracion_balance
    WHERE company_id = v_company_id
      AND periodo_id = v_periodo_id;

    WITH required_config(numero_linea, clase, codigo_cuenta, descripcion_linea, mostrar_en_reporte) AS (
        VALUES
            (1::smallint, 1::smallint, '11100000000'::varchar, 'Efectivo y Equivalentes'::varchar, true),
            (2::smallint, 1::smallint, '11200000000'::varchar, 'Inversiones Financieras Corrientes'::varchar, true),
            (3::smallint, 1::smallint, '11300000000'::varchar, 'Cuentas y Documentos por Cobrar'::varchar, true),
            (4::smallint, 1::smallint, '11400000000'::varchar, 'Inventarios'::varchar, true),
            (5::smallint, 1::smallint, '11500000000'::varchar, 'Gastos Pagados por Adelantado'::varchar, true),
            (6::smallint, 2::smallint, '12100000000'::varchar, 'Cuentas y Documentos por cobrar de Largo Plazo'::varchar, true),
            (7::smallint, 2::smallint, '12200000000'::varchar, 'Inversiones Financieras no Corrientes'::varchar, true),
            (8::smallint, 2::smallint, '12300000000'::varchar, 'Propiedad, Planta y Equipo'::varchar, true),
            (9::smallint, 2::smallint, '12400000000'::varchar, 'Construcciones'::varchar, true),
            (10::smallint, 2::smallint, '12500000000'::varchar, 'Bienes Inmateriales'::varchar, true),
            (11::smallint, 3::smallint, '21100000000'::varchar, 'Cuentas por Pagar'::varchar, true),
            (12::smallint, 3::smallint, '21200000000'::varchar, 'Endeudamiento de Corto Plazo'::varchar, true),
            (13::smallint, 3::smallint, '21900000000'::varchar, 'Otros Pasivos Corrientes'::varchar, true),
            (14::smallint, 4::smallint, '22100000000'::varchar, 'Deudas de Largo Plazo'::varchar, true),
            (15::smallint, 4::smallint, '22200000000'::varchar, 'Provisiones para Pasivos'::varchar, true),
            (16::smallint, 5::smallint, '31100000000'::varchar, 'Capital Social'::varchar, true),
            (17::smallint, 5::smallint, '31200000000'::varchar, 'Donaciones y Transferencias de Capital Recibidas'::varchar, true),
            (18::smallint, 5::smallint, '31300000000'::varchar, 'Reservas'::varchar, true),
            (19::smallint, 5::smallint, '31400000000'::varchar, 'Revaluos'::varchar, true),
            (20::smallint, 5::smallint, '31500000000'::varchar, 'Resultados'::varchar, true),
            (90::smallint, 7::smallint, '41000000000'::varchar, 'Cuentas de Orden Deudoras'::varchar, false),
            (91::smallint, 8::smallint, '42000000000'::varchar, 'Cuentas de Orden Acreedoras'::varchar, false)
    )
    INSERT INTO public.con_configuracion_balance (
        company_id,
        periodo_id,
        numero_linea,
        clase,
        codigo_cuenta,
        descripcion_cuenta,
        descripcion_linea,
        porcentaje_activo,
        mostrar_en_reporte,
        created_at,
        created_by
    )
    SELECT
        v_company_id,
        v_periodo_id,
        r.numero_linea,
        r.clase,
        r.codigo_cuenta,
        p.name,
        r.descripcion_linea,
        0,
        r.mostrar_en_reporte,
        now(),
        'codex-regulatorio'
    FROM required_config r
    JOIN public.con_plan_cuentas p
      ON p.company_id = v_company_id
     AND p.code = r.codigo_cuenta
    ORDER BY r.numero_linea;
END;
$$;

COMMIT;
