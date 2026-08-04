-- Estados fase 2 (docs/PLAN_ESTADOS_FASE1_2026-08-02.md, fases siguientes):
-- los LECTORES SQL de la base dejan de filtrar por la letra legacy de
-- factura.estado y pasan a estado_id (A=1, C=2, N=3, B=4; columna NOT NULL,
-- auditoria letra<->id en 0 desde el saneo del 02-08).
--
-- Objetos: fn_ban_ws_pendientes (lector del WS, validado con golden tests),
-- rep_saldo_clientes_categoria (2 overloads), rep_saldos_agua_potable_ciclo,
-- rep_saldos_alcantarillado_sanitario_ciclo, sp_obtener_cliente_saldo,
-- sp_obtener_cliente_saldo_servicio_detalle (2 overloads) y
-- vw_rep_movimiento_vigente. Equivalencia validada con hashes de salida
-- antes/despues sobre copia09 (identicos).
--
-- Los predicados sobre transaccion_abonado (tabla congelada) CONSERVAN sus
-- letras: son el historico. Escritores (sp_lectura_v3, emision NC/ND) quedan
-- para fase 3 post-deploy.

BEGIN;

CREATE OR REPLACE FUNCTION public.fn_ban_ws_pendientes(p_company_id bigint, p_clave character varying)
 RETURNS TABLE(factura_id bigint, numrecibo integer, numfactura character varying, fechaemision date, fechavence date, categoria_servicio_id integer, con_medicion boolean, detalle_id bigint, codigo character varying, tiposervicio character varying, descripcion character varying, saldo numeric)
 LANGUAGE sql
 STABLE
AS $function$

    SELECT f.id,

           f.numrecibo,

           f.numfactura,

           f.fechaemision,

           f.fechavence,

           f.categoria_servicio_id,

           f.con_medicion,

           d.id,

           d.codigo,

           d.tiposervicio,

           d.descripcion,

           COALESCE(d.montovalor_saldo, d.montovalor, 0)

    FROM public.factura f

    JOIN public.factura_detalle d ON d.factura_id = f.id

    WHERE f.company_id = p_company_id

      AND f.clientecodigo = btrim(p_clave)

      AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada

      -- Solo líneas con saldo POSITIVO (cargos por cobrar). El total que dicta el

      -- banco == suma de estas líneas == lo que el pago puede aplicar: mantener

      -- `> 0` (no `<> 0`) garantiza que consulta y pago coincidan. Una eventual

      -- línea de crédito (saldo < 0) no la cobra el banco; se maneja en el portal.

      AND COALESCE(d.montovalor_saldo, d.montovalor, 0) > 0

    ORDER BY f.fechaemision, f.numrecibo, d.id;

$function$
;

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria(p_company_id bigint, p_fecha_corte date, p_categoria_servicio_id integer DEFAULT 0, p_estado_cliente integer DEFAULT 0)
 RETURNS TABLE(fila_orden integer, row_kind text, codigo text, categoria text, agua_potable numeric, alcantarillado_sanitario numeric, fondo_fuentes_agua numeric, tasa_ersaps numeric, convenio_pago numeric, otros numeric, gestion_legal numeric, total numeric, empresa_nombre text, titulo_reporte text, fecha_corte date, fecha_corte_texto text, fecha_reporte date, fecha_reporte_texto text)
 LANGUAGE sql
 STABLE
AS $function$
WITH parametros AS (
    SELECT
        p_company_id AS company_id,
        COALESCE(p_fecha_corte, current_date) AS fecha_corte,
        NULLIF(p_categoria_servicio_id, 0) AS categoria_servicio_id,
        NULLIF(p_estado_cliente, 0) AS estado_cliente
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
movimientos AS (
    SELECT
        ta.cliente_clave,
        UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) AS servicio_codigo,
        COALESCE(ta.saldo_detalle, 0)::numeric(18, 2) AS saldo_detalle,
        COALESCE(ta.fecha_docu, p.fecha_corte) AS fecha_movimiento,
        ta.ide,
        ta.tiene_med
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND ta.cliente_clave IS NOT NULL
      AND COALESCE(ta.estado, 'A') = 'A'
      AND COALESCE(ta.fecha_docu, p.fecha_corte) <= p.fecha_corte
),
ultimo_movimiento_cliente AS (
    SELECT DISTINCT ON (m.cliente_clave)
        m.cliente_clave,
        m.tiene_med
    FROM movimientos m
    ORDER BY m.cliente_clave, m.fecha_movimiento DESC, m.ide DESC
),
ultimo_saldo_servicio AS (
    -- F4 (2026-07-28): saldo por servicio desde DOCUMENTOS pendientes (lineas
    -- de facturas A/B emitidas hasta el corte). La corrida saldo_detalle
    -- quedaba desactualizada con los pagos del motor y ademas omitia el
    -- residuo migrado de SIMAFI (sus filas llevan saldo_detalle = 0), que
    -- ahora entra al bucket OTROS.
    SELECT
        f.clientecodigo AS cliente_clave,
        UPPER(TRIM(COALESCE(NULLIF(d.tiposervicio, ''), NULLIF(d.descripcion, ''), 'OTROS'))) AS servicio_codigo,
        SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))::numeric(18, 2) AS saldo_detalle
    FROM public.factura f
    JOIN public.factura_detalle d ON d.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
      AND COALESCE(f.fechaemision, p.fecha_corte) <= p.fecha_corte
      AND f.clientecodigo IS NOT NULL
    GROUP BY 1, 2
    HAVING SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)) <> 0
    UNION ALL
    SELECT
        ta.cliente_clave,
        'OTROS'::text,
        SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))::numeric(18, 2)
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND ta.cliente_clave IS NOT NULL
      AND ta.tipotransaccion IN ('SALDO_ANTERIOR', 'SALDO_INICIAL')
      AND COALESCE(ta.fecha_docu, p.fecha_corte) <= p.fecha_corte
    GROUP BY 1
    HAVING SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0)) <> 0
),
clientes_base AS (
    SELECT
        cm.maestro_cliente_clave AS cliente_clave,
        COALESCE(cm.categoria_servicio_id, 0) AS categoria_codigo,
        CASE
            WHEN cm.categoria_servicio_id IS NULL THEN ''
            ELSE cm.categoria_servicio_id::text
        END AS codigo,
        COALESCE(NULLIF(cs.descripcion, ''), 'Sin categoria') AS categoria,
        COALESCE(
            cm.maestro_cliente_tiene_medidor,
            CASE
                WHEN UPPER(COALESCE(umc.tiene_med, '')) = 'S' THEN true
                WHEN UPPER(COALESCE(umc.tiene_med, '')) = 'N' THEN false
                ELSE false
            END,
            false
        ) AS tiene_medidor
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.categoria_servicio cs
      ON cs.categoria_servicio_id = cm.categoria_servicio_id
    LEFT JOIN ultimo_movimiento_cliente umc
      ON umc.cliente_clave = cm.maestro_cliente_clave
    WHERE cm.company_id = p.company_id
      AND (p.categoria_servicio_id IS NULL OR cm.categoria_servicio_id = p.categoria_servicio_id)
      AND (
            p.estado_cliente IS NULL
            OR (p.estado_cliente = 1 AND cm.estado = true)
            OR (p.estado_cliente = 2 AND cm.estado = false)
      )
),
saldos_cliente AS (
    SELECT
        cb.tiene_medidor,
        cb.categoria_codigo,
        cb.codigo,
        cb.categoria,
        cb.cliente_clave,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                    OR us.servicio_codigo LIKE '%AGUA POTABLE%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS agua_potable,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%FUENTE%'
                  OR us.servicio_codigo LIKE '%AMBIENTAL%'
                  OR us.servicio_codigo IN ('FONDO', 'FONDO_FUENTES_AGUA', 'FONDO FUENTES DE AGUA', '3', '03')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS fondo_fuentes_agua,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS tasa_ersaps,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%CONVENIO%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS convenio_pago,
        SUM(
            CASE
                WHEN us.servicio_codigo LIKE '%GESTION%LEGAL%'
                  OR us.servicio_codigo LIKE '%LEGAL%'
                  OR us.servicio_codigo LIKE '%ABOG%'
                    THEN us.saldo_detalle
                ELSE 0
            END
        )::numeric(18, 2) AS gestion_legal,
        SUM(
            CASE
                WHEN us.servicio_codigo IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                  OR us.servicio_codigo LIKE '%AGUA POTABLE%'
                  OR us.servicio_codigo IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                  OR us.servicio_codigo LIKE '%FUENTE%'
                  OR us.servicio_codigo LIKE '%AMBIENTAL%'
                  OR us.servicio_codigo IN ('FONDO', 'FONDO_FUENTES_AGUA', 'FONDO FUENTES DE AGUA', '3', '03')
                  OR us.servicio_codigo IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                  OR us.servicio_codigo LIKE '%CONVENIO%'
                  OR us.servicio_codigo LIKE '%GESTION%LEGAL%'
                  OR us.servicio_codigo LIKE '%LEGAL%'
                  OR us.servicio_codigo LIKE '%ABOG%'
                    THEN 0
                ELSE us.saldo_detalle
            END
        )::numeric(18, 2) AS otros
    FROM clientes_base cb
    INNER JOIN ultimo_saldo_servicio us
        ON us.cliente_clave = cb.cliente_clave
    GROUP BY
        cb.tiene_medidor,
        cb.categoria_codigo,
        cb.codigo,
        cb.categoria,
        cb.cliente_clave
),
clientes_con_saldo AS (
    SELECT
        sc.tiene_medidor,
        sc.categoria_codigo,
        sc.codigo,
        sc.categoria,
        sc.agua_potable,
        sc.alcantarillado_sanitario,
        sc.fondo_fuentes_agua,
        sc.tasa_ersaps,
        sc.convenio_pago,
        sc.otros,
        sc.gestion_legal,
        (
            sc.agua_potable +
            sc.alcantarillado_sanitario +
            sc.fondo_fuentes_agua +
            sc.tasa_ersaps +
            sc.convenio_pago +
            sc.otros +
            sc.gestion_legal
        )::numeric(18, 2) AS total
    FROM saldos_cliente sc
    WHERE (
            sc.agua_potable +
            sc.alcantarillado_sanitario +
            sc.fondo_fuentes_agua +
            sc.tasa_ersaps +
            sc.convenio_pago +
            sc.otros +
            sc.gestion_legal
        ) > 0
),
detalle_categoria AS (
    SELECT
        CASE
            WHEN ccs.tiene_medidor THEN 2
            ELSE 1
        END AS grupo_orden,
        CASE
            WHEN ccs.tiene_medidor THEN 'Con Medidor'
            ELSE 'Sin Medicion'
        END AS grupo_nombre,
        ccs.categoria_codigo,
        ccs.codigo,
        ccs.categoria,
        SUM(ccs.agua_potable)::numeric(18, 2) AS agua_potable,
        SUM(ccs.alcantarillado_sanitario)::numeric(18, 2) AS alcantarillado_sanitario,
        SUM(ccs.fondo_fuentes_agua)::numeric(18, 2) AS fondo_fuentes_agua,
        SUM(ccs.tasa_ersaps)::numeric(18, 2) AS tasa_ersaps,
        SUM(ccs.convenio_pago)::numeric(18, 2) AS convenio_pago,
        SUM(ccs.otros)::numeric(18, 2) AS otros,
        SUM(ccs.gestion_legal)::numeric(18, 2) AS gestion_legal,
        SUM(ccs.total)::numeric(18, 2) AS total
    FROM clientes_con_saldo ccs
    GROUP BY
        CASE
            WHEN ccs.tiene_medidor THEN 2
            ELSE 1
        END,
        CASE
            WHEN ccs.tiene_medidor THEN 'Con Medidor'
            ELSE 'Sin Medicion'
        END,
        ccs.categoria_codigo,
        ccs.codigo,
        ccs.categoria
),
grupos AS (
    SELECT *
    FROM (
        VALUES
            (1, 'Sin Medicion'),
            (2, 'Con Medidor')
    ) AS g(grupo_orden, grupo_nombre)
),
detalle_ordenado AS (
    SELECT
        dc.*,
        ROW_NUMBER() OVER (
            PARTITION BY dc.grupo_orden
            ORDER BY
                CASE
                    WHEN dc.categoria_codigo = 0 THEN 9999
                    ELSE dc.categoria_codigo
                END,
                dc.categoria
        ) AS detalle_orden
    FROM detalle_categoria dc
),
filas AS (
    SELECT
        g.grupo_orden * 1000 AS fila_orden,
        'group_header'::text AS row_kind,
        ''::text AS codigo,
        g.grupo_nombre::text AS categoria,
        NULL::numeric AS agua_potable,
        NULL::numeric AS alcantarillado_sanitario,
        NULL::numeric AS fondo_fuentes_agua,
        NULL::numeric AS tasa_ersaps,
        NULL::numeric AS convenio_pago,
        NULL::numeric AS otros,
        NULL::numeric AS gestion_legal,
        NULL::numeric AS total
    FROM grupos g

    UNION ALL

    SELECT
        (d.grupo_orden * 1000) + d.detalle_orden AS fila_orden,
        'detail'::text AS row_kind,
        d.codigo,
        d.categoria,
        d.agua_potable,
        d.alcantarillado_sanitario,
        d.fondo_fuentes_agua,
        d.tasa_ersaps,
        d.convenio_pago,
        d.otros,
        d.gestion_legal,
        d.total
    FROM detalle_ordenado d

    UNION ALL

    SELECT
        (g.grupo_orden * 1000) + 900 AS fila_orden,
        'subtotal'::text AS row_kind,
        ''::text AS codigo,
        'Total'::text AS categoria,
        COALESCE(SUM(d.agua_potable), 0)::numeric(18, 2) AS agua_potable,
        COALESCE(SUM(d.alcantarillado_sanitario), 0)::numeric(18, 2) AS alcantarillado_sanitario,
        COALESCE(SUM(d.fondo_fuentes_agua), 0)::numeric(18, 2) AS fondo_fuentes_agua,
        COALESCE(SUM(d.tasa_ersaps), 0)::numeric(18, 2) AS tasa_ersaps,
        COALESCE(SUM(d.convenio_pago), 0)::numeric(18, 2) AS convenio_pago,
        COALESCE(SUM(d.otros), 0)::numeric(18, 2) AS otros,
        COALESCE(SUM(d.gestion_legal), 0)::numeric(18, 2) AS gestion_legal,
        COALESCE(SUM(d.total), 0)::numeric(18, 2) AS total
    FROM grupos g
    LEFT JOIN detalle_ordenado d
      ON d.grupo_orden = g.grupo_orden
    GROUP BY g.grupo_orden

    UNION ALL

    SELECT
        9000 AS fila_orden,
        'grand_total'::text AS row_kind,
        ''::text AS codigo,
        'Total:'::text AS categoria,
        COALESCE(SUM(d.agua_potable), 0)::numeric(18, 2) AS agua_potable,
        COALESCE(SUM(d.alcantarillado_sanitario), 0)::numeric(18, 2) AS alcantarillado_sanitario,
        COALESCE(SUM(d.fondo_fuentes_agua), 0)::numeric(18, 2) AS fondo_fuentes_agua,
        COALESCE(SUM(d.tasa_ersaps), 0)::numeric(18, 2) AS tasa_ersaps,
        COALESCE(SUM(d.convenio_pago), 0)::numeric(18, 2) AS convenio_pago,
        COALESCE(SUM(d.otros), 0)::numeric(18, 2) AS otros,
        COALESCE(SUM(d.gestion_legal), 0)::numeric(18, 2) AS gestion_legal,
        COALESCE(SUM(d.total), 0)::numeric(18, 2) AS total
    FROM detalle_ordenado d
)
SELECT
    f.fila_orden,
    f.row_kind,
    f.codigo,
    f.categoria,
    f.agua_potable,
    f.alcantarillado_sanitario,
    f.fondo_fuentes_agua,
    f.tasa_ersaps,
    f.convenio_pago,
    f.otros,
    f.gestion_legal,
    f.total,
    e.empresa_nombre,
    (
        'Saldos de Clientes por Categoria desglosado por tipo de servicios al '
        || to_char(p.fecha_corte, 'DD/MM/YYYY')
        || CASE
            WHEN p.categoria_servicio_id IS NULL THEN ''
            ELSE ' - Categoria: ' || COALESCE(
                (
                    SELECT cs.descripcion
                    FROM public.categoria_servicio cs
                    WHERE cs.categoria_servicio_id = p.categoria_servicio_id
                ),
                p.categoria_servicio_id::text)
        END
        || CASE
            WHEN p.estado_cliente = 1 THEN ' - Clientes: Activos'
            WHEN p.estado_cliente = 2 THEN ' - Clientes: Inactivos'
            ELSE ''
        END
    )::text AS titulo_reporte,
    p.fecha_corte,
    to_char(p.fecha_corte, 'DD/MM/YYYY') AS fecha_corte_texto,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM filas f
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY f.fila_orden;
$function$
;

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_categoria_servicio_id integer DEFAULT 0, p_estado_cliente integer DEFAULT 0)
 RETURNS TABLE(fila_orden integer, codigo text, categoria text, con_medidor_cantidad integer, con_medidor_facturacion_mes numeric, con_medidor_saldo_acumulado numeric, con_medidor_consumo_m3 numeric, sin_medidor_cantidad integer, sin_medidor_facturacion_mes numeric, sin_medidor_saldo_acumulado numeric, total_cantidad integer, total_facturacion_mes numeric, total_saldo_acumulado numeric, empresa_nombre text, titulo_reporte text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
        NULLIF(p_categoria_servicio_id, 0) AS categoria_servicio_id,
        NULLIF(p_estado_cliente, 0) AS estado_cliente
),
empresa AS (
    SELECT
        COALESCE(
            NULLIF(TRIM(c.legal_name), ''),
            NULLIF(TRIM(c.commercial_name), ''),
            NULLIF(TRIM(c.code), ''),
            'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
),
clientes AS (
    SELECT
        cm.maestro_cliente_clave::text AS cliente_clave,
        COALESCE(cm.categoria_servicio_id, 0) AS categoria_codigo,
        COALESCE(NULLIF(TRIM(cs.descripcion), ''), '')::text AS categoria,
        COALESCE(cm.maestro_cliente_tiene_medidor, false) AS tiene_medidor
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.categoria_servicio cs
      ON cs.categoria_servicio_id = cm.categoria_servicio_id
    WHERE cm.company_id = p.company_id
      AND NULLIF(TRIM(cm.maestro_cliente_clave), '') IS NOT NULL
      AND (p.categoria_servicio_id IS NULL OR cm.categoria_servicio_id = p.categoria_servicio_id)
      AND (
            p.estado_cliente IS NULL
            OR (p.estado_cliente = 1 AND cm.estado = true)
            OR (p.estado_cliente = 2 AND cm.estado = false)
      )
),
facturacion_periodo AS (
    SELECT
        f.clientecodigo::text AS cliente_clave,
        SUM(COALESCE(fd.montovalor, 0))::numeric(18, 2) AS facturacion_mes
    FROM public.factura f
    INNER JOIN public.factura_detalle fd
        ON fd.company_id = f.company_id
       AND fd.numrecibo = f.numrecibo
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND NULLIF(TRIM(f.clientecodigo), '') IS NOT NULL
      AND f.estado_id <> 3  -- Anulada
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
    GROUP BY f.clientecodigo
),
saldo_cliente AS (
    SELECT
        f.clientecodigo::text AS cliente_clave,
        SUM(COALESCE(f.saldototal, 0))::numeric(18, 2) AS saldo_acumulado
    FROM public.factura f
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND NULLIF(TRIM(f.clientecodigo), '') IS NOT NULL
      AND f.estado_id <> 3  -- Anulada
      AND f.fechaemision <= p.fecha_hasta
    GROUP BY f.clientecodigo
),
consumo_periodo AS (
    SELECT
        hm.clave::text AS cliente_clave,
        SUM(COALESCE(hm.consumo, 0))::numeric(18, 2) AS consumo_m3
    FROM public.historicomedicion hm
    CROSS JOIN parametros p
    WHERE hm.company_id = p.company_id
      AND NULLIF(TRIM(hm.clave), '') IS NOT NULL
      AND hm.fecha BETWEEN p.fecha_desde AND p.fecha_hasta
    GROUP BY hm.clave
),
resumen_categoria AS (
    SELECT
        c.categoria_codigo,
        c.categoria,
        SUM(CASE WHEN c.tiene_medidor THEN 1 ELSE 0 END)::integer AS con_medidor_cantidad,
        SUM(CASE WHEN c.tiene_medidor THEN COALESCE(fp.facturacion_mes, 0) ELSE 0 END)::numeric(18, 2) AS con_medidor_facturacion_mes,
        SUM(CASE WHEN c.tiene_medidor THEN COALESCE(sc.saldo_acumulado, 0) ELSE 0 END)::numeric(18, 2) AS con_medidor_saldo_acumulado,
        SUM(CASE WHEN c.tiene_medidor THEN COALESCE(cp.consumo_m3, 0) ELSE 0 END)::numeric(18, 2) AS con_medidor_consumo_m3,
        SUM(CASE WHEN NOT c.tiene_medidor THEN 1 ELSE 0 END)::integer AS sin_medidor_cantidad,
        SUM(CASE WHEN NOT c.tiene_medidor THEN COALESCE(fp.facturacion_mes, 0) ELSE 0 END)::numeric(18, 2) AS sin_medidor_facturacion_mes,
        SUM(CASE WHEN NOT c.tiene_medidor THEN COALESCE(sc.saldo_acumulado, 0) ELSE 0 END)::numeric(18, 2) AS sin_medidor_saldo_acumulado
    FROM clientes c
    LEFT JOIN facturacion_periodo fp
      ON fp.cliente_clave = c.cliente_clave
    LEFT JOIN saldo_cliente sc
      ON sc.cliente_clave = c.cliente_clave
    LEFT JOIN consumo_periodo cp
      ON cp.cliente_clave = c.cliente_clave
    GROUP BY c.categoria_codigo, c.categoria
),
detalle AS (
    SELECT
        ROW_NUMBER() OVER (
            ORDER BY rc.categoria_codigo, rc.categoria
        )::integer AS fila_orden,
        rc.categoria_codigo::text AS codigo,
        rc.categoria,
        rc.con_medidor_cantidad,
        rc.con_medidor_facturacion_mes,
        rc.con_medidor_saldo_acumulado,
        rc.con_medidor_consumo_m3,
        rc.sin_medidor_cantidad,
        rc.sin_medidor_facturacion_mes,
        rc.sin_medidor_saldo_acumulado,
        (rc.con_medidor_cantidad + rc.sin_medidor_cantidad)::integer AS total_cantidad,
        (rc.con_medidor_facturacion_mes + rc.sin_medidor_facturacion_mes)::numeric(18, 2) AS total_facturacion_mes,
        (rc.con_medidor_saldo_acumulado + rc.sin_medidor_saldo_acumulado)::numeric(18, 2) AS total_saldo_acumulado
    FROM resumen_categoria rc
)
SELECT
    d.fila_orden,
    d.codigo,
    d.categoria,
    d.con_medidor_cantidad,
    d.con_medidor_facturacion_mes,
    d.con_medidor_saldo_acumulado,
    d.con_medidor_consumo_m3,
    d.sin_medidor_cantidad,
    d.sin_medidor_facturacion_mes,
    d.sin_medidor_saldo_acumulado,
    d.total_cantidad,
    d.total_facturacion_mes,
    d.total_saldo_acumulado,
    e.empresa_nombre,
    (
        'Saldos de Clientes por Categoria del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || ' al '
        || to_char(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS titulo_reporte,
    p.fecha_desde,
    p.fecha_hasta,
    current_date AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM detalle d
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY d.fila_orden;
$function$
;

CREATE OR REPLACE FUNCTION public.rep_saldos_agua_potable_ciclo(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_ciclo_id bigint DEFAULT 0)
 RETURNS TABLE(ciclo_orden integer, ciclo text, saldo_anterior numeric, debitos numeric, creditos numeric, saldo_actual numeric, total_usuarios integer, con_medidor integer, sin_medidor integer, activos integer, inactivos integer, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
      AND (p_ciclo_id = 0 OR cm.ciclos_id = p_ciclo_id)
),
movimientos_periodo AS (
    -- F4: movimientos vigentes del servicio en el periodo (el <> 'N' contaba
    -- pagos anulados). Los abonos del motor no llevan tipo_servicio, por lo
    -- que el neto por servicio se completa con los documentos (abajo).
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND ta.fecha_docu BETWEEN p.fecha_desde AND p.fecha_hasta
      AND (
          UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
          OR UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) LIKE '%AGUA POTABLE%'
      )
    GROUP BY ta.cliente_clave
),
ultimo_saldo_anterior AS (
    -- F4 (2026-07-28): derivado de los DOCUMENTOS pendientes del servicio
    -- (lineas de facturas A/B) para que anterior + movimientos = saldo actual
    -- real del servicio. La corrida saldo_detalle quedaba desactualizada con
    -- los pagos del motor.
    SELECT
        l.cliente_clave,
        (l.saldo_docs - COALESCE(mp.debitos, 0) + COALESCE(mp.creditos, 0))::numeric(18, 2) AS saldo_anterior
    FROM (
        SELECT
            f.clientecodigo::text AS cliente_clave,
            SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))::numeric(18, 2) AS saldo_docs
        FROM public.factura f
        JOIN public.factura_detalle d ON d.factura_id = f.id
        CROSS JOIN parametros p
        WHERE f.company_id = p.company_id
          AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
          AND (
          UPPER(TRIM(COALESCE(NULLIF(d.tiposervicio, ''), NULLIF(d.descripcion, ''), 'OTROS'))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
          OR UPPER(TRIM(COALESCE(NULLIF(d.tiposervicio, ''), NULLIF(d.descripcion, ''), 'OTROS'))) LIKE '%AGUA POTABLE%'
      )
        GROUP BY f.clientecodigo
    ) l
    LEFT JOIN movimientos_periodo mp ON mp.cliente_clave = l.cliente_clave
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
        'Saldos de Agua Potable por Ciclos del '
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
$function$
;

CREATE OR REPLACE FUNCTION public.rep_saldos_alcantarillado_sanitario_ciclo(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_ciclo_id bigint DEFAULT 0)
 RETURNS TABLE(ciclo_orden integer, ciclo text, saldo_anterior numeric, debitos numeric, creditos numeric, saldo_actual numeric, total_usuarios integer, con_medidor integer, sin_medidor integer, activos integer, inactivos integer, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
      AND (p_ciclo_id = 0 OR cm.ciclos_id = p_ciclo_id)
),
movimientos_periodo AS (
    -- F4: movimientos vigentes del servicio en el periodo (el <> 'N' contaba
    -- pagos anulados). Los abonos del motor no llevan tipo_servicio, por lo
    -- que el neto por servicio se completa con los documentos (abajo).
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND ta.fecha_docu BETWEEN p.fecha_desde AND p.fecha_hasta
      AND (
          UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
          OR UPPER(TRIM(COALESCE(NULLIF(ta.tipo_servicio, ''), NULLIF(ta.tasa, ''), NULLIF(ta.descripcion, ''), 'OTROS'))) LIKE '%ALCANTARILLADO%'
      )
    GROUP BY ta.cliente_clave
),
ultimo_saldo_anterior AS (
    -- F4 (2026-07-28): derivado de los DOCUMENTOS pendientes del servicio
    -- (lineas de facturas A/B) para que anterior + movimientos = saldo actual
    -- real del servicio. La corrida saldo_detalle quedaba desactualizada con
    -- los pagos del motor.
    SELECT
        l.cliente_clave,
        (l.saldo_docs - COALESCE(mp.debitos, 0) + COALESCE(mp.creditos, 0))::numeric(18, 2) AS saldo_anterior
    FROM (
        SELECT
            f.clientecodigo::text AS cliente_clave,
            SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))::numeric(18, 2) AS saldo_docs
        FROM public.factura f
        JOIN public.factura_detalle d ON d.factura_id = f.id
        CROSS JOIN parametros p
        WHERE f.company_id = p.company_id
          AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
          AND (
          UPPER(TRIM(COALESCE(NULLIF(d.tiposervicio, ''), NULLIF(d.descripcion, ''), 'OTROS'))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
          OR UPPER(TRIM(COALESCE(NULLIF(d.tiposervicio, ''), NULLIF(d.descripcion, ''), 'OTROS'))) LIKE '%ALCANTARILLADO%'
      )
        GROUP BY f.clientecodigo
    ) l
    LEFT JOIN movimientos_periodo mp ON mp.cliente_clave = l.cliente_clave
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
        'Saldos de Alcantarillado Sanitario por Ciclos del '
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
$function$
;

CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(p_company_id bigint, pcodigocliente character varying)
 RETURNS TABLE(saldo_actual numeric)
 LANGUAGE plpgsql
 STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT
      COALESCE((
          SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = pcodigocliente
            AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
      ), 0)
    + COALESCE((
          SELECT SUM(dt.saldo_cuota)
          FROM public.cln_plan_pago_dtl dt
          JOIN public.cln_plan_pago_hdr h ON h.id = dt.idhdr
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = h.clienteid
                                        AND cm.company_id = h.company_id
          WHERE h.company_id = p_company_id
            AND h.estado_id  = 1
            AND cm.maestro_cliente_clave = pcodigocliente
            AND dt.estado_id IN (1, 4)
      ), 0)
    + COALESCE((
          SELECT SUM(nd.saldo_pendiente)
          FROM public.adm_nota_debito nd
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = nd.cliente_id
                                        AND cm.company_id = nd.company_id
          WHERE nd.company_id = p_company_id
            AND cm.maestro_cliente_clave = pcodigocliente
            AND nd.estado_id IN (1, 2)
            AND nd.saldo_pendiente > 0
      ), 0);
END
$function$
;

CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(pcodigocliente character varying, servicio_codigo character varying)
 RETURNS numeric
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_valor numeric(18,2);
BEGIN
    SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
      INTO v_valor
      FROM public.factura f
      JOIN public.factura_detalle d ON d.factura_id = f.id
     WHERE f.clientecodigo = pcodigocliente
       AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
       AND d.tiposervicio  = servicio_codigo;

    RETURN COALESCE(v_valor, 0);
END
$function$
;

CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(p_company_id bigint, pcodigocliente character varying, servicio_codigo character varying)
 RETURNS numeric
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_valor numeric(18,2);
BEGIN
    SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
      INTO v_valor
      FROM public.factura f
      JOIN public.factura_detalle d ON d.factura_id = f.id
     WHERE f.company_id    = p_company_id
       AND f.clientecodigo = pcodigocliente
       AND f.estado_id IN (1, 4)  -- Activa/ParcialmenteAbonada
       AND d.tiposervicio  = servicio_codigo;

    RETURN COALESCE(v_valor, 0);
END
$function$
;

CREATE OR REPLACE VIEW public.vw_rep_movimiento_vigente AS
 SELECT f.company_id,
    f.clientecodigo AS cliente_clave,
    f.fechaemision AS fecha_docu,
    f.fechaemision AS fecha_registro,
    COALESCE(NULLIF(TRIM(BOTH FROM d.codigo), ''::text), d.tiposervicio::text, 'CARGO'::text)::character varying AS tipotransaccion,
    d.tiposervicio AS tipo_servicio,
    NULL::character varying AS tasa,
    d.descripcion,
        CASE
            WHEN COALESCE(d.montovalor, 0::numeric) >= 0::numeric THEN d.montovalor
            ELSE 0::numeric
        END AS debitos,
        CASE
            WHEN COALESCE(d.montovalor, 0::numeric) < 0::numeric THEN - d.montovalor
            ELSE 0::numeric
        END AS creditos,
    NULL::character varying AS ciclo
   FROM factura f
     JOIN factura_detalle d ON d.factura_id = f.id
  WHERE f.estado_id <> 3  -- Anulada
UNION ALL
 SELECT p.company_id,
    p.cliente_clave,
    p.fecha AS fecha_docu,
    p.fecha AS fecha_registro,
        CASE
            WHEN p.tipo_transaccion_id = ANY (ARRAY[2, 3, 4]) THEN '201'::text
            ELSE '205'::text
        END::character varying AS tipotransaccion,
    d.tiposervicio AS tipo_servicio,
    NULL::character varying AS tasa,
    NULL::character varying AS descripcion,
    0::numeric AS debitos,
    a.monto_aplicado AS creditos,
    NULL::character varying AS ciclo
   FROM adm_pago p
     JOIN adm_pago_aplicacion a ON a.company_id = p.company_id AND a.pago_id = p.pago_id
     LEFT JOIN factura_detalle d ON d.id = a.factura_detalle_id
  WHERE p.estado_id = 1
UNION ALL
 SELECT p.company_id,
    p.cliente_clave,
    p.fecha AS fecha_docu,
    p.fecha AS fecha_registro,
        CASE
            WHEN p.tipo_transaccion_id = ANY (ARRAY[2, 3, 4]) THEN '201'::text
            ELSE '205'::text
        END::character varying AS tipotransaccion,
    NULL::character varying AS tipo_servicio,
    NULL::character varying AS tasa,
    NULL::character varying AS descripcion,
    0::numeric AS debitos,
    p.monto_total - COALESCE(ap.aplicado, 0::numeric) AS creditos,
    NULL::character varying AS ciclo
   FROM adm_pago p
     LEFT JOIN ( SELECT adm_pago_aplicacion.company_id,
            adm_pago_aplicacion.pago_id,
            sum(adm_pago_aplicacion.monto_aplicado) AS aplicado
           FROM adm_pago_aplicacion
          GROUP BY adm_pago_aplicacion.company_id, adm_pago_aplicacion.pago_id) ap ON ap.company_id = p.company_id AND ap.pago_id = p.pago_id
  WHERE p.estado_id = 1 AND (p.monto_total - COALESCE(ap.aplicado, 0::numeric)) > 0::numeric
UNION ALL
 SELECT nd.company_id,
    cm.maestro_cliente_clave AS cliente_clave,
    nd.fecha_emision::date AS fecha_docu,
    nd.fecha_emision::date AS fecha_registro,
    '206'::character varying AS tipotransaccion,
    NULL::character varying AS tipo_servicio,
    NULL::character varying AS tasa,
    nd.numero_documento::character varying AS descripcion,
    nd.total_nota AS debitos,
    0::numeric AS creditos,
    NULL::character varying AS ciclo
   FROM adm_nota_debito nd
     JOIN cliente_maestro cm ON cm.maestro_cliente_id = nd.cliente_id AND cm.company_id = nd.company_id
  WHERE nd.estado_id = ANY (ARRAY[1, 2])
UNION ALL
 SELECT nc.company_id,
    cm.maestro_cliente_clave AS cliente_clave,
    nc.fecha_emision::date AS fecha_docu,
    nc.fecha_emision::date AS fecha_registro,
    '205'::character varying AS tipotransaccion,
    NULL::character varying AS tipo_servicio,
    NULL::character varying AS tasa,
    nc.numero_documento::character varying AS descripcion,
    0::numeric AS debitos,
    nc.total_nota AS creditos,
    NULL::character varying AS ciclo
   FROM adm_nota_credito nc
     JOIN cliente_maestro cm ON cm.maestro_cliente_id = nc.cliente_id AND cm.company_id = nc.company_id
  WHERE nc.estado_id = ANY (ARRAY[1, 2])
;

COMMIT;
