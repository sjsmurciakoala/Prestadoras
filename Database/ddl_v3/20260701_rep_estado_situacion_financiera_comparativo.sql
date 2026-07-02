BEGIN;

DROP FUNCTION IF EXISTS public.rep_estado_situacion_financiera(bigint, date);

CREATE FUNCTION public.rep_estado_situacion_financiera(
    p_company_id bigint,
    p_fecha_corte date)
RETURNS TABLE(
    seccion_orden integer,
    seccion_nombre text,
    clase smallint,
    linea_orden integer,
    codigo_cuenta character varying,
    descripcion text,
    descripcion_mostrar text,
    monto numeric,
    monto_anterior numeric,
    porcentaje_activo numeric,
    mostrar_en_reporte boolean)
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_fecha_anterior date;
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;

    IF p_fecha_corte IS NULL THEN
        RAISE EXCEPTION 'El parametro p_fecha_corte es obligatorio.';
    END IF;

    v_fecha_anterior := (p_fecha_corte - INTERVAL '1 year')::date;

    RETURN QUERY
    WITH balance_actual AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            p_fecha_corte,
            p_fecha_corte,
            TRUE
        )
    ),
    balance_anterior AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            v_fecha_anterior,
            v_fecha_anterior,
            TRUE
        )
    ),
    configuracion AS
    (
        SELECT
            b.numero_linea,
            b.clase,
            b.codigo_cuenta,
            NULLIF(regexp_replace(COALESCE(b.codigo_cuenta, ''), '[./\s-]', '', 'g'), '') AS codigo_normalizado,
            COALESCE(
                NULLIF(btrim(b.descripcion_linea), ''),
                NULLIF(btrim(b.descripcion_cuenta), ''),
                COALESCE(b.codigo_cuenta, '')
            )::text AS descripcion,
            COALESCE(b.porcentaje_activo, 0)::numeric(18,4) AS porcentaje_activo,
            b.mostrar_en_reporte
        FROM public.con_configuracion_balance b
        WHERE b.company_id = p_company_id
          AND b.mostrar_en_reporte
    )
    SELECT
        CASE
            WHEN cfg.clase IN (1, 2) THEN 10
            WHEN cfg.clase IN (3, 4) THEN 20
            WHEN cfg.clase IN (5, 6) THEN 30
            WHEN cfg.clase = 7 THEN 50
            WHEN cfg.clase = 8 THEN 60
            ELSE 99
        END AS seccion_orden,
        CASE
            WHEN cfg.clase IN (1, 2) THEN 'ACTIVO'
            WHEN cfg.clase IN (3, 4) THEN 'PASIVO'
            WHEN cfg.clase IN (5, 6) THEN 'PATRIMONIO'
            WHEN cfg.clase = 7 THEN 'ORDEN'
            WHEN cfg.clase = 8 THEN 'PERCONTRA'
            ELSE 'OTROS'
        END AS seccion_nombre,
        cfg.clase::smallint AS clase,
        cfg.numero_linea::integer AS linea_orden,
        cfg.codigo_cuenta,
        cfg.descripcion,
        cfg.descripcion AS descripcion_mostrar,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.clase IN (1, 2) THEN bal_act.saldo_actual
                    ELSE bal_act.saldo_actual * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.clase IN (1, 2) THEN bal_ant.saldo_actual
                    ELSE bal_ant.saldo_actual * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto_anterior,
        cfg.porcentaje_activo,
        cfg.mostrar_en_reporte
    FROM configuracion cfg
    LEFT JOIN balance_actual bal_act
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal_act.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    LEFT JOIN balance_anterior bal_ant
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal_ant.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    ORDER BY
        seccion_orden,
        cfg.numero_linea,
        cfg.codigo_cuenta;
END;
$$;

COMMENT ON FUNCTION public.rep_estado_situacion_financiera(bigint, date)
IS 'Estado de situacion financiera comparativo por empresa con ejercicio actual y anterior.';

COMMIT;
