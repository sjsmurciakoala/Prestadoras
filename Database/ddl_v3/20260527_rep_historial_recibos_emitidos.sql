-- Reporte: Historial de recibos emitidos
-- Fuente principal: pagovariostemp

DROP FUNCTION IF EXISTS public.rep_historial_recibos_emitidos(bigint, date, date, text);

CREATE OR REPLACE FUNCTION public.rep_historial_recibos_emitidos(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_usuario text DEFAULT NULL
)
RETURNS TABLE (
    fila_orden bigint,
    recibo bigint,
    tipo text,
    fecha date,
    cliente_codigo text,
    contribuyente text,
    valor_lempiras numeric(18, 2),
    nulo text,
    usuario_codigo text,
    tipo_servicio text,
    estado_recibo text,
    empresa_nombre text,
    titulo_reporte text,
    fecha_reporte date,
    fecha_reporte_texto text,
    total_emitidos_cantidad integer,
    total_emitidos_valor numeric(18, 2),
    total_nulos_cantidad integer,
    total_nulos_valor numeric(18, 2),
    total_servicios_publicos numeric(18, 2),
    total_miscelaneos numeric(18, 2),
    total_general numeric(18, 2)
)
LANGUAGE sql
STABLE
AS $$
WITH company_info AS (
    SELECT
        COALESCE(
            MAX(NULLIF(TRIM(c.legal_name), '')),
            MAX(NULLIF(TRIM(c.commercial_name), '')),
            'Empresa') AS empresa_nombre
    FROM public.cfg_company c
    WHERE c.company_id = p_company_id
),
base AS (
    SELECT
        p.id,
        COALESCE(p.recibo, 0)::bigint AS recibo,
        p.fecha,
        COALESCE(NULLIF(TRIM(p.cliente_clave), ''), NULLIF(TRIM(p.codigo), ''), '') AS cliente_codigo,
        COALESCE(NULLIF(TRIM(p.nombre), ''), NULLIF(TRIM(p.descripcion), ''), 'SIN CONTRIBUYENTE') AS contribuyente,
        COALESCE(p."valor ", 0)::numeric(18, 2) AS valor_lempiras,
        UPPER(TRIM(COALESCE(NULLIF(p.cajero, ''), NULLIF(p.usuario, ''), 'SIN USUARIO'))) AS usuario_codigo,
        UPPER(TRIM(COALESCE(NULLIF(p.tipo_servicio, ''), NULLIF(p.tipo_factura, ''), 'SERVICIOS PUBLICOS'))) AS tipo_servicio_bruto,
        UPPER(TRIM(COALESCE(p.estado, ''))) AS estado_recibo,
        CASE
            WHEN UPPER(TRIM(COALESCE(p.estado, ''))) IN ('NULO', 'NUL', 'ANULADO', 'ANULADA', 'VOID') THEN true
            WHEN UPPER(TRIM(COALESCE(p.estado, ''))) LIKE '%NUL%' THEN true
            WHEN UPPER(TRIM(COALESCE(p.estado, ''))) LIKE '%ANUL%' THEN true
            ELSE false
        END AS es_nulo
    FROM public.pagovariostemp p
    WHERE p.fecha IS NOT NULL
      AND p.fecha >= COALESCE(p_fecha_desde, CURRENT_DATE)
      AND p.fecha <= COALESCE(p_fecha_hasta, CURRENT_DATE)
      AND (
            NULLIF(TRIM(COALESCE(p_usuario, '')), '') IS NULL
            OR UPPER(TRIM(COALESCE(p.cajero, ''))) = UPPER(TRIM(p_usuario))
            OR UPPER(TRIM(COALESCE(p.usuario, ''))) = UPPER(TRIM(p_usuario))
      )
),
normalized AS (
    SELECT
        b.id,
        b.recibo,
        b.fecha,
        b.cliente_codigo,
        b.contribuyente,
        b.valor_lempiras,
        b.usuario_codigo,
        CASE
            WHEN b.tipo_servicio_bruto LIKE '%MISC%' THEN 'M'
            WHEN b.tipo_servicio_bruto LIKE '%MIS%' THEN 'M'
            WHEN b.tipo_servicio_bruto LIKE 'M%' THEN 'M'
            ELSE COALESCE(NULLIF(LEFT(b.tipo_servicio_bruto, 1), ''), 'S')
        END AS tipo,
        CASE
            WHEN b.tipo_servicio_bruto LIKE '%MISC%' THEN 'MISCELANEOS'
            WHEN b.tipo_servicio_bruto LIKE '%MIS%' THEN 'MISCELANEOS'
            WHEN b.tipo_servicio_bruto LIKE 'M%' THEN 'MISCELANEOS'
            ELSE 'SERVICIOS PUBLICOS'
        END AS tipo_servicio,
        b.estado_recibo,
        b.es_nulo
    FROM base b
),
totals AS (
    SELECT
        COUNT(*) FILTER (WHERE NOT n.es_nulo)::integer AS total_emitidos_cantidad,
        COALESCE(SUM(n.valor_lempiras) FILTER (WHERE NOT n.es_nulo), 0)::numeric(18, 2) AS total_emitidos_valor,
        COUNT(*) FILTER (WHERE n.es_nulo)::integer AS total_nulos_cantidad,
        COALESCE(SUM(n.valor_lempiras) FILTER (WHERE n.es_nulo), 0)::numeric(18, 2) AS total_nulos_valor,
        COALESCE(SUM(n.valor_lempiras) FILTER (WHERE NOT n.es_nulo AND n.tipo = 'S'), 0)::numeric(18, 2) AS total_servicios_publicos,
        COALESCE(SUM(n.valor_lempiras) FILTER (WHERE NOT n.es_nulo AND n.tipo = 'M'), 0)::numeric(18, 2) AS total_miscelaneos,
        COALESCE(SUM(n.valor_lempiras) FILTER (WHERE NOT n.es_nulo), 0)::numeric(18, 2) AS total_general
    FROM normalized n
)
SELECT
    ROW_NUMBER() OVER (ORDER BY n.fecha, n.recibo, n.id)::bigint AS fila_orden,
    n.recibo,
    n.tipo,
    n.fecha,
    n.cliente_codigo,
    n.contribuyente,
    n.valor_lempiras,
    CASE WHEN n.es_nulo THEN 'Nulo' ELSE '' END AS nulo,
    n.usuario_codigo,
    n.tipo_servicio,
    n.estado_recibo,
    ci.empresa_nombre,
    'HISTORIAL DE RECIBOS EMITIDOS DEL '
        || TO_CHAR(COALESCE(p_fecha_desde, CURRENT_DATE), 'DD/MM/YY')
        || ' AL '
        || TO_CHAR(COALESCE(p_fecha_hasta, CURRENT_DATE), 'DD/MM/YY')
        || ' Cajero :'
        || COALESCE(NULLIF(UPPER(TRIM(p_usuario)), ''), 'TODOS') AS titulo_reporte,
    CURRENT_DATE AS fecha_reporte,
    TO_CHAR(CURRENT_DATE, 'DD/MM/YY') AS fecha_reporte_texto,
    t.total_emitidos_cantidad,
    t.total_emitidos_valor,
    t.total_nulos_cantidad,
    t.total_nulos_valor,
    t.total_servicios_publicos,
    t.total_miscelaneos,
    t.total_general
FROM normalized n
CROSS JOIN totals t
CROSS JOIN company_info ci
ORDER BY fila_orden;
$$;

COMMENT ON FUNCTION public.rep_historial_recibos_emitidos(bigint, date, date, text)
IS 'Historial de recibos emitidos por rango de fechas y usuario/cajero, con totales de emitidos, nulos, servicios publicos y miscelaneos.';
