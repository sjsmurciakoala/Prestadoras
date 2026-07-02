-- =============================================================================
-- Reporte: Saldo de clientes por categoria (cobranzas)
-- Origen para el dataset web public.rep_saldo_clientes_categoria_cobranza
-- =============================================================================

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria_cobranza(bigint, date, date);
DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria_cobranza(bigint, date, date, integer);

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria_cobranza(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_categoria_servicio_id integer DEFAULT 0
)
RETURNS TABLE (
    categoria_orden integer,
    categoria text,
    -- Con medidor
    cant_con_medidor integer,
    facturacion_con_medidor numeric(18, 2),
    saldo_con_medidor numeric(18, 2),
    consumo_con_medidor numeric(18, 2),
    -- Sin medidor
    cant_sin_medidor integer,
    facturacion_sin_medidor numeric(18, 2),
    saldo_sin_medidor numeric(18, 2),
    -- Total acueducto
    cant_total integer,
    facturacion_total numeric(18, 2),
    saldo_total numeric(18, 2),
    -- Metadata
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
        COALESCE(cm.categoria_servicio_id, 9999) AS categoria_orden,
        COALESCE(NULLIF(TRIM(cs.descripcion), ''), 'Sin categoria')::text AS categoria,
        COALESCE(cm.maestro_cliente_tiene_medidor, false) AS tiene_medidor,
        COALESCE(cm.estado, false) AS estado_cliente
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
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
),
consumo_periodo AS (
    SELECT
        hm.clave::text AS cliente_clave,
        SUM(COALESCE(hm.consumo, 0))::numeric(18, 2) AS consumo
    FROM public.historicomedicion hm
    CROSS JOIN parametros p
    WHERE hm.company_id = p.company_id
      AND NULLIF(TRIM(hm.clave), '') IS NOT NULL
      AND (COALESCE(hm.ano, 0)::integer * 12 + COALESCE(hm.mes, 0)::integer) BETWEEN (EXTRACT(year FROM p.fecha_desde)::integer * 12 + EXTRACT(month FROM p.fecha_desde)::integer)
                                                                                  AND (EXTRACT(year FROM p.fecha_hasta)::integer * 12 + EXTRACT(month FROM p.fecha_hasta)::integer)
    GROUP BY hm.clave
),
saldos_cliente AS (
    SELECT
        cc.categoria_orden,
        cc.categoria,
        cc.tiene_medidor,
        -- saldo actual = saldo anterior + debitos - creditos
        (COALESCE(usa.saldo_anterior, 0) + COALESCE(mp.debitos, 0) - COALESCE(mp.creditos, 0))::numeric(18, 2) AS saldo_actual,
        COALESCE(mp.debitos, 0)::numeric(18, 2) AS debitos,
        COALESCE(cp.consumo, 0)::numeric(18, 2) AS consumo
    FROM clientes_catalogo cc
    LEFT JOIN ultimo_saldo_anterior usa
      ON usa.cliente_clave = cc.cliente_clave
    LEFT JOIN movimientos_periodo mp
      ON mp.cliente_clave = cc.cliente_clave
    LEFT JOIN consumo_periodo cp
      ON cp.cliente_clave = cc.cliente_clave
),
resumen_por_categoria AS (
    SELECT
        sc.categoria_orden,
        sc.categoria,
        -- Con medidor
        COALESCE(SUM(CASE WHEN sc.tiene_medidor THEN 1 ELSE 0 END), 0)::integer AS cant_con_medidor,
        COALESCE(SUM(CASE WHEN sc.tiene_medidor THEN sc.debitos ELSE 0 END), 0)::numeric(18, 2) AS facturacion_con_medidor,
        COALESCE(SUM(CASE WHEN sc.tiene_medidor THEN sc.saldo_actual ELSE 0 END), 0)::numeric(18, 2) AS saldo_con_medidor,
        COALESCE(SUM(CASE WHEN sc.tiene_medidor THEN sc.consumo ELSE 0 END), 0)::numeric(18, 2) AS consumo_con_medidor,
        -- Sin medidor
        COALESCE(SUM(CASE WHEN NOT sc.tiene_medidor THEN 1 ELSE 0 END), 0)::integer AS cant_sin_medidor,
        COALESCE(SUM(CASE WHEN NOT sc.tiene_medidor THEN sc.debitos ELSE 0 END), 0)::numeric(18, 2) AS facturacion_sin_medidor,
        COALESCE(SUM(CASE WHEN NOT sc.tiene_medidor THEN sc.saldo_actual ELSE 0 END), 0)::numeric(18, 2) AS saldo_sin_medidor,
        -- Total acueducto
        COUNT(*)::integer AS cant_total,
        COALESCE(SUM(sc.debitos), 0)::numeric(18, 2) AS facturacion_total,
        COALESCE(SUM(sc.saldo_actual), 0)::numeric(18, 2) AS saldo_total
    FROM saldos_cliente sc
    GROUP BY sc.categoria_orden, sc.categoria
)
SELECT
    r.categoria_orden,
    r.categoria,
    r.cant_con_medidor,
    r.facturacion_con_medidor,
    r.saldo_con_medidor,
    r.consumo_con_medidor,
    r.cant_sin_medidor,
    r.facturacion_sin_medidor,
    r.saldo_sin_medidor,
    r.cant_total,
    r.facturacion_total,
    r.saldo_total,
    e.empresa_nombre,
    (
        'Saldos de Clientes por Categoria del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || ' al '
        || to_char(p.fecha_hasta, 'DD/MM/YYYY')
        || COALESCE(
            CASE
                WHEN p.categoria_servicio_id IS NULL THEN ''
                ELSE ' - Categoria: ' || COALESCE(
                    (
                        SELECT NULLIF(TRIM(cs.descripcion), '')
                        FROM public.categoria_servicio cs
                        WHERE cs.categoria_servicio_id = p.categoria_servicio_id
                    ),
                    p.categoria_servicio_id::text)
            END,
            '')
    )::text AS periodo_titulo,
    p.fecha_desde,
    p.fecha_hasta,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM resumen_por_categoria r
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY r.categoria_orden, r.categoria;
$function$;

COMMENT ON FUNCTION public.rep_saldo_clientes_categoria_cobranza(bigint, date, date, integer) IS
'Resumen totalizado por categoria con saldo anterior, movimientos del periodo y conteos de clientes split por medidor.';
