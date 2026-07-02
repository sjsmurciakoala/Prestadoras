-- =============================================================================
-- Reporte: Transacciones por periodo
-- Origen para el dataset web public.rep_transacciones_periodo
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_transacciones_periodo(bigint, date, date);

CREATE OR REPLACE FUNCTION public.rep_transacciones_periodo(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE (
    fila_orden integer,
    concepto text,
    agua_potable numeric,
    alcantarillado_sanitario numeric,
    ambiental numeric,
    tasa_ersap numeric,
    convenio numeric,
    gestion_legal numeric,
    otros_cargos numeric,
    total numeric,
    es_total boolean,
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
conceptos AS (
    SELECT *
    FROM (
        VALUES
            (10, 'facturacion', 'Facturación', 1),
            (20, 'notas_debito', '(+) Notas De Debito', 1),
            (30, 'pagos', '(-) Pagos', -1),
            (40, 'descuentos', '(-) Descuentos', -1),
            (50, 'notas_credito', '(-) Notas Crédito', -1),
            (60, 'notas_credito_convenio', '(-) Notas Crédito Convenio', -1)
    ) AS c(fila_orden, concepto_codigo, concepto, factor)
),
movimientos_raw AS (
    SELECT
        CASE
            WHEN COALESCE(fd.montovalor, 0) < 0 THEN 'descuentos'
            ELSE 'facturacion'
        END AS concepto_codigo,
        COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, '') AS rubro_codigo,
        ABS(COALESCE(fd.montovalor, 0)) AS monto
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) <> 0

    UNION ALL

    SELECT
        'pagos' AS concepto_codigo,
        COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.tipotransaccion, ''), ta.descripcion, '') AS rubro_codigo,
        ABS(COALESCE(ta.creditos, 0)) AS monto
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(ta.estado, 'A') = 'A'
      AND ta.tipotransaccion = '201'
      AND COALESCE(ta.creditos, 0) <> 0

    UNION ALL

    SELECT
        CASE
            WHEN UPPER(CONCAT_WS(' ', ncd.servicio_codigo, ncd.descripcion, nc.motivo_detalle)) LIKE '%CONVENIO%'
                THEN 'notas_credito_convenio'
            ELSE 'notas_credito'
        END AS concepto_codigo,
        COALESCE(NULLIF(ncd.servicio_codigo, ''), ncd.descripcion, '') AS rubro_codigo,
        ABS(COALESCE(ncd.monto_total, 0) + COALESCE(ncd.isv_monto, 0)) AS monto
    FROM public.adm_nota_credito nc
    JOIN public.adm_nota_credito_detalle ncd
      ON ncd.nota_credito_id = nc.nota_credito_id
    CROSS JOIN parametros p
    WHERE nc.company_id = p.company_id
      AND nc.fecha_emision::date BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(nc.estado_id, 1) = 1
      AND (COALESCE(ncd.monto_total, 0) + COALESCE(ncd.isv_monto, 0)) <> 0

    UNION ALL

    SELECT
        'notas_debito' AS concepto_codigo,
        COALESCE(NULLIF(ndd.servicio_codigo, ''), ndd.descripcion, '') AS rubro_codigo,
        ABS(COALESCE(ndd.monto_total, 0) + COALESCE(ndd.isv_monto, 0)) AS monto
    FROM public.adm_nota_debito nd
    JOIN public.adm_nota_debito_detalle ndd
      ON ndd.nota_debito_id = nd.nota_debito_id
    CROSS JOIN parametros p
    WHERE nd.company_id = p.company_id
      AND nd.fecha_emision::date BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(nd.estado_id, 1) = 1
      AND (COALESCE(ndd.monto_total, 0) + COALESCE(ndd.isv_monto, 0)) <> 0
),
movimientos AS (
    SELECT
        mr.concepto_codigo,
        CASE
            WHEN UPPER(TRIM(mr.rubro_codigo)) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01') THEN 'agua_potable'
            WHEN UPPER(TRIM(mr.rubro_codigo)) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02') THEN 'alcantarillado_sanitario'
            WHEN UPPER(TRIM(mr.rubro_codigo)) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03') THEN 'ambiental'
            WHEN UPPER(TRIM(mr.rubro_codigo)) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04') THEN 'tasa_ersap'
            WHEN UPPER(TRIM(mr.rubro_codigo)) LIKE '%CONVENIO%' THEN 'convenio'
            WHEN UPPER(TRIM(mr.rubro_codigo)) LIKE '%GESTION%LEGAL%'
              OR UPPER(TRIM(mr.rubro_codigo)) LIKE '%GESTIÓN%LEGAL%'
              OR UPPER(TRIM(mr.rubro_codigo)) LIKE '%LEGAL%'
              OR UPPER(TRIM(mr.rubro_codigo)) LIKE '%ABOG%' THEN 'gestion_legal'
            ELSE 'otros_cargos'
        END AS rubro,
        mr.monto
    FROM movimientos_raw mr
    WHERE COALESCE(mr.monto, 0) <> 0
),
resumen AS (
    SELECT
        concepto_codigo,
        SUM(CASE WHEN rubro = 'agua_potable' THEN monto ELSE 0 END)::numeric(18, 2) AS agua_potable,
        SUM(CASE WHEN rubro = 'alcantarillado_sanitario' THEN monto ELSE 0 END)::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(CASE WHEN rubro = 'ambiental' THEN monto ELSE 0 END)::numeric(18, 2) AS ambiental,
        SUM(CASE WHEN rubro = 'tasa_ersap' THEN monto ELSE 0 END)::numeric(18, 2) AS tasa_ersap,
        SUM(CASE WHEN rubro = 'convenio' THEN monto ELSE 0 END)::numeric(18, 2) AS convenio,
        SUM(CASE WHEN rubro = 'gestion_legal' THEN monto ELSE 0 END)::numeric(18, 2) AS gestion_legal,
        SUM(CASE WHEN rubro = 'otros_cargos' THEN monto ELSE 0 END)::numeric(18, 2) AS otros_cargos
    FROM movimientos
    GROUP BY concepto_codigo
),
filas AS (
    SELECT
        c.fila_orden,
        c.concepto,
        c.factor,
        COALESCE(r.agua_potable, 0)::numeric(18, 2) AS agua_potable,
        COALESCE(r.alcantarillado_sanitario, 0)::numeric(18, 2) AS alcantarillado_sanitario,
        COALESCE(r.ambiental, 0)::numeric(18, 2) AS ambiental,
        COALESCE(r.tasa_ersap, 0)::numeric(18, 2) AS tasa_ersap,
        COALESCE(r.convenio, 0)::numeric(18, 2) AS convenio,
        COALESCE(r.gestion_legal, 0)::numeric(18, 2) AS gestion_legal,
        COALESCE(r.otros_cargos, 0)::numeric(18, 2) AS otros_cargos
    FROM conceptos c
    LEFT JOIN resumen r
      ON r.concepto_codigo = c.concepto_codigo
),
salida AS (
    SELECT
        f.fila_orden,
        f.concepto,
        f.agua_potable,
        f.alcantarillado_sanitario,
        f.ambiental,
        f.tasa_ersap,
        f.convenio,
        f.gestion_legal,
        f.otros_cargos,
        (f.agua_potable + f.alcantarillado_sanitario + f.ambiental + f.tasa_ersap + f.convenio + f.gestion_legal + f.otros_cargos)::numeric(18, 2) AS total,
        false AS es_total
    FROM filas f

    UNION ALL

    SELECT
        70 AS fila_orden,
        'MOVIMIENTOS DEL PERIODO' AS concepto,
        SUM(f.agua_potable * f.factor)::numeric(18, 2) AS agua_potable,
        SUM(f.alcantarillado_sanitario * f.factor)::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(f.ambiental * f.factor)::numeric(18, 2) AS ambiental,
        SUM(f.tasa_ersap * f.factor)::numeric(18, 2) AS tasa_ersap,
        SUM(f.convenio * f.factor)::numeric(18, 2) AS convenio,
        SUM(f.gestion_legal * f.factor)::numeric(18, 2) AS gestion_legal,
        SUM(f.otros_cargos * f.factor)::numeric(18, 2) AS otros_cargos,
        SUM((f.agua_potable + f.alcantarillado_sanitario + f.ambiental + f.tasa_ersap + f.convenio + f.gestion_legal + f.otros_cargos) * f.factor)::numeric(18, 2) AS total,
        true AS es_total
    FROM filas f
)
SELECT
    s.fila_orden,
    s.concepto,
    s.agua_potable,
    s.alcantarillado_sanitario,
    s.ambiental,
    s.tasa_ersap,
    s.convenio,
    s.gestion_legal,
    s.otros_cargos,
    s.total,
    s.es_total,
    e.empresa_nombre,
    (
        'TOTAL CONTROL DE TRANSACCIONES POR PERIODO DEL '
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

COMMENT ON FUNCTION public.rep_transacciones_periodo(bigint, date, date) IS
'Total control de transacciones por periodo para reporteria web.';
