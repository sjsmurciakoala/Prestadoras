-- =============================================================================
-- Reporte: Saldo de clientes segun antiguedad
-- Origen para el dataset web public.rep_saldo_clientes_antiguedad
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_antiguedad(bigint, date, integer, integer, integer);

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_antiguedad(
    p_company_id bigint,
    p_fecha_corte date,
    p_dias_minimos integer,
    p_estado_cliente integer,
    p_ciclo_id integer
)
RETURNS TABLE (
    ciclo_orden integer,
    ciclo_codigo text,
    ciclo_titulo text,
    cuenta text,
    cliente_nombre text,
    direccion text,
    telefono text,
    saldo numeric,
    empresa_nombre text,
    periodo_titulo text,
    fecha_corte date,
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
        GREATEST(COALESCE(p_dias_minimos, 60), 1) AS dias_minimos,
        COALESCE(p_estado_cliente, 0) AS estado_cliente,
        COALESCE(p_ciclo_id, 0) AS ciclo_id
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
detalle_cliente AS (
    SELECT DISTINCT ON (cd.company_id, cd.maestro_cliente_id)
        cd.company_id,
        cd.maestro_cliente_id,
        NULLIF(TRIM(cd.detalle_cliente_direccion), '') AS direccion,
        COALESCE(NULLIF(TRIM(cd.detalle_cliente_telefono), ''), NULLIF(TRIM(cd.detalle_cliente_movil), '')) AS telefono
    FROM public.cliente_detalle cd
    WHERE cd.company_id = p_company_id
    ORDER BY
        cd.company_id,
        cd.maestro_cliente_id,
        COALESCE(cd.estado, true) DESC,
        cd.detalle_cliente_id DESC
),
facturas_antiguas AS (
    SELECT
        f.clientecodigo AS cliente_clave,
        SUM(COALESCE(f.saldototal, 0))::numeric(18, 2) AS saldo
    FROM public.factura f
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND NULLIF(TRIM(f.clientecodigo), '') IS NOT NULL
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(f.saldototal, 0) > 0
      AND f.fechaemision <= p.fecha_corte
      AND f.fechaemision <= (p.fecha_corte - make_interval(days => p.dias_minimos))::date
    GROUP BY f.clientecodigo
),
clientes_base AS (
    SELECT
        CASE
            WHEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0') ~ '^[0-9]+$'
                THEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0')::integer
            ELSE 9999
        END AS ciclo_orden,
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO')::text AS ciclo_codigo,
        ('CICLO :' || COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO'))::text AS ciclo_titulo,
        cm.maestro_cliente_clave::text AS cuenta,
        cm.maestro_cliente_nombre::text AS cliente_nombre,
        COALESCE(dc.direccion, '')::text AS direccion,
        COALESCE(dc.telefono, '')::text AS telefono,
        fa.saldo
    FROM facturas_antiguas fa
    JOIN public.cliente_maestro cm
      ON cm.company_id = p_company_id
     AND cm.maestro_cliente_clave = fa.cliente_clave
    LEFT JOIN detalle_cliente dc
      ON dc.company_id = cm.company_id
     AND dc.maestro_cliente_id = cm.maestro_cliente_id
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    WHERE fa.saldo > 0
      AND (
            p.estado_cliente = 0
            OR (p.estado_cliente = 1 AND cm.estado = true)
            OR (p.estado_cliente = 2 AND cm.estado = false)
          )
      AND (p.ciclo_id = 0 OR cm.ciclos_id = p.ciclo_id)
)
SELECT
    cb.ciclo_orden,
    cb.ciclo_codigo,
    cb.ciclo_titulo,
    cb.cuenta,
    cb.cliente_nombre,
    cb.direccion,
    cb.telefono,
    cb.saldo,
    e.empresa_nombre,
    (
        'Saldo de Clientes mayores a '
        || p.dias_minimos::text
        || ' dias al Periodo '
        || to_char(p.fecha_corte, 'YYYY/MM')
    )::text AS periodo_titulo,
    p.fecha_corte,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM clientes_base cb
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY cb.ciclo_orden, cb.ciclo_codigo, cb.cliente_nombre, cb.cuenta;
$function$;

COMMENT ON FUNCTION public.rep_saldo_clientes_antiguedad(bigint, date, integer, integer, integer) IS
'Clientes con saldo vencido segun dias de antiguedad, agrupados por ciclo y filtrables por estado.';
