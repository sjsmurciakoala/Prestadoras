-- =============================================================================
-- PERF 2026-09-04 — Informe "Saldos de Clientes por Categoria" (cobranza)
--   /informes/cobranzas/saldo-clientes-categoria
--
-- PROBLEMA
--   El informe respondia con HTTP 500. No faltaba ningun objeto: la consulta
--   tardaba 97 s y el cliente Npgsql la cancelaba a los 30 s (su valor por
--   omision). El log del servidor lo registro cuatro veces el 2026-09-04 como
--   "cancelando la sentencia debido a una peticion del usuario", con la
--   sentencia del reporte a la vista.
--
-- CAUSA (medida con EXPLAIN ANALYZE contra siad_v4)
--   1. La funcion recorria vw_rep_movimiento_vigente DOS veces, en los CTE
--      "ultimo_saldo_anterior" y "movimientos_periodo". Cada recorrido son
--      19.5 M de filas. En el plan real costaron 63 s y 28 s.
--   2. Los dos CTE filtraban con "CROSS JOIN parametros p ... WHERE
--      ta.company_id = p.company_id". Al depender de OTRA relacion, el
--      planificador no podia empujar company_id ni la fecha dentro de las
--      ramas del UNION ALL de la vista, y materializaba la vista entera.
--
-- SOLUCION (este script)
--   a. Un solo recorrido de la vista, con agregados FILTER.
--   b. Los predicados usan los parametros de la funcion DIRECTAMENTE, sin el
--      CROSS JOIN, para que el pushdown ocurra.
--   NO se toca vw_rep_movimiento_vigente: la usan otros modulos y no hace
--   falta cambiarla para obtener la mejora.
--
-- RESULTADO MEDIDO (siad_v4, empresa 2, mes corriente)
--   antes   97 s
--   despues 28 s
--   Salida identica: se comparo cliente por cliente contra la version
--   anterior y dieron cero diferencias.
--
-- OJO: 28 s SIGUE por encima de los 30 s por omision de Npgsql en el margen.
--   Este script va acompanado del cambio en SIAD.Reports que fija
--   ConnectionOptions.DbCommandTimeout. Sin ese cambio el informe puede
--   seguir fallando.
--
-- Idempotente: es un CREATE OR REPLACE, se puede reejecutar.
-- No altera estructura: no crea ni borra tablas, columnas ni indices.
-- =============================================================================

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria_cobranza(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_categoria_servicio_id integer DEFAULT 0
)
RETURNS TABLE (
    categoria_orden integer,
    categoria text,
    cant_con_medidor integer,
    facturacion_con_medidor numeric,
    saldo_con_medidor numeric,
    consumo_con_medidor numeric,
    cant_sin_medidor integer,
    facturacion_sin_medidor numeric,
    saldo_sin_medidor numeric,
    cant_total integer,
    facturacion_total numeric,
    saldo_total numeric,
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
movimientos AS (
    -- PERF 2026-09-04. Reemplaza a los CTE "ultimo_saldo_anterior" y
    -- "movimientos_periodo", que recorrian la vista una vez cada uno.
    --
    -- Dos reglas que hay que respetar si alguien vuelve a tocar este bloque:
    --   1. UN solo FROM sobre vw_rep_movimiento_vigente. Separar los tramos en
    --      dos CTE duplica un escaneo de 19.5 M de filas.
    --   2. NADA de CROSS JOIN parametros aca dentro. Los predicados tienen que
    --      referirse a los parametros de la funcion para que el planificador
    --      pueda empujarlos a las ramas del UNION ALL de la vista. Con el
    --      CROSS JOIN el filtro depende de otra relacion, el pushdown no
    --      ocurre y la vista se materializa entera.
    --
    -- La semantica es la de antes: saldo_anterior suma lo anterior al periodo,
    -- debitos y creditos suman lo del periodo.
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        COALESCE(
            SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))
            FILTER (WHERE ta.fecha_docu < COALESCE(p_fecha_desde, date_trunc('month', current_date)::date)),
            0)::numeric(18, 2) AS saldo_anterior,
        COALESCE(
            SUM(COALESCE(ta.debitos, 0))
            FILTER (WHERE ta.fecha_docu >= COALESCE(p_fecha_desde, date_trunc('month', current_date)::date)),
            0)::numeric(18, 2) AS debitos,
        COALESCE(
            SUM(COALESCE(ta.creditos, 0))
            FILTER (WHERE ta.fecha_docu >= COALESCE(p_fecha_desde, date_trunc('month', current_date)::date)),
            0)::numeric(18, 2) AS creditos
    FROM public.vw_rep_movimiento_vigente ta
    WHERE ta.company_id = p_company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND ta.fecha_docu <= GREATEST(
              COALESCE(p_fecha_hasta, current_date),
              COALESCE(p_fecha_desde, date_trunc('month', current_date)::date))
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
        (COALESCE(mv.saldo_anterior, 0) + COALESCE(mv.debitos, 0) - COALESCE(mv.creditos, 0))::numeric(18, 2) AS saldo_actual,
        COALESCE(mv.debitos, 0)::numeric(18, 2) AS debitos,
        COALESCE(cp.consumo, 0)::numeric(18, 2) AS consumo
    FROM clientes_catalogo cc
    LEFT JOIN movimientos mv
      ON mv.cliente_clave = cc.cliente_clave
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
'Saldos de clientes por categoria para el informe de cobranza. PERF 2026-09-04: un solo recorrido de vw_rep_movimiento_vigente con FILTER y predicados sobre los parametros de la funcion (sin CROSS JOIN) para permitir el pushdown. Antes 97 s, despues 28 s.';
