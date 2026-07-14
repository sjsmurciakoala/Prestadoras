-- =============================================================================
-- Estado de Cambios en el Patrimonio segun Manual de Contabilidad Regulatoria
-- ERSAPS (el manual lo exige en el juego de estados financieros; formato
-- estandar matriz: componente x saldo inicial / aumentos / disminuciones /
-- saldo final, con fila TOTAL).
--
-- No requiere configuracion nueva: los componentes del patrimonio son las
-- lineas de clase 5 ya configuradas en con_configuracion_balance (31.1.xx
-- Capital Social ... 31.5.xx Resultados).
--
-- Los saldos y movimientos salen de public.rep_balance_comprobacion — la misma
-- fuente del Estado de Situacion Financiera — por lo que el saldo final de
-- cada componente cuadra por construccion con la linea correspondiente del ESF
-- a la fecha hasta. Incluye columnas del ejercicio anterior (mismo rango un
-- anio atras) y los datos de la empresa en uso.
-- =============================================================================

BEGIN;

DROP FUNCTION IF EXISTS public.rep_estado_cambios_patrimonio(bigint, date, date);

CREATE FUNCTION public.rep_estado_cambios_patrimonio(
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
    linea_orden integer,
    codigo_cuenta character varying,
    componente text,
    es_total boolean,
    saldo_inicial numeric,
    aumentos numeric,
    disminuciones numeric,
    saldo_final numeric,
    saldo_inicial_anterior numeric,
    aumentos_anterior numeric,
    disminuciones_anterior numeric,
    saldo_final_anterior numeric
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
    IF NOT EXISTS (
        SELECT 1
        FROM public.con_configuracion_balance b
        WHERE b.company_id = p_company_id
          AND b.clase = 5
          AND b.mostrar_en_reporte
    ) THEN
        RAISE EXCEPTION 'No hay componentes de patrimonio (clase=5) en con_configuracion_balance para company_id=%.', p_company_id;
    END IF;

    v_fecha_desde_anterior := (p_fecha_desde - INTERVAL '1 year')::date;
    v_fecha_hasta_anterior := (p_fecha_hasta - INTERVAL '1 year')::date;

    RETURN QUERY
    WITH empresa AS
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
    componentes AS
    (
        SELECT
            b.numero_linea,
            MIN(b.codigo_cuenta) AS codigo_cuenta,
            NULLIF(regexp_replace(COALESCE(MIN(b.codigo_cuenta), ''), '[./\s-]', '', 'g'), '') AS codigo_normalizado,
            COALESCE(
                NULLIF(btrim(MIN(b.descripcion_linea)), ''),
                NULLIF(btrim(MIN(b.descripcion_cuenta)), ''),
                COALESCE(MIN(b.codigo_cuenta), '')
            )::text AS componente
        FROM public.con_configuracion_balance b
        WHERE b.company_id = p_company_id
          AND b.clase = 5
          AND b.mostrar_en_reporte
        GROUP BY b.numero_linea
    ),
    balance_actual AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(p_company_id, p_fecha_desde, p_fecha_hasta, TRUE)
    ),
    balance_anterior AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(p_company_id, v_fecha_desde_anterior, v_fecha_hasta_anterior, TRUE)
    ),
    base AS
    (
        SELECT
            c.numero_linea::integer AS linea_orden,
            c.codigo_cuenta::character varying AS codigo_cuenta,
            c.componente,
            false AS es_total,
            ROUND(COALESCE(bal_act.saldo_anterior, 0) * -1, 2)::numeric(18,2) AS saldo_inicial,
            ROUND(COALESCE(bal_act.creditos_periodo, 0), 2)::numeric(18,2) AS aumentos,
            ROUND(COALESCE(bal_act.debitos_periodo, 0), 2)::numeric(18,2) AS disminuciones,
            ROUND(COALESCE(bal_act.saldo_actual, 0) * -1, 2)::numeric(18,2) AS saldo_final,
            ROUND(COALESCE(bal_ant.saldo_anterior, 0) * -1, 2)::numeric(18,2) AS saldo_inicial_anterior,
            ROUND(COALESCE(bal_ant.creditos_periodo, 0), 2)::numeric(18,2) AS aumentos_anterior,
            ROUND(COALESCE(bal_ant.debitos_periodo, 0), 2)::numeric(18,2) AS disminuciones_anterior,
            ROUND(COALESCE(bal_ant.saldo_actual, 0) * -1, 2)::numeric(18,2) AS saldo_final_anterior
        FROM componentes c
        LEFT JOIN balance_actual bal_act
          ON c.codigo_normalizado IS NOT NULL
         AND regexp_replace(COALESCE(bal_act.cuenta_codigo, ''), '[./\s-]', '', 'g') = c.codigo_normalizado
        LEFT JOIN balance_anterior bal_ant
          ON c.codigo_normalizado IS NOT NULL
         AND regexp_replace(COALESCE(bal_ant.cuenta_codigo, ''), '[./\s-]', '', 'g') = c.codigo_normalizado
    ),
    total AS
    (
        SELECT
            999 AS linea_orden,
            NULL::character varying AS codigo_cuenta,
            'TOTAL DEL PATRIMONIO'::text AS componente,
            true AS es_total,
            COALESCE(SUM(b.saldo_inicial), 0)::numeric(18,2),
            COALESCE(SUM(b.aumentos), 0)::numeric(18,2),
            COALESCE(SUM(b.disminuciones), 0)::numeric(18,2),
            COALESCE(SUM(b.saldo_final), 0)::numeric(18,2),
            COALESCE(SUM(b.saldo_inicial_anterior), 0)::numeric(18,2),
            COALESCE(SUM(b.aumentos_anterior), 0)::numeric(18,2),
            COALESCE(SUM(b.disminuciones_anterior), 0)::numeric(18,2),
            COALESCE(SUM(b.saldo_final_anterior), 0)::numeric(18,2)
        FROM base b
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
        q.linea_orden,
        q.codigo_cuenta,
        q.componente,
        q.es_total,
        q.saldo_inicial,
        q.aumentos,
        q.disminuciones,
        q.saldo_final,
        q.saldo_inicial_anterior,
        q.aumentos_anterior,
        q.disminuciones_anterior,
        q.saldo_final_anterior
    FROM (
        SELECT * FROM base
        UNION ALL
        SELECT * FROM total
    ) q
    CROSS JOIN empresa e
    ORDER BY q.linea_orden, q.codigo_cuenta NULLS LAST;
END;
$function$;

COMMENT ON FUNCTION public.rep_estado_cambios_patrimonio(bigint, date, date)
IS 'Estado de cambios en el patrimonio ERSAPS por empresa: componentes de clase 5 de con_configuracion_balance con saldo inicial, aumentos, disminuciones y saldo final (fuente rep_balance_comprobacion, cuadra con el ESF), comparativo con el ejercicio anterior y datos de la empresa en uso.';

COMMIT;
