DROP FUNCTION IF EXISTS public.rep_estado_resultados(bigint, date, date);

CREATE FUNCTION public.rep_estado_resultados(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE(
    empresa_id bigint,
    empresa_codigo text,
    empresa_nombre text,
    empresa_nombre_legal text,
    empresa_rtn text,
    empresa_email text,
    empresa_telefono text,
    empresa_direccion text,
    seccion_orden integer,
    seccion_nombre text,
    linea_orden integer,
    tipo_linea smallint,
    codigo_cuenta character varying,
    descripcion text,
    descripcion_mostrar text,
    nivel_indentacion smallint,
    mostrar_subtotal boolean,
    monto numeric,
    monto_neto numeric,
    monto_anterior numeric,
    monto_neto_anterior numeric
)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_fecha_desde_anterior date;
    v_fecha_hasta_anterior date;
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;
    IF p_fecha_desde IS NULL OR p_fecha_hasta IS NULL THEN
        RAISE EXCEPTION 'Los parametros p_fecha_desde y p_fecha_hasta son obligatorios.';
    END IF;
    IF p_fecha_hasta < p_fecha_desde THEN
        RAISE EXCEPTION 'La fecha hasta no puede ser menor que la fecha desde.';
    END IF;
    IF NOT EXISTS (
        SELECT 1
        FROM public.cfg_company co
        WHERE co.company_id = p_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%.', p_company_id;
    END IF;

    v_fecha_desde_anterior := (p_fecha_desde - INTERVAL '1 year')::date;
    v_fecha_hasta_anterior := (p_fecha_hasta - INTERVAL '1 year')::date;

    RETURN QUERY
    WITH RECURSIVE plan AS
    (
        SELECT c.account_id, c.parent_account_id, c.code
        FROM public.con_plan_cuentas c
        WHERE c.company_id = p_company_id
          AND COALESCE(upper(c.status), 'ACTIVO') NOT IN ('INACTIVO', 'INACTIVE')
    ),
    empresa AS
    (
        SELECT
            co.company_id AS empresa_id,
            co.code::text AS empresa_codigo,
            co.commercial_name::text AS empresa_nombre,
            co.legal_name::text AS empresa_nombre_legal,
            ec.id_fiscal_valor::text AS empresa_rtn,
            co.email::text AS empresa_email,
            co.phone::text AS empresa_telefono,
            co.address::text AS empresa_direccion
        FROM public.cfg_company co
        LEFT JOIN public.con_empresa_configuracion ec
          ON ec.company_id = co.company_id
        WHERE co.company_id = p_company_id
    ),
    descendants AS
    (
        SELECT p.account_id AS ancestor_id, p.account_id AS descendant_id
        FROM plan p
        UNION ALL
        SELECT d.ancestor_id, c.account_id
        FROM descendants d
        JOIN plan c ON c.parent_account_id = d.descendant_id
    ),
    mov_leaf AS
    (
        SELECT
            d.account_id,
            COALESCE(SUM(d.debit_amount) FILTER (
                WHERE h.poliza_date::date >= p_fecha_desde
                  AND h.poliza_date::date <= p_fecha_hasta
            ), 0)::numeric(18,2) AS deb_actual,
            COALESCE(SUM(d.credit_amount) FILTER (
                WHERE h.poliza_date::date >= p_fecha_desde
                  AND h.poliza_date::date <= p_fecha_hasta
            ), 0)::numeric(18,2) AS cred_actual,
            COALESCE(SUM(d.debit_amount) FILTER (
                WHERE h.poliza_date::date >= v_fecha_desde_anterior
                  AND h.poliza_date::date <= v_fecha_hasta_anterior
            ), 0)::numeric(18,2) AS deb_anterior,
            COALESCE(SUM(d.credit_amount) FILTER (
                WHERE h.poliza_date::date >= v_fecha_desde_anterior
                  AND h.poliza_date::date <= v_fecha_hasta_anterior
            ), 0)::numeric(18,2) AS cred_anterior
        FROM public.con_partida_hdr h
        JOIN public.con_partida_dtl d
          ON d.company_id = h.company_id
         AND d.poliza_id = h.poliza_id
        WHERE h.company_id = p_company_id
          AND h.status = 1
          AND h.type_id <> 27
          AND (
              h.poliza_date::date BETWEEN p_fecha_desde AND p_fecha_hasta
              OR h.poliza_date::date BETWEEN v_fecha_desde_anterior AND v_fecha_hasta_anterior
          )
        GROUP BY d.account_id
    ),
    mov_roll AS
    (
        SELECT
            d.ancestor_id AS account_id,
            COALESCE(SUM(m.deb_actual), 0)::numeric(18,2) AS debitos_periodo,
            COALESCE(SUM(m.cred_actual), 0)::numeric(18,2) AS creditos_periodo,
            COALESCE(SUM(m.deb_anterior), 0)::numeric(18,2) AS debitos_periodo_anterior,
            COALESCE(SUM(m.cred_anterior), 0)::numeric(18,2) AS creditos_periodo_anterior
        FROM descendants d
        LEFT JOIN mov_leaf m ON m.account_id = d.descendant_id
        GROUP BY d.ancestor_id
    ),
    balance AS
    (
        SELECT
            pl.code AS cuenta_codigo,
            mr.debitos_periodo,
            mr.creditos_periodo,
            mr.debitos_periodo_anterior,
            mr.creditos_periodo_anterior
        FROM plan pl
        JOIN mov_roll mr ON mr.account_id = pl.account_id
    ),
    configuracion AS
    (
        SELECT
            r.numero_linea,
            r.tipo_linea,
            r.codigo_cuenta,
            NULLIF(regexp_replace(COALESCE(r.codigo_cuenta, ''), '[./\s-]', '', 'g'), '') AS codigo_normalizado,
            COALESCE(NULLIF(btrim(r.descripcion_linea), ''), COALESCE(r.codigo_cuenta, ''))::text AS descripcion,
            COALESCE(r.nivel_indentacion, 0)::smallint AS nivel_indentacion,
            r.mostrar_subtotal
        FROM public.con_configuracion_linea_resultado r
        WHERE r.company_id = p_company_id
    ),
    base AS
    (
        SELECT
            CASE WHEN cfg.tipo_linea = 0 THEN 10 ELSE 20 END AS seccion_orden,
            CASE WHEN cfg.tipo_linea = 0 THEN 'INGRESOS' ELSE 'COSTOS' END AS seccion_nombre,
            cfg.numero_linea::integer AS linea_orden,
            cfg.tipo_linea::smallint AS tipo_linea,
            cfg.codigo_cuenta,
            cfg.codigo_normalizado,
            cfg.descripcion,
            concat(repeat('  ', GREATEST(cfg.nivel_indentacion::integer, 0)), cfg.descripcion)::text AS descripcion_mostrar,
            cfg.nivel_indentacion,
            cfg.mostrar_subtotal,
            ROUND(
                COALESCE(
                    CASE
                        WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo - bal.debitos_periodo
                        ELSE bal.debitos_periodo - bal.creditos_periodo
                    END,
                    0
                ),
                2
            )::numeric(18,2) AS monto,
            ROUND(
                COALESCE(
                    CASE
                        WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo - bal.debitos_periodo
                        ELSE (bal.debitos_periodo - bal.creditos_periodo) * -1
                    END,
                    0
                ),
                2
            )::numeric(18,2) AS monto_neto,
            ROUND(
                COALESCE(
                    CASE
                        WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo_anterior - bal.debitos_periodo_anterior
                        ELSE bal.debitos_periodo_anterior - bal.creditos_periodo_anterior
                    END,
                    0
                ),
                2
            )::numeric(18,2) AS monto_anterior,
            ROUND(
                COALESCE(
                    CASE
                        WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo_anterior - bal.debitos_periodo_anterior
                        ELSE (bal.debitos_periodo_anterior - bal.creditos_periodo_anterior) * -1
                    END,
                    0
                ),
                2
            )::numeric(18,2) AS monto_neto_anterior
        FROM configuracion cfg
        LEFT JOIN balance bal
          ON cfg.codigo_normalizado IS NOT NULL
         AND regexp_replace(COALESCE(bal.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    ),
    acumulados AS
    (
        SELECT
            COALESCE(SUM(b.monto_neto) FILTER (
                WHERE b.tipo_linea = 0 AND b.nivel_indentacion = 0
            ), 0)::numeric(18,2) AS ingresos_actual,
            COALESCE(SUM(b.monto_neto_anterior) FILTER (
                WHERE b.tipo_linea = 0 AND b.nivel_indentacion = 0
            ), 0)::numeric(18,2) AS ingresos_anterior,
            COALESCE(SUM(b.monto_neto) FILTER (
                WHERE b.tipo_linea IN (1, 2)
                  AND b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') NOT IN ('73000000000', '74000000000')
            ), 0)::numeric(18,2) AS costos_operativos_actual,
            COALESCE(SUM(b.monto_neto_anterior) FILTER (
                WHERE b.tipo_linea IN (1, 2)
                  AND b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') NOT IN ('73000000000', '74000000000')
            ), 0)::numeric(18,2) AS costos_operativos_anterior,
            COALESCE(SUM(b.monto_neto) FILTER (
                WHERE b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') = '73000000000'
            ), 0)::numeric(18,2) AS intereses_actual,
            COALESCE(SUM(b.monto_neto_anterior) FILTER (
                WHERE b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') = '73000000000'
            ), 0)::numeric(18,2) AS intereses_anterior,
            COALESCE(SUM(b.monto_neto) FILTER (
                WHERE b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') = '74000000000'
            ), 0)::numeric(18,2) AS impuesto_actual,
            COALESCE(SUM(b.monto_neto_anterior) FILTER (
                WHERE b.nivel_indentacion = 0
                  AND COALESCE(b.codigo_normalizado, '') = '74000000000'
            ), 0)::numeric(18,2) AS impuesto_anterior
        FROM base b
    ),
    calculadas AS
    (
        SELECT
            10 AS seccion_orden,
            'INGRESOS'::text AS seccion_nombre,
            9 AS linea_orden,
            0::smallint AS tipo_linea,
            NULL::character varying AS codigo_cuenta,
            'TOTAL DE INGRESOS CORRIENTES'::text AS descripcion,
            'TOTAL DE INGRESOS CORRIENTES'::text AS descripcion_mostrar,
            0::smallint AS nivel_indentacion,
            true AS mostrar_subtotal,
            ingresos_actual AS monto,
            ingresos_actual AS monto_neto,
            ingresos_anterior AS monto_anterior,
            ingresos_anterior AS monto_neto_anterior
        FROM acumulados

        UNION ALL

        SELECT
            20,
            'COSTOS'::text,
            26,
            1::smallint,
            NULL::character varying,
            'Utilidad antes de intereses e impuesto sobre la renta'::text,
            'Utilidad antes de intereses e impuesto sobre la renta'::text,
            0::smallint,
            true,
            (ingresos_actual + costos_operativos_actual)::numeric(18,2),
            (ingresos_actual + costos_operativos_actual)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior)::numeric(18,2)
        FROM acumulados

        UNION ALL

        SELECT
            20,
            'COSTOS'::text,
            28,
            1::smallint,
            NULL::character varying,
            'Utilidad antes de impuesto sobre la renta'::text,
            'Utilidad antes de impuesto sobre la renta'::text,
            0::smallint,
            true,
            (ingresos_actual + costos_operativos_actual + intereses_actual)::numeric(18,2),
            (ingresos_actual + costos_operativos_actual + intereses_actual)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior + intereses_anterior)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior + intereses_anterior)::numeric(18,2)
        FROM acumulados

        UNION ALL

        SELECT
            20,
            'COSTOS'::text,
            30,
            1::smallint,
            NULL::character varying,
            'Utilidad (perdida) del ejercicio'::text,
            'Utilidad (perdida) del ejercicio'::text,
            0::smallint,
            true,
            (ingresos_actual + costos_operativos_actual + intereses_actual + impuesto_actual)::numeric(18,2),
            (ingresos_actual + costos_operativos_actual + intereses_actual + impuesto_actual)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior + intereses_anterior + impuesto_anterior)::numeric(18,2),
            (ingresos_anterior + costos_operativos_anterior + intereses_anterior + impuesto_anterior)::numeric(18,2)
        FROM acumulados
    )
    SELECT
        e.empresa_id,
        e.empresa_codigo,
        e.empresa_nombre,
        e.empresa_nombre_legal,
        e.empresa_rtn,
        e.empresa_email,
        e.empresa_telefono,
        e.empresa_direccion,
        q.seccion_orden,
        q.seccion_nombre,
        q.linea_orden,
        q.tipo_linea,
        q.codigo_cuenta,
        q.descripcion,
        q.descripcion_mostrar,
        q.nivel_indentacion,
        q.mostrar_subtotal,
        q.monto,
        q.monto_neto,
        q.monto_anterior,
        q.monto_neto_anterior
    FROM (
        SELECT
            b.seccion_orden,
            b.seccion_nombre,
            b.linea_orden,
            b.tipo_linea,
            b.codigo_cuenta,
            b.descripcion,
            b.descripcion_mostrar,
            b.nivel_indentacion,
            b.mostrar_subtotal,
            b.monto,
            b.monto_neto,
            b.monto_anterior,
            b.monto_neto_anterior
        FROM base b

        UNION ALL

        SELECT
            c.seccion_orden,
            c.seccion_nombre,
            c.linea_orden,
            c.tipo_linea,
            c.codigo_cuenta,
            c.descripcion,
            c.descripcion_mostrar,
            c.nivel_indentacion,
            c.mostrar_subtotal,
            c.monto,
            c.monto_neto,
            c.monto_anterior,
            c.monto_neto_anterior
        FROM calculadas c
    ) q
    CROSS JOIN empresa e
    ORDER BY
        q.seccion_orden,
        q.linea_orden,
        q.codigo_cuenta NULLS LAST;
END;
$function$;
