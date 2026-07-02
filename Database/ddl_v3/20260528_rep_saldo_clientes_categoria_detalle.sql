-- =============================================================================
-- Reporte: Detalle de saldos de clientes por categoria (cobranzas)
-- Origen para el dataset web public.rep_saldo_clientes_categoria_detalle
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria_detalle(bigint, date, date, integer);

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria_detalle(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_categoria_servicio_id integer DEFAULT 0
)
RETURNS TABLE (
    ciclo_orden integer,
    ciclo text,
    cliente_codigo text,
    cliente_nombre text,
    ruta text,
    saldo_anterior numeric(18, 2),
    debitos numeric(18, 2),
    creditos numeric(18, 2),
    saldo_actual numeric(18, 2),
    empresa_nombre text,
    titulo_reporte text,
    fecha_reporte date,
    fecha_reporte_texto text
)
LANGUAGE sql
STABLE
AS $function$
WITH parametros AS (
    SELECT
        p_company_id AS company_id,
        COALESCE(p_fecha_desde, date_trunc('month', current_date)::date) AS fecha_desde,
        GREATEST(
            COALESCE(p_fecha_hasta, current_date),
            COALESCE(p_fecha_desde, date_trunc('month', current_date)::date)
        ) AS fecha_hasta,
        NULLIF(p_categoria_servicio_id, 0) AS categoria_servicio_id
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
clientes_catalogo AS (
    SELECT
        cm.maestro_cliente_id,
        cm.maestro_cliente_clave::text AS cliente_clave,
        cm.maestro_cliente_nombre::text AS cliente_nombre,
        COALESCE(cm.maestro_cliente_indicativo_ruta, '')::text AS ruta,
        CASE
            WHEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0') ~ '^[0-9]+$'
                THEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0')::integer
            ELSE 9999
        END AS ciclo_orden,
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO')::text AS ciclo,
        COALESCE(cm.categoria_servicio_id, 9999) AS categoria_orden,
        COALESCE(NULLIF(TRIM(cs.descripcion), ''), 'Sin categoria')::text AS categoria
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    LEFT JOIN public.categoria_servicio cs
      ON cs.categoria_servicio_id = cm.categoria_servicio_id
    WHERE cm.company_id = p.company_id
      AND NULLIF(TRIM(cm.maestro_cliente_clave), '') IS NOT NULL
      AND (p.categoria_servicio_id IS NULL OR cm.categoria_servicio_id = p.categoria_servicio_id)
),
ultimo_saldo_anterior AS (
    SELECT DISTINCT ON (ta.cliente_clave)
        ta.cliente_clave::text AS cliente_clave,
        COALESCE(ta.saldo, 0)::numeric(18, 2) AS saldo_anterior
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.estado, 'A') <> 'N'
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    ORDER BY
        ta.cliente_clave,
        COALESCE(ta.fecha_docu, ta.fecha_registro) DESC,
        COALESCE(ta.fecha_registro, ta.fecha_docu) DESC,
        ta.ide DESC
),
movimientos_periodo AS (
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.estado, 'A') <> 'N'
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
    GROUP BY ta.cliente_clave
)
SELECT
    cc.ciclo_orden,
    cc.ciclo,
    cc.cliente_clave AS cliente_codigo,
    cc.cliente_nombre,
    cc.ruta,
    COALESCE(usa.saldo_anterior, 0.00)::numeric(18, 2) AS saldo_anterior,
    COALESCE(mp.debitos, 0.00)::numeric(18, 2) AS debitos,
    COALESCE(mp.creditos, 0.00)::numeric(18, 2) AS creditos,
    (COALESCE(usa.saldo_anterior, 0.00) + COALESCE(mp.debitos, 0.00) - COALESCE(mp.creditos, 0.00))::numeric(18, 2) AS saldo_actual,
    e.empresa_nombre,
    (
        'Saldos de Clientes detallado por Categoria ' 
        || COALESCE(
            CASE 
                WHEN p.categoria_servicio_id IS NULL THEN 'Todas' 
                ELSE (
                    SELECT cs.descripcion 
                    FROM public.categoria_servicio cs 
                    WHERE cs.categoria_servicio_id = p.categoria_servicio_id
                ) 
            END, 
            'Todas'
        ) 
        || ' del ' 
        || TO_CHAR(p.fecha_desde, 'DD/MM/YYYY') 
        || ' al ' 
        || TO_CHAR(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS titulo_reporte,
    CURRENT_DATE AS fecha_reporte,
    TO_CHAR(CURRENT_DATE, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM clientes_catalogo cc
LEFT JOIN ultimo_saldo_anterior usa
  ON usa.cliente_clave = cc.cliente_clave
LEFT JOIN movimientos_periodo mp
  ON mp.cliente_clave = cc.cliente_clave
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY cc.ciclo_orden, cc.ciclo, cc.cliente_clave;
$function$;

COMMENT ON FUNCTION public.rep_saldo_clientes_categoria_detalle(bigint, date, date, integer) IS
'Detalle de saldos de clientes por categoria y ciclo, con saldos anteriores, movimientos y saldos actuales.';
