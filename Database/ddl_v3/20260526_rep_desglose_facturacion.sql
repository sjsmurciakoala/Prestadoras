-- =============================================================================
-- Reporte: Desglose de Facturacion
-- Origen para el dataset web public.rep_desglose_facturacion
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_desglose_facturacion(bigint, date, date);

CREATE OR REPLACE FUNCTION public.rep_desglose_facturacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE (
    ciclo_orden integer,
    ciclo text,
    facturacion numeric,
    debitos numeric,
    creditos numeric,
    adulto_mayor numeric,
    pagos_registrados numeric,
    saldo numeric,
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
facturacion_raw AS (
    SELECT
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO') AS ciclo_codigo,
        SUM(CASE WHEN COALESCE(fd.montovalor, 0) > 0 THEN COALESCE(fd.montovalor, 0) ELSE 0 END)::numeric(18, 2) AS facturacion,
        SUM(CASE WHEN COALESCE(fd.montovalor, 0) < 0 THEN ABS(COALESCE(fd.montovalor, 0)) ELSE 0 END)::numeric(18, 2) AS adulto_mayor
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = f.company_id
     AND cm.maestro_cliente_clave = f.clientecodigo
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) <> 0
    GROUP BY 1
),
debitos_raw AS (
    SELECT
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO') AS ciclo_codigo,
        SUM(COALESCE(ndd.monto_total, 0) + COALESCE(ndd.isv_monto, 0))::numeric(18, 2) AS debitos
    FROM public.adm_nota_debito nd
    JOIN public.adm_nota_debito_detalle ndd
      ON ndd.nota_debito_id = nd.nota_debito_id
    LEFT JOIN public.factura f
      ON f.company_id = nd.company_id
     AND f.id = nd.factura_origen_id
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = f.company_id
     AND cm.maestro_cliente_clave = f.clientecodigo
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    WHERE nd.company_id = p.company_id
      AND nd.fecha_emision::date BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(nd.estado_id, 1) = 1
      AND (COALESCE(ndd.monto_total, 0) + COALESCE(ndd.isv_monto, 0)) <> 0
    GROUP BY 1
),
creditos_raw AS (
    SELECT
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO') AS ciclo_codigo,
        SUM(COALESCE(ncd.monto_total, 0) + COALESCE(ncd.isv_monto, 0))::numeric(18, 2) AS creditos
    FROM public.adm_nota_credito nc
    JOIN public.adm_nota_credito_detalle ncd
      ON ncd.nota_credito_id = nc.nota_credito_id
    LEFT JOIN public.factura f
      ON f.company_id = nc.company_id
     AND f.id = nc.factura_origen_id
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = f.company_id
     AND cm.maestro_cliente_clave = f.clientecodigo
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    WHERE nc.company_id = p.company_id
      AND nc.fecha_emision::date BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(nc.estado_id, 1) = 1
      AND (COALESCE(ncd.monto_total, 0) + COALESCE(ncd.isv_monto, 0)) <> 0
    GROUP BY 1
),
pagos_raw AS (
    SELECT
        COALESCE(NULLIF(TRIM(ta.ciclo), ''), NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO') AS ciclo_codigo,
        SUM(ABS(COALESCE(ta.creditos, 0)))::numeric(18, 2) AS pagos_registrados
    FROM public.transaccion_abonado ta
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = ta.company_id
     AND cm.maestro_cliente_clave = ta.cliente_clave
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(ta.estado, 'A') = 'A'
      AND ta.tipotransaccion = '201'
      AND COALESCE(ta.creditos, 0) <> 0
    GROUP BY 1
),
ciclos_catalogo AS (
    SELECT DISTINCT
        TRIM(c.ciclos_codigo) AS ciclo_codigo
    FROM public.ciclos c
    WHERE c.estado
      AND NULLIF(TRIM(c.ciclos_codigo), '') IS NOT NULL
),
ciclos_movimiento AS (
    SELECT ciclo_codigo FROM facturacion_raw
    UNION
    SELECT ciclo_codigo FROM debitos_raw
    UNION
    SELECT ciclo_codigo FROM creditos_raw
    UNION
    SELECT ciclo_codigo FROM pagos_raw
),
ciclos_base AS (
    SELECT ciclo_codigo FROM ciclos_catalogo
    UNION
    SELECT ciclo_codigo FROM ciclos_movimiento
),
resumen AS (
    SELECT
        CASE
            WHEN cb.ciclo_codigo ~ '^[0-9]+$' THEN cb.ciclo_codigo::integer
            ELSE 9999
        END AS ciclo_orden,
        cb.ciclo_codigo AS ciclo,
        COALESCE(fr.facturacion, 0)::numeric(18, 2) AS facturacion,
        COALESCE(dr.debitos, 0)::numeric(18, 2) AS debitos,
        COALESCE(cr.creditos, 0)::numeric(18, 2) AS creditos,
        COALESCE(fr.adulto_mayor, 0)::numeric(18, 2) AS adulto_mayor,
        COALESCE(pr.pagos_registrados, 0)::numeric(18, 2) AS pagos_registrados
    FROM ciclos_base cb
    LEFT JOIN facturacion_raw fr
      ON fr.ciclo_codigo = cb.ciclo_codigo
    LEFT JOIN debitos_raw dr
      ON dr.ciclo_codigo = cb.ciclo_codigo
    LEFT JOIN creditos_raw cr
      ON cr.ciclo_codigo = cb.ciclo_codigo
    LEFT JOIN pagos_raw pr
      ON pr.ciclo_codigo = cb.ciclo_codigo
)
SELECT
    r.ciclo_orden,
    r.ciclo,
    r.facturacion,
    r.debitos,
    r.creditos,
    r.adulto_mayor,
    r.pagos_registrados,
    (r.facturacion + r.debitos - r.creditos - r.adulto_mayor - r.pagos_registrados)::numeric(18, 2) AS saldo,
    e.empresa_nombre,
    (
        'Desglose de Facturacion por Ciclos del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || ' al '
        || to_char(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS periodo_titulo,
    p.fecha_desde,
    p.fecha_hasta,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM resumen r
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY r.ciclo_orden, r.ciclo;
$function$;

COMMENT ON FUNCTION public.rep_desglose_facturacion(bigint, date, date) IS
'Desglose de facturacion por ciclos para reporteria web.';
