-- =============================================================================
-- Reporte: Movimiento por periodo
-- Origen para el dataset web public.rep_movimiento_periodo
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_movimiento_periodo(bigint, date, date);

CREATE OR REPLACE FUNCTION public.rep_movimiento_periodo(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE (
    fila_orden bigint,
    fecha_movimiento date,
    fecha_texto text,
    transaccion_codigo text,
    descripcion text,
    debitos numeric,
    creditos numeric,
    saldo numeric,
    es_saldo_anterior boolean,
    empresa_nombre text,
    periodo_titulo text,
    fecha_desde date,
    fecha_hasta date,
    fecha_reporte date,
    fecha_reporte_texto text
)
LANGUAGE sql
STABLE
AS $function$
WITH parametros AS (
    SELECT
        p_company_id AS company_id,
        COALESCE(p_fecha_desde, current_date) AS fecha_desde,
        COALESCE(p_fecha_hasta, COALESCE(p_fecha_desde, current_date)) AS fecha_hasta
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
ultimo_saldo_cliente AS (
    SELECT DISTINCT ON (ta.cliente_clave)
        ta.cliente_clave,
        COALESCE(ta.saldo, 0)::numeric(18, 2) AS saldo_cliente
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.estado, 'A') = 'A'
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    ORDER BY
        ta.cliente_clave,
        COALESCE(ta.fecha_docu, ta.fecha_registro) DESC,
        COALESCE(ta.fecha_registro, ta.fecha_docu) DESC,
        ta.ide DESC
),
saldo_anterior AS (
    SELECT COALESCE(SUM(usc.saldo_cliente), 0)::numeric(18, 2) AS saldo_anterior
    FROM ultimo_saldo_cliente usc
),
facturacion_raw AS (
    SELECT
        f.fechaemision AS fecha_movimiento,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN '101'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN '102'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN '103'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN '104'
            ELSE '111'
        END AS transaccion_codigo,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN 'Facturacion Agua Potable'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN 'Facturacion Alcantarillado'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN 'Facturacion Ambiental'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN 'Facturacion ERSAP'
            ELSE 'Otros Cargos Facturacion'
        END AS descripcion,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN 10
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN 20
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN 30
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN 40
            ELSE 50
        END AS trans_orden,
        SUM(COALESCE(fd.montovalor, 0))::numeric(18, 2) AS debitos
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) > 0
    GROUP BY 1, 2, 3, 4
),
pagos_raw AS (
    SELECT
        COALESCE(ta.fecha_docu, ta.fecha_registro) AS fecha_movimiento,
        '201'::text AS transaccion_codigo,
        'Registro De Pagos'::text AS descripcion,
        60 AS trans_orden,
        SUM(ABS(COALESCE(ta.creditos, 0)))::numeric(18, 2) AS creditos
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(ta.estado, 'A') = 'A'
      AND ta.tipotransaccion = '201'
      AND COALESCE(ta.creditos, 0) <> 0
    GROUP BY 1
),
descuentos_raw AS (
    SELECT
        f.fechaemision AS fecha_movimiento,
        '202'::text AS transaccion_codigo,
        'Descuento Tercera Edad'::text AS descripcion,
        70 AS trans_orden,
        SUM(ABS(COALESCE(fd.montovalor, 0)))::numeric(18, 2) AS creditos
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) < 0
    GROUP BY 1
),
movimientos AS (
    SELECT
        fr.fecha_movimiento,
        fr.transaccion_codigo,
        fr.descripcion,
        fr.trans_orden,
        fr.debitos,
        0::numeric(18, 2) AS creditos
    FROM facturacion_raw fr

    UNION ALL

    SELECT
        pr.fecha_movimiento,
        pr.transaccion_codigo,
        pr.descripcion,
        pr.trans_orden,
        0::numeric(18, 2) AS debitos,
        pr.creditos
    FROM pagos_raw pr

    UNION ALL

    SELECT
        dr.fecha_movimiento,
        dr.transaccion_codigo,
        dr.descripcion,
        dr.trans_orden,
        0::numeric(18, 2) AS debitos,
        dr.creditos
    FROM descuentos_raw dr
),
movimientos_ordenados AS (
    SELECT
        ROW_NUMBER() OVER (
            ORDER BY
                m.fecha_movimiento,
                m.trans_orden,
                m.transaccion_codigo,
                m.descripcion
        ) AS fila_orden,
        m.fecha_movimiento,
        to_char(m.fecha_movimiento, 'DD/MM/YYYY')::text AS fecha_texto,
        m.transaccion_codigo,
        m.descripcion,
        m.debitos,
        m.creditos,
        m.trans_orden
    FROM movimientos m
),
movimientos_con_saldo AS (
    SELECT
        (mo.fila_orden + 1)::bigint AS fila_orden,
        mo.fecha_movimiento,
        mo.fecha_texto,
        mo.transaccion_codigo,
        mo.descripcion,
        mo.debitos,
        mo.creditos,
        (
            sa.saldo_anterior +
            SUM(mo.debitos - mo.creditos) OVER (
                ORDER BY
                    mo.fecha_movimiento,
                    mo.trans_orden,
                    mo.transaccion_codigo,
                    mo.descripcion,
                    mo.fila_orden
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            )
        )::numeric(18, 2) AS saldo,
        false AS es_saldo_anterior
    FROM movimientos_ordenados mo
    CROSS JOIN saldo_anterior sa
),
salida AS (
    SELECT
        1::bigint AS fila_orden,
        NULL::date AS fecha_movimiento,
        ''::text AS fecha_texto,
        ''::text AS transaccion_codigo,
        'Saldo Anterior'::text AS descripcion,
        0::numeric(18, 2) AS debitos,
        0::numeric(18, 2) AS creditos,
        sa.saldo_anterior AS saldo,
        true AS es_saldo_anterior
    FROM saldo_anterior sa

    UNION ALL

    SELECT
        mcs.fila_orden,
        mcs.fecha_movimiento,
        mcs.fecha_texto,
        mcs.transaccion_codigo,
        mcs.descripcion,
        mcs.debitos,
        mcs.creditos,
        mcs.saldo,
        mcs.es_saldo_anterior
    FROM movimientos_con_saldo mcs
)
SELECT
    s.fila_orden,
    s.fecha_movimiento,
    s.fecha_texto,
    s.transaccion_codigo,
    s.descripcion,
    s.debitos,
    s.creditos,
    s.saldo,
    s.es_saldo_anterior,
    e.empresa_nombre,
    (
        'Registro de Movimientos del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || ' al '
        || to_char(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS periodo_titulo,
    p.fecha_desde,
    p.fecha_hasta,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM salida s
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY s.fila_orden;
$function$;

COMMENT ON FUNCTION public.rep_movimiento_periodo(bigint, date, date) IS
'Movimiento por periodo con saldo anterior, cargos de facturacion, pagos y descuentos, calculado para reporteria web.';
