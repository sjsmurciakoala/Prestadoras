-- =============================================================================
-- Reporte: Saldo de clientes por categoria
-- Origen para el reporte DevExpress fisico y dataset web
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date, integer, integer);
DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date, integer);
DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date);

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria(
    p_company_id bigint,
    p_fecha_corte date,
    p_categoria_servicio_id integer DEFAULT 0,
    p_estado_cliente integer DEFAULT 0
)
RETURNS TABLE (
    fila_orden integer,
    row_kind text,
    codigo text,
    categoria text,
    agua_potable numeric,
    alcantarillado_sanitario numeric,
    fondo_fuentes_agua numeric,
    tasa_ersaps numeric,
    convenio_pago numeric,
    otros numeric,
    gestion_legal numeric,
    total numeric,
    empresa_nombre text,
    titulo_reporte text,
    fecha_corte date,
    fecha_corte_texto text,
    fecha_reporte date,
    fecha_reporte_texto text
)
LANGUAGE sql
STABLE
AS $function$
WITH parametros AS (
    SELECT
        p_company_id AS company_id,
        COALESCE(p_fecha_corte, current_date) AS fecha_corte,
        NULLIF(p_categoria_servicio_id, 0) AS categoria_servicio_id,
        NULLIF(p_estado_cliente, 0) AS estado_cliente
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
movimientos AS (
    SELECT
        ta.cliente_clave,
        UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) AS servicio_codigo,
        COALESCE(ta.saldo_detalle, 0)::numeric(18, 2) AS saldo_detalle,
        COALESCE(ta.fecha_docu, ta.fecha_registro, p.fecha_corte) AS fecha_movimiento,
        ta.ide,
        ta.tiene_med
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND ta.cliente_clave IS NOT NULL
      AND COALESCE(ta.estado, 'A') = 'A'
      AND COALESCE(ta.fecha_docu, ta.fecha_registro, p.fecha_corte) <= p.fecha_corte
),
ultimo_movimiento_cliente AS (
    SELECT DISTINCT ON (m.cliente_clave)
        m.cliente_clave,
        m.tiene_med
    FROM movimientos m
    ORDER BY m.cliente_clave, m.fecha_movimiento DESC, m.ide DESC
),
ultimo_saldo_servicio AS (
    SELECT DISTINCT ON (m.cliente_clave, m.servicio_codigo)
        m.cliente_clave,
        m.servicio_codigo,
        m.saldo_detalle
    FROM movimientos m
    WHERE COALESCE(m.saldo_detalle, 0) <> 0
    ORDER BY m.cliente_clave, m.servicio_codigo, m.fecha_movimiento DESC, m.ide DESC
),
clientes_base AS (
    SELECT
        cm.maestro_cliente_clave AS cliente_clave,
        COALESCE(cm.categoria_servicio_id, 0) AS categoria_codigo,
        CASE
            WHEN cm.categoria_servicio_id IS NULL THEN ''
            ELSE cm.categoria_servicio_id::text
        END AS codigo,
        COALESCE(NULLIF(cs.descripcion, ''), 'Sin categoria') AS categoria,
        COALESCE(
            cm.maestro_cliente_tiene_medidor,
            CASE
                WHEN UPPER(COALESCE(umc.tiene_med, '')) = 'S' THEN true
                WHEN UPPER(COALESCE(umc.tiene_med, '')) = 'N' THEN false
                ELSE false
            END,
            false
        ) AS tiene_medidor
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.categoria_servicio cs
      ON cs.categoria_servicio_id = cm.categoria_servicio_id
    LEFT JOIN ultimo_movimiento_cliente umc
      ON umc.cliente_clave = cm.maestro_cliente_clave
    WHERE cm.company_id = p.company_id
      AND (p.categoria_servicio_id IS NULL OR cm.categoria_servicio_id = p.categoria_servicio_id)
      AND (
            p.estado_cliente IS NULL
            OR (p.estado_cliente = 1 AND cm.estado = true)
            OR (p.estado_cliente = 2 AND cm.estado = false)
      )
),
saldos_cliente AS (
    SELECT
        cb.tiene_medidor,
        cb.categoria_codigo,
        cb.codigo,
        cb.categoria,
        cb.cliente_clave,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                    OR us.servicio_codigo LIKE '%AGUA POTABLE%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS agua_potable,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%FUENTE%'
                  OR us.servicio_codigo LIKE '%AMBIENTAL%'
                  OR us.servicio_codigo IN ('FONDO', 'FONDO_FUENTES_AGUA', 'FONDO FUENTES DE AGUA', '3', '03')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS fondo_fuentes_agua,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS tasa_ersaps,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%CONVENIO%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS convenio_pago,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%GESTION%LEGAL%'
                  OR us.servicio_codigo LIKE '%LEGAL%'
                  OR us.servicio_codigo LIKE '%ABOG%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS gestion_legal,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                  OR us.servicio_codigo LIKE '%AGUA POTABLE%'
                  OR us.servicio_codigo IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                  OR us.servicio_codigo LIKE '%FUENTE%'
                  OR us.servicio_codigo LIKE '%AMBIENTAL%'
                  OR us.servicio_codigo IN ('FONDO', 'FONDO_FUENTES_AGUA', 'FONDO FUENTES DE AGUA', '3', '03')
                  OR us.servicio_codigo IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                  OR us.servicio_codigo LIKE '%CONVENIO%'
                  OR us.servicio_codigo LIKE '%GESTION%LEGAL%'
                  OR us.servicio_codigo LIKE '%LEGAL%'
                  OR us.servicio_codigo LIKE '%ABOG%'
                    THEN 0
                ELSE us.saldo_detalle
            END
        )::numeric(18, 2) AS otros
    FROM clientes_base cb
    INNER JOIN ultimo_saldo_servicio us
        ON us.cliente_clave = cb.cliente_clave
    GROUP BY
        cb.tiene_medidor,
        cb.categoria_codigo,
        cb.codigo,
        cb.categoria,
        cb.cliente_clave
),
clientes_con_saldo AS (
    SELECT
        sc.tiene_medidor,
        sc.categoria_codigo,
        sc.codigo,
        sc.categoria,
        sc.agua_potable,
        sc.alcantarillado_sanitario,
        sc.fondo_fuentes_agua,
        sc.tasa_ersaps,
        sc.convenio_pago,
        sc.otros,
        sc.gestion_legal,
        (
            sc.agua_potable +
            sc.alcantarillado_sanitario +
            sc.fondo_fuentes_agua +
            sc.tasa_ersaps +
            sc.convenio_pago +
            sc.otros +
            sc.gestion_legal
        )::numeric(18, 2) AS total
    FROM saldos_cliente sc
    WHERE (
            sc.agua_potable +
            sc.alcantarillado_sanitario +
            sc.fondo_fuentes_agua +
            sc.tasa_ersaps +
            sc.convenio_pago +
            sc.otros +
            sc.gestion_legal
        ) > 0
),
detalle_categoria AS (
    SELECT
        CASE
            WHEN ccs.tiene_medidor THEN 2
            ELSE 1
        END AS grupo_orden,
        CASE
            WHEN ccs.tiene_medidor THEN 'Con Medidor'
            ELSE 'Sin Medicion'
        END AS grupo_nombre,
        ccs.categoria_codigo,
        ccs.codigo,
        ccs.categoria,
        SUM(ccs.agua_potable)::numeric(18, 2) AS agua_potable,
        SUM(ccs.alcantarillado_sanitario)::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(ccs.fondo_fuentes_agua)::numeric(18, 2) AS fondo_fuentes_agua,
        SUM(ccs.tasa_ersaps)::numeric(18, 2) AS tasa_ersaps,
        SUM(ccs.convenio_pago)::numeric(18, 2) AS convenio_pago,
        SUM(ccs.otros)::numeric(18, 2) AS otros,
        SUM(ccs.gestion_legal)::numeric(18, 2) AS gestion_legal,
        SUM(ccs.total)::numeric(18, 2) AS total
    FROM clientes_con_saldo ccs
    GROUP BY
        CASE
            WHEN ccs.tiene_medidor THEN 2
            ELSE 1
        END,
        CASE
            WHEN ccs.tiene_medidor THEN 'Con Medidor'
            ELSE 'Sin Medicion'
        END,
        ccs.categoria_codigo,
        ccs.codigo,
        ccs.categoria
),
grupos AS (
    SELECT *
    FROM (
        VALUES
            (1, 'Sin Medicion'),
            (2, 'Con Medidor')
    ) AS g(grupo_orden, grupo_nombre)
),
detalle_ordenado AS (
    SELECT
        dc.*,
        ROW_NUMBER() OVER (
            PARTITION BY dc.grupo_orden
            ORDER BY
                CASE
                    WHEN dc.categoria_codigo = 0 THEN 9999
                    ELSE dc.categoria_codigo
                END,
                dc.categoria
        ) AS detalle_orden
    FROM detalle_categoria dc
),
filas AS (
    SELECT
        g.grupo_orden * 1000 AS fila_orden,
        'group_header'::text AS row_kind,
        ''::text AS codigo,
        g.grupo_nombre::text AS categoria,
        NULL::numeric AS agua_potable,
        NULL::numeric AS alcantarillado_sanitario,
        NULL::numeric AS fondo_fuentes_agua,
        NULL::numeric AS tasa_ersaps,
        NULL::numeric AS convenio_pago,
        NULL::numeric AS otros,
        NULL::numeric AS gestion_legal,
        NULL::numeric AS total
    FROM grupos g

    UNION ALL

    SELECT
        (d.grupo_orden * 1000) + d.detalle_orden AS fila_orden,
        'detail'::text AS row_kind,
        d.codigo,
        d.categoria,
        d.agua_potable,
        d.alcantarillado_sanitario,
        d.fondo_fuentes_agua,
        d.tasa_ersaps,
        d.convenio_pago,
        d.otros,
        d.gestion_legal,
        d.total
    FROM detalle_ordenado d

    UNION ALL

    SELECT
        (g.grupo_orden * 1000) + 900 AS fila_orden,
        'subtotal'::text AS row_kind,
        ''::text AS codigo,
        'Total'::text AS categoria,
        COALESCE(SUM(d.agua_potable), 0)::numeric(18, 2) AS agua_potable,
        COALESCE(SUM(d.alcantarillado_sanitario), 0)::numeric(18, 2) AS alcantarillado_sanitario,
        COALESCE(SUM(d.fondo_fuentes_agua), 0)::numeric(18, 2) AS fondo_fuentes_agua,
        COALESCE(SUM(d.tasa_ersaps), 0)::numeric(18, 2) AS tasa_ersaps,
        COALESCE(SUM(d.convenio_pago), 0)::numeric(18, 2) AS convenio_pago,
        COALESCE(SUM(d.otros), 0)::numeric(18, 2) AS otros,
        COALESCE(SUM(d.gestion_legal), 0)::numeric(18, 2) AS gestion_legal,
        COALESCE(SUM(d.total), 0)::numeric(18, 2) AS total
    FROM grupos g
    LEFT JOIN detalle_ordenado d
      ON d.grupo_orden = g.grupo_orden
    GROUP BY g.grupo_orden

    UNION ALL

    SELECT
        9000 AS fila_orden,
        'grand_total'::text AS row_kind,
        ''::text AS codigo,
        'Total:'::text AS categoria,
        COALESCE(SUM(d.agua_potable), 0)::numeric(18, 2) AS agua_potable,
        COALESCE(SUM(d.alcantarillado_sanitario), 0)::numeric(18, 2) AS alcantarillado_sanitario,
        COALESCE(SUM(d.fondo_fuentes_agua), 0)::numeric(18, 2) AS fondo_fuentes_agua,
        COALESCE(SUM(d.tasa_ersaps), 0)::numeric(18, 2) AS tasa_ersaps,
        COALESCE(SUM(d.convenio_pago), 0)::numeric(18, 2) AS convenio_pago,
        COALESCE(SUM(d.otros), 0)::numeric(18, 2) AS otros,
        COALESCE(SUM(d.gestion_legal), 0)::numeric(18, 2) AS gestion_legal,
        COALESCE(SUM(d.total), 0)::numeric(18, 2) AS total
    FROM detalle_ordenado d
)
SELECT
    f.fila_orden,
    f.row_kind,
    f.codigo,
    f.categoria,
    f.agua_potable,
    f.alcantarillado_sanitario,
    f.fondo_fuentes_agua,
    f.tasa_ersaps,
    f.convenio_pago,
    f.otros,
    f.gestion_legal,
    f.total,
    e.empresa_nombre,
    (
        'Saldos de Clientes por Categoria desglosado por tipo de servicios al '
        || to_char(p.fecha_corte, 'DD/MM/YYYY')
        || CASE
            WHEN p.categoria_servicio_id IS NULL THEN ''
            ELSE ' - Categoria: ' || COALESCE(
                (
                    SELECT cs.descripcion
                    FROM public.categoria_servicio cs
                    WHERE cs.categoria_servicio_id = p.categoria_servicio_id
                ),
                p.categoria_servicio_id::text)
        END
        || CASE
            WHEN p.estado_cliente = 1 THEN ' - Clientes: Activos'
            WHEN p.estado_cliente = 2 THEN ' - Clientes: Inactivos'
            ELSE ''
        END
    )::text AS titulo_reporte,
    p.fecha_corte,
    to_char(p.fecha_corte, 'DD/MM/YYYY') AS fecha_corte_texto,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM filas f
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY f.fila_orden;
$function$;

COMMENT ON FUNCTION public.rep_saldo_clientes_categoria(bigint, date, integer, integer) IS
'Saldo de clientes por categoria y condicion de medicion, calculado con saldos por servicio desde transaccion_abonado.';
