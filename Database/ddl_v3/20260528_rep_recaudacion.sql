-- =============================================================================
-- Reporte: Informe de recaudación (cobranzas)
-- Origen para el dataset web public.rep_recaudacion
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_recaudacion(bigint, date, date, text);

CREATE OR REPLACE FUNCTION public.rep_recaudacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_medio_pago_codigo text DEFAULT NULL
)
RETURNS TABLE (
    fila_orden bigint,
    fecha date,
    recibo bigint,
    cliente_codigo text,
    cliente_nombre text,
    recuperacion numeric(18, 2),
    ingresos_mes numeric(18, 2),
    total_fila numeric(18, 2),
    medio_pago_nombre text,
    medio_pago_codigo text,
    empresa_nombre text,
    titulo_reporte text,
    fecha_reporte date,
    fecha_reporte_texto text
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
        p.fecha,
        COALESCE(p.recibo, 0)::bigint AS recibo,
        COALESCE(NULLIF(TRIM(p.cliente_clave), ''), NULLIF(TRIM(p.codigo), ''), '') AS cliente_codigo,
        COALESCE(NULLIF(TRIM(p.nombre), ''), NULLIF(TRIM(p.descripcion), ''), 'SIN NOMBRE') AS cliente_nombre,
        COALESCE(p."valor ", 0)::numeric(18, 2) AS valor_pago,
        UPPER(TRIM(COALESCE(p.cod_banco, 'EFECTIVO'))) AS medio_pago_codigo,
        COALESCE(NULLIF(TRIM(rec.descripcion), ''), NULLIF(TRIM(p.cod_banco), ''), 'EFECTIVO') AS medio_pago_nombre,
        f.periodo AS factura_periodo
    FROM public.pagovariostemp p
    LEFT JOIN public.recolectora rec ON rec.codigo = p.cod_banco
    LEFT JOIN public.factura f ON f.numrecibo = p.recibo
    WHERE p.fecha IS NOT NULL
      AND p.fecha >= COALESCE(p_fecha_desde, CURRENT_DATE)
      AND p.fecha <= COALESCE(p_fecha_hasta, CURRENT_DATE)
      AND NOT (
            UPPER(TRIM(COALESCE(p.estado, ''))) IN ('NULO', 'NUL', 'ANULADO', 'ANULADA', 'VOID')
            OR UPPER(TRIM(COALESCE(p.estado, ''))) LIKE '%NUL%'
            OR UPPER(TRIM(COALESCE(p.estado, ''))) LIKE '%ANUL%'
      )
      -- Filtro opcional por Medio de Pago
      AND (
            NULLIF(TRIM(COALESCE(p_medio_pago_codigo, '')), '') IS NULL
            OR UPPER(TRIM(p.cod_banco)) = UPPER(TRIM(p_medio_pago_codigo))
      )
),
calculated AS (
    SELECT
        b.id,
        b.fecha,
        b.recibo,
        b.cliente_codigo,
        b.cliente_nombre,
        -- Si el periodo de la factura coincide con el mes/año del pago, es ingreso del mes. Sino, es recuperacion.
        CASE
            WHEN COALESCE(b.factura_periodo, '') = TO_CHAR(b.fecha, 'YYYYMM') THEN 0.00
            ELSE b.valor_pago
        END::numeric(18, 2) AS recuperacion,
        
        CASE
            WHEN COALESCE(b.factura_periodo, '') = TO_CHAR(b.fecha, 'YYYYMM') THEN b.valor_pago
            ELSE 0.00
        END::numeric(18, 2) AS ingresos_mes,
        
        b.valor_pago::numeric(18, 2) AS total_fila,
        b.medio_pago_nombre,
        b.medio_pago_codigo
    FROM base b
)
SELECT
    ROW_NUMBER() OVER (ORDER BY c.medio_pago_codigo, c.fecha, c.recibo, c.id)::bigint AS fila_orden,
    c.fecha,
    c.recibo,
    c.cliente_codigo,
    c.cliente_nombre,
    c.recuperacion,
    c.ingresos_mes,
    c.total_fila,
    c.medio_pago_nombre,
    c.medio_pago_codigo,
    ci.empresa_nombre,
    'INFORME DE RECAUDACIÓN DEL '
        || TO_CHAR(COALESCE(p_fecha_desde, CURRENT_DATE), 'DD/MM/YYYY')
        || ' AL '
        || TO_CHAR(COALESCE(p_fecha_hasta, CURRENT_DATE), 'DD/MM/YYYY') AS titulo_reporte,
    CURRENT_DATE AS fecha_reporte,
    TO_CHAR(CURRENT_DATE, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM calculated c
CROSS JOIN company_info ci
ORDER BY c.medio_pago_codigo, c.fecha, c.recibo, c.id;
$$;

COMMENT ON FUNCTION public.rep_recaudacion(bigint, date, date, text) IS
'Resumen detallado de ingresos y recuperaciones agrupados por medio de pago para informes de cobranzas.';
