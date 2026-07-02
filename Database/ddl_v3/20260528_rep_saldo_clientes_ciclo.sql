-- =============================================================================
-- Reporte: Saldo de clientes por ciclo
-- Origen para el dataset web public.rep_saldo_clientes_ciclo
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_ciclo(bigint, date, date);

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_ciclo(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE (
    ciclo_orden integer,
    ciclo text,
    saldo_anterior numeric,
    debitos numeric,
    creditos numeric,
    saldo_actual numeric,
    total_usuarios integer,
    con_medidor integer,
    sin_medidor integer,
    activos integer,
    inactivos integer,
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
        COALESCE(p_fecha_desde, date_trunc('month', current_date)::date) AS fecha_desde,
        GREATEST(
            COALESCE(p_fecha_hasta, current_date),
            COALESCE(p_fecha_desde, date_trunc('month', current_date)::date)
        ) AS fecha_hasta
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
        CASE
            WHEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0') ~ '^[0-9]+$'
                THEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0')::integer
            ELSE 9999
        END AS ciclo_orden,
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO')::text AS ciclo,
        COALESCE(cm.maestro_cliente_tiene_medidor, false) AS tiene_medidor,
        COALESCE(cm.estado, false) AS estado_cliente
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    WHERE cm.company_id = p.company_id
      AND NULLIF(TRIM(cm.maestro_cliente_clave), '') IS NOT NULL
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
),
saldos_por_ciclo AS (
    SELECT
        cc.ciclo_orden,
        cc.ciclo,
        SUM(COALESCE(usa.saldo_anterior, 0))::numeric(18, 2) AS saldo_anterior,
        SUM(COALESCE(mp.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(mp.creditos, 0))::numeric(18, 2) AS creditos
    FROM clientes_catalogo cc
    LEFT JOIN ultimo_saldo_anterior usa
      ON usa.cliente_clave = cc.cliente_clave
    LEFT JOIN movimientos_periodo mp
      ON mp.cliente_clave = cc.cliente_clave
    GROUP BY cc.ciclo_orden, cc.ciclo
),
conteos_por_ciclo AS (
    SELECT
        cc.ciclo_orden,
        cc.ciclo,
        COUNT(*)::integer AS total_usuarios,
        SUM(CASE WHEN cc.tiene_medidor THEN 1 ELSE 0 END)::integer AS con_medidor,
        SUM(CASE WHEN NOT cc.tiene_medidor THEN 1 ELSE 0 END)::integer AS sin_medidor,
        SUM(CASE WHEN cc.estado_cliente THEN 1 ELSE 0 END)::integer AS activos,
        SUM(CASE WHEN NOT cc.estado_cliente THEN 1 ELSE 0 END)::integer AS inactivos
    FROM clientes_catalogo cc
    GROUP BY cc.ciclo_orden, cc.ciclo
),
ciclos_base AS (
    SELECT ciclo_orden, ciclo FROM conteos_por_ciclo
    UNION
    SELECT ciclo_orden, ciclo FROM saldos_por_ciclo
)
SELECT
    cb.ciclo_orden,
    cb.ciclo,
    COALESCE(spc.saldo_anterior, 0)::numeric(18, 2) AS saldo_anterior,
    COALESCE(spc.debitos, 0)::numeric(18, 2) AS debitos,
    COALESCE(spc.creditos, 0)::numeric(18, 2) AS creditos,
    (
        COALESCE(spc.saldo_anterior, 0)
        + COALESCE(spc.debitos, 0)
        - COALESCE(spc.creditos, 0)
    )::numeric(18, 2) AS saldo_actual,
    COALESCE(cpc.total_usuarios, 0) AS total_usuarios,
    COALESCE(cpc.con_medidor, 0) AS con_medidor,
    COALESCE(cpc.sin_medidor, 0) AS sin_medidor,
    COALESCE(cpc.activos, 0) AS activos,
    COALESCE(cpc.inactivos, 0) AS inactivos,
    e.empresa_nombre,
    (
        'Saldos de Clientes Totalizados por Ciclos del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || ' al '
        || to_char(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS periodo_titulo,
    p.fecha_desde,
    p.fecha_hasta,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM ciclos_base cb
LEFT JOIN saldos_por_ciclo spc
  ON spc.ciclo_orden = cb.ciclo_orden
 AND spc.ciclo = cb.ciclo
LEFT JOIN conteos_por_ciclo cpc
  ON cpc.ciclo_orden = cb.ciclo_orden
 AND cpc.ciclo = cb.ciclo
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY cb.ciclo_orden, cb.ciclo;
$function$;

COMMENT ON FUNCTION public.rep_saldo_clientes_ciclo(bigint, date, date) IS
'Resumen totalizado por ciclo con saldo anterior, movimientos del periodo y conteos de clientes.';
