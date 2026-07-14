-- =============================================================================
-- Configuracion regulatoria ERSAPS del Estado de Flujos de Efectivo para
-- company_id = 2. Mapea cada linea del formato del Manual de Contabilidad
-- Regulatoria a las cuentas del plan regulatorio que actuan como CONTRAPARTIDA
-- de los movimientos de efectivo (metodo directo).
--
-- actividad: 0=OPERACION, 1=INVERSION, 2=FINANCIACION, 9=EFECTIVO.
-- tipo_linea: 0=cobro, 1=pago, 2=etiqueta.
-- Una misma linea puede tener varias cuentas (varias filas con el mismo
-- numero_linea). Una misma cuenta puede estar en una linea de cobro y otra de
-- pago (p.ej. prestamos): el SP la enruta por el signo del movimiento.
-- Las lineas con codigo_cuenta NULL se imprimen sin monto hasta que la empresa
-- las mapee a una cuenta.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_company_id bigint := 2;
    v_missing_codes text;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM public.cfg_company
        WHERE company_id = v_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%', v_company_id;
    END IF;

    WITH required_config(numero_linea, actividad, tipo_linea, codigo_cuenta, descripcion_linea) AS (
        VALUES
            -- ============ ACTIVIDADES DE OPERACION ============
            (1::smallint, 0::smallint, 0::smallint, '11300000000'::varchar, 'Cobranzas de ventas de bienes o servicios e ingresos operacionales'::varchar),
            (1::smallint, 0::smallint, 0::smallint, '51000000000'::varchar, 'Cobranzas de ventas de bienes o servicios e ingresos operacionales'::varchar),
            (2::smallint, 0::smallint, 0::smallint, NULL::varchar, 'Cobranzas de regalias, honorarios, comisiones y otros'::varchar),
            (3::smallint, 0::smallint, 0::smallint, '52000000000'::varchar, 'Cobranza de intereses y rendimientos (1)'::varchar),
            (4::smallint, 0::smallint, 0::smallint, '53100000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (4::smallint, 0::smallint, 0::smallint, '59000000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (5::smallint, 0::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '21100000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '11400000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '61000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '62000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '63000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (7::smallint, 0::smallint, 1::smallint, NULL::varchar, 'Pagos de remuneraciones y beneficios sociales'::varchar),
            (8::smallint, 0::smallint, 1::smallint, '74000000000'::varchar, 'Pagos de tributos'::varchar),
            (9::smallint, 0::smallint, 1::smallint, '73000000000'::varchar, 'Pagos de intereses y rendimientos (2)'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '71000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '72000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '79000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '11500000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '21900000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            -- ============ ACTIVIDADES DE INVERSION ============
            (20::smallint, 1::smallint, 0::smallint, '11200000000'::varchar, 'Cobranzas de ventas de valores e inversiones permanentes'::varchar),
            (20::smallint, 1::smallint, 0::smallint, '12200000000'::varchar, 'Cobranzas de ventas de valores e inversiones permanentes'::varchar),
            (21::smallint, 1::smallint, 0::smallint, '12300000000'::varchar, 'Cobranzas de ventas de inmuebles, maquinaria y equipo'::varchar),
            (22::smallint, 1::smallint, 0::smallint, '12500000000'::varchar, 'Cobranzas de ventas de activos intangibles'::varchar),
            (23::smallint, 1::smallint, 0::smallint, '12100000000'::varchar, 'Cobranzas de amortizaciones de prestamos otorgados'::varchar),
            (24::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Cobranza de intereses y rendimientos'::varchar),
            (25::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Cobranza de dividendos'::varchar),
            (26::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (27::smallint, 1::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (28::smallint, 1::smallint, 1::smallint, '11200000000'::varchar, 'Pagos por compras de valores e inversiones permanentes'::varchar),
            (28::smallint, 1::smallint, 1::smallint, '12200000000'::varchar, 'Pagos por compras de valores e inversiones permanentes'::varchar),
            (29::smallint, 1::smallint, 1::smallint, '12300000000'::varchar, 'Pagos por compras de inmuebles, maquinaria y equipo'::varchar),
            (30::smallint, 1::smallint, 1::smallint, '12400000000'::varchar, 'Pagos por obras en curso'::varchar),
            (31::smallint, 1::smallint, 1::smallint, '12500000000'::varchar, 'Pagos por compras y desarrollo de activos intangibles'::varchar),
            (32::smallint, 1::smallint, 1::smallint, NULL::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            -- ============ ACTIVIDADES DE FINANCIACION ============
            (40::smallint, 2::smallint, 0::smallint, '31100000000'::varchar, 'Cobranza de emision de acciones o nuevos aportes'::varchar),
            (41::smallint, 2::smallint, 0::smallint, NULL::varchar, 'Cobranza de recursos obtenidos por emision de valores u obligaciones de corto y largo plazo'::varchar),
            (42::smallint, 2::smallint, 0::smallint, '21200000000'::varchar, 'Cobranza de recursos obtenidos por prestamos de corto y largo plazo'::varchar),
            (42::smallint, 2::smallint, 0::smallint, '22100000000'::varchar, 'Cobranza de recursos obtenidos por prestamos de corto y largo plazo'::varchar),
            (43::smallint, 2::smallint, 0::smallint, '31200000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (43::smallint, 2::smallint, 0::smallint, '31300000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (44::smallint, 2::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (45::smallint, 2::smallint, 1::smallint, '21200000000'::varchar, 'Pagos de amortizacion o cancelacion de valores u otras obligaciones de corto y largo plazo'::varchar),
            (45::smallint, 2::smallint, 1::smallint, '22100000000'::varchar, 'Pagos de amortizacion o cancelacion de valores u otras obligaciones de corto y largo plazo'::varchar),
            (46::smallint, 2::smallint, 1::smallint, NULL::varchar, 'Pagos de intereses y rendimientos'::varchar),
            (47::smallint, 2::smallint, 1::smallint, '31500000000'::varchar, 'Pagos de dividendos y otras distribuciones'::varchar),
            (48::smallint, 2::smallint, 1::smallint, NULL::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            -- ============ CUENTAS DE EFECTIVO (no se imprimen) ============
            (90::smallint, 9::smallint, 0::smallint, '11100000000'::varchar, 'Efectivo y Equivalentes'::varchar)
    )
    SELECT string_agg(DISTINCT r.codigo_cuenta, ', ' ORDER BY r.codigo_cuenta)
    INTO v_missing_codes
    FROM required_config r
    LEFT JOIN public.con_plan_cuentas p
      ON p.company_id = v_company_id
     AND p.code = r.codigo_cuenta
    WHERE r.codigo_cuenta IS NOT NULL
      AND p.account_id IS NULL;

    IF v_missing_codes IS NOT NULL THEN
        RAISE EXCEPTION 'Faltan cuentas requeridas en con_plan_cuentas para company_id=%: %', v_company_id, v_missing_codes;
    END IF;

    DELETE FROM public.con_configuracion_flujo_efectivo
    WHERE company_id = v_company_id;

    WITH required_config(numero_linea, actividad, tipo_linea, codigo_cuenta, descripcion_linea) AS (
        VALUES
            (1::smallint, 0::smallint, 0::smallint, '11300000000'::varchar, 'Cobranzas de ventas de bienes o servicios e ingresos operacionales'::varchar),
            (1::smallint, 0::smallint, 0::smallint, '51000000000'::varchar, 'Cobranzas de ventas de bienes o servicios e ingresos operacionales'::varchar),
            (2::smallint, 0::smallint, 0::smallint, NULL::varchar, 'Cobranzas de regalias, honorarios, comisiones y otros'::varchar),
            (3::smallint, 0::smallint, 0::smallint, '52000000000'::varchar, 'Cobranza de intereses y rendimientos (1)'::varchar),
            (4::smallint, 0::smallint, 0::smallint, '53100000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (4::smallint, 0::smallint, 0::smallint, '59000000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (5::smallint, 0::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '21100000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '11400000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '61000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '62000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (6::smallint, 0::smallint, 1::smallint, '63000000000'::varchar, 'Pagos a proveedores de bienes y servicios'::varchar),
            (7::smallint, 0::smallint, 1::smallint, NULL::varchar, 'Pagos de remuneraciones y beneficios sociales'::varchar),
            (8::smallint, 0::smallint, 1::smallint, '74000000000'::varchar, 'Pagos de tributos'::varchar),
            (9::smallint, 0::smallint, 1::smallint, '73000000000'::varchar, 'Pagos de intereses y rendimientos (2)'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '71000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '72000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '79000000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '11500000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (10::smallint, 0::smallint, 1::smallint, '21900000000'::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (20::smallint, 1::smallint, 0::smallint, '11200000000'::varchar, 'Cobranzas de ventas de valores e inversiones permanentes'::varchar),
            (20::smallint, 1::smallint, 0::smallint, '12200000000'::varchar, 'Cobranzas de ventas de valores e inversiones permanentes'::varchar),
            (21::smallint, 1::smallint, 0::smallint, '12300000000'::varchar, 'Cobranzas de ventas de inmuebles, maquinaria y equipo'::varchar),
            (22::smallint, 1::smallint, 0::smallint, '12500000000'::varchar, 'Cobranzas de ventas de activos intangibles'::varchar),
            (23::smallint, 1::smallint, 0::smallint, '12100000000'::varchar, 'Cobranzas de amortizaciones de prestamos otorgados'::varchar),
            (24::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Cobranza de intereses y rendimientos'::varchar),
            (25::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Cobranza de dividendos'::varchar),
            (26::smallint, 1::smallint, 0::smallint, NULL::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (27::smallint, 1::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (28::smallint, 1::smallint, 1::smallint, '11200000000'::varchar, 'Pagos por compras de valores e inversiones permanentes'::varchar),
            (28::smallint, 1::smallint, 1::smallint, '12200000000'::varchar, 'Pagos por compras de valores e inversiones permanentes'::varchar),
            (29::smallint, 1::smallint, 1::smallint, '12300000000'::varchar, 'Pagos por compras de inmuebles, maquinaria y equipo'::varchar),
            (30::smallint, 1::smallint, 1::smallint, '12400000000'::varchar, 'Pagos por obras en curso'::varchar),
            (31::smallint, 1::smallint, 1::smallint, '12500000000'::varchar, 'Pagos por compras y desarrollo de activos intangibles'::varchar),
            (32::smallint, 1::smallint, 1::smallint, NULL::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (40::smallint, 2::smallint, 0::smallint, '31100000000'::varchar, 'Cobranza de emision de acciones o nuevos aportes'::varchar),
            (41::smallint, 2::smallint, 0::smallint, NULL::varchar, 'Cobranza de recursos obtenidos por emision de valores u obligaciones de corto y largo plazo'::varchar),
            (42::smallint, 2::smallint, 0::smallint, '21200000000'::varchar, 'Cobranza de recursos obtenidos por prestamos de corto y largo plazo'::varchar),
            (42::smallint, 2::smallint, 0::smallint, '22100000000'::varchar, 'Cobranza de recursos obtenidos por prestamos de corto y largo plazo'::varchar),
            (43::smallint, 2::smallint, 0::smallint, '31200000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (43::smallint, 2::smallint, 0::smallint, '31300000000'::varchar, 'Otros cobros de efectivo relativos a la actividad'::varchar),
            (44::smallint, 2::smallint, 2::smallint, NULL::varchar, 'Menos:'::varchar),
            (45::smallint, 2::smallint, 1::smallint, '21200000000'::varchar, 'Pagos de amortizacion o cancelacion de valores u otras obligaciones de corto y largo plazo'::varchar),
            (45::smallint, 2::smallint, 1::smallint, '22100000000'::varchar, 'Pagos de amortizacion o cancelacion de valores u otras obligaciones de corto y largo plazo'::varchar),
            (46::smallint, 2::smallint, 1::smallint, NULL::varchar, 'Pagos de intereses y rendimientos'::varchar),
            (47::smallint, 2::smallint, 1::smallint, '31500000000'::varchar, 'Pagos de dividendos y otras distribuciones'::varchar),
            (48::smallint, 2::smallint, 1::smallint, NULL::varchar, 'Otros pagos de efectivo relativos a la actividad'::varchar),
            (90::smallint, 9::smallint, 0::smallint, '11100000000'::varchar, 'Efectivo y Equivalentes'::varchar)
    )
    INSERT INTO public.con_configuracion_flujo_efectivo (
        company_id,
        numero_linea,
        actividad,
        tipo_linea,
        codigo_cuenta,
        descripcion_linea,
        nivel_indentacion,
        mostrar_subtotal,
        created_at,
        created_by
    )
    SELECT
        v_company_id,
        r.numero_linea,
        r.actividad,
        r.tipo_linea,
        r.codigo_cuenta,
        r.descripcion_linea,
        0,
        false,
        now(),
        'flujo-efectivo-regulatorio'
    FROM required_config r;
END $$;

COMMIT;
