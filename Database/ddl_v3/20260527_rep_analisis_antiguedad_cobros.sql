-- =============================================================================
-- Reporte: Analisis de antigüedad de cobros
-- Origen para el dataset web public.rep_analisis_antiguedad_cobros
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_analisis_antiguedad_cobros(bigint, date, integer, text);

CREATE OR REPLACE FUNCTION public.rep_analisis_antiguedad_cobros(
    p_company_id bigint,
    p_fecha_base date,
    p_retroceso_valor integer,
    p_unidad_tiempo text
)
RETURNS TABLE (
    cuenta text,
    cliente_nombre text,
    direccion text,
    corriente numeric(18, 2),
    dias_30_60 numeric(18, 2),
    dias_61_90 numeric(18, 2),
    dias_91_180 numeric(18, 2),
    dias_181_360 numeric(18, 2),
    mas_361 numeric(18, 2),
    total numeric(18, 2),
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
        COALESCE(p_fecha_base, current_date) AS fecha_base,
        GREATEST(COALESCE(p_retroceso_valor, 12), 1) AS retroceso_valor,
        CASE
            WHEN UPPER(TRIM(COALESCE(p_unidad_tiempo, 'MESES'))) = 'ANIOS' THEN 'ANIOS'
            ELSE 'MESES'
        END AS unidad_tiempo
),
rango AS (
    SELECT
        p.company_id,
        p.fecha_base,
        p.retroceso_valor,
        p.unidad_tiempo,
        CASE
            WHEN p.unidad_tiempo = 'ANIOS'
                THEN (p.fecha_base - make_interval(years => p.retroceso_valor))::date
            ELSE (p.fecha_base - make_interval(months => p.retroceso_valor))::date
        END AS fecha_inicio
    FROM parametros p
),
empresa AS (
    SELECT
        r.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM rango r
    LEFT JOIN public.cfg_company c
      ON c.company_id = r.company_id
),
detalle_cliente AS (
    SELECT DISTINCT ON (cd.company_id, cd.maestro_cliente_id)
        cd.company_id,
        cd.maestro_cliente_id,
        NULLIF(TRIM(cd.detalle_cliente_direccion), '') AS direccion
    FROM public.cliente_detalle cd
    CROSS JOIN rango r
    WHERE cd.company_id = r.company_id
    ORDER BY
        cd.company_id,
        cd.maestro_cliente_id,
        COALESCE(cd.estado, true) DESC,
        cd.detalle_cliente_id DESC
),
facturas_filtradas AS (
    SELECT
        f.clientecodigo AS cliente_clave,
        f.fechaemision AS fecha_documento,
        COALESCE(f.saldototal, 0)::numeric(18, 2) AS saldo
    FROM public.factura f
    CROSS JOIN rango r
    WHERE f.company_id = r.company_id
      AND NULLIF(TRIM(f.clientecodigo), '') IS NOT NULL
      AND COALESCE(f.estado, 'A') <> 'N'
      AND f.fechaemision IS NOT NULL
      AND f.fechaemision >= r.fecha_inicio
      AND f.fechaemision <= r.fecha_base
      AND COALESCE(f.saldototal, 0) <> 0
),
facturas_etiquetadas AS (
    SELECT
        ff.cliente_clave,
        ff.saldo,
        GREATEST((r.fecha_base - ff.fecha_documento), 0) AS edad_dias
    FROM facturas_filtradas ff
    CROSS JOIN rango r
),
clientes_agregados AS (
    SELECT
        fe.cliente_clave,
        SUM(CASE WHEN fe.edad_dias < 30 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS corriente,
        SUM(CASE WHEN fe.edad_dias BETWEEN 30 AND 60 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS dias_30_60,
        SUM(CASE WHEN fe.edad_dias BETWEEN 61 AND 90 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS dias_61_90,
        SUM(CASE WHEN fe.edad_dias BETWEEN 91 AND 180 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS dias_91_180,
        SUM(CASE WHEN fe.edad_dias BETWEEN 181 AND 360 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS dias_181_360,
        SUM(CASE WHEN fe.edad_dias > 360 THEN fe.saldo ELSE 0 END)::numeric(18, 2) AS mas_361
    FROM facturas_etiquetadas fe
    GROUP BY fe.cliente_clave
),
clientes_base AS (
    SELECT
        cm.maestro_cliente_clave::text AS cuenta,
        cm.maestro_cliente_nombre::text AS cliente_nombre,
        COALESCE(dc.direccion, '')::text AS direccion,
        ca.corriente,
        ca.dias_30_60,
        ca.dias_61_90,
        ca.dias_91_180,
        ca.dias_181_360,
        ca.mas_361,
        (ca.corriente + ca.dias_30_60 + ca.dias_61_90 + ca.dias_91_180 + ca.dias_181_360 + ca.mas_361)::numeric(18, 2) AS total
    FROM clientes_agregados ca
    CROSS JOIN rango r
    JOIN public.cliente_maestro cm
      ON cm.company_id = r.company_id
     AND cm.maestro_cliente_clave = ca.cliente_clave
    LEFT JOIN detalle_cliente dc
      ON dc.company_id = cm.company_id
     AND dc.maestro_cliente_id = cm.maestro_cliente_id
)
SELECT
    cb.cuenta,
    cb.cliente_nombre,
    cb.direccion,
    cb.corriente,
    cb.dias_30_60,
    cb.dias_61_90,
    cb.dias_91_180,
    cb.dias_181_360,
    cb.mas_361,
    cb.total,
    e.empresa_nombre,
    ('Analisis de Antigüedad de Cobro al ' || to_char(r.fecha_base, 'DD/MM/YYYY'))::text AS periodo_titulo,
    r.fecha_base AS fecha_corte,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM clientes_base cb
CROSS JOIN rango r
CROSS JOIN empresa e
WHERE ROUND(cb.total, 2) <> 0
ORDER BY cb.cuenta, cb.cliente_nombre;
$function$;

COMMENT ON FUNCTION public.rep_analisis_antiguedad_cobros(bigint, date, integer, text) IS
'Analisis de saldos por tramos de antigüedad de cobro, filtrable por una ventana hacia atrás en meses o años desde una fecha base.';
