-- =============================================================================
-- F7 H5 (parte reportes) — los rep_* leen EXCLUSIVAMENTE el modelo nuevo
-- =============================================================================
-- Decisión del usuario (2026-07-30): nada legacy se conserva. La vista de
-- vigencia muere; su papel para los 9 reportes lo toma una vista NUEVA sobre el
-- modelo nuevo, con las mismas columnas que los reportes consumen — así cada
-- función cambia solo de relación y su lógica queda intacta.
--
-- vw_rep_movimiento_vigente = el "estado de cuenta" del modelo nuevo:
--   * CARGOS: líneas de facturas no anuladas (montovalor≥0 → débito; el
--     negativo — descuento de adulto mayor — → crédito).
--   * CRÉDITOS a nivel de APLICACIÓN (adm_pago estado APLICADO × sus
--     aplicaciones): así los reportes por servicio (agua/alcantarillado)
--     obtienen el pago repartido por línea, cosa que el espejo nunca dio bien.
--     El remanente sin aplicar de cada pago (saldo a favor) sale como fila
--     propia para que la suma iguale monto_total al centavo.
--     tipotransaccion: '201' para pagos (tipos 2/3/4), '205' para créditos
--     migrados (NC/convenios, tipo 5) — respeta los filtros de los reportes.
--   * ND y NC nuevas (post-corte) como débito/crédito propios.
-- Anulados y reversados quedan fuera (estado_id ≠ 1 / documentos anulados):
-- misma semántica de vigencia que tenía la vista vieja.
--
-- Al final: DROP de vw_transaccion_abonado_vigente (retiro definitivo).
--
-- VALIDACIÓN (copia09, company 2, 01→29-jul): los 9 rep_* comparados
-- antes/después. Diferencias, todas explicadas al centavo y a favor del
-- modelo nuevo: cobros post-candado H4 (el espejo ya no los recibe), 21
-- cargos recibo=0 sin factura en el origen (L2,976.30, estado C), cuotas
-- de convenio fechadas a futuro en el ledger (el reporte viejo las
-- excluía del corte; el nuevo es consistente con sp_obtener_cliente_saldo)
-- y reagrupación de ciclos legacy → catálogo. Detalle en
-- docs/PLAN_F7_CORTE_2026-07.md §H5.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

CREATE OR REPLACE VIEW public.vw_rep_movimiento_vigente AS
-- 1) Cargos: líneas de facturas vigentes
SELECT
    f.company_id,
    f.clientecodigo                                   AS cliente_clave,
    f.fechaemision                                    AS fecha_docu,
    f.fechaemision                                    AS fecha_registro,
    COALESCE(NULLIF(TRIM(d.codigo), ''), d.tiposervicio, 'CARGO')::varchar AS tipotransaccion,
    d.tiposervicio                                    AS tipo_servicio,
    NULL::varchar                                     AS tasa,
    d.descripcion,
    CASE WHEN COALESCE(d.montovalor,0) >= 0 THEN d.montovalor ELSE 0 END::numeric  AS debitos,
    CASE WHEN COALESCE(d.montovalor,0) <  0 THEN -d.montovalor ELSE 0 END::numeric AS creditos,
    NULL::varchar                                     AS ciclo
FROM public.factura f
JOIN public.factura_detalle d ON d.factura_id = f.id
WHERE COALESCE(f.estado,'A') <> 'N'

UNION ALL
-- 2) Créditos aplicados, a nivel de aplicación (pagos, y créditos migrados)
SELECT
    p.company_id,
    p.cliente_clave,
    p.fecha, p.fecha,
    CASE WHEN p.tipo_transaccion_id IN (2,3,4) THEN '201' ELSE '205' END::varchar,
    d.tiposervicio,
    NULL::varchar,
    NULL::varchar,
    0::numeric,
    a.monto_aplicado,
    NULL::varchar
FROM public.adm_pago p
JOIN public.adm_pago_aplicacion a ON a.company_id = p.company_id
                                 AND a.pago_id = p.pago_id
LEFT JOIN public.factura_detalle d ON d.id = a.factura_detalle_id
WHERE p.estado_id = 1

UNION ALL
-- 3) Remanente sin aplicar de cada pago (saldo a favor)
SELECT
    p.company_id,
    p.cliente_clave,
    p.fecha, p.fecha,
    CASE WHEN p.tipo_transaccion_id IN (2,3,4) THEN '201' ELSE '205' END::varchar,
    NULL::varchar, NULL::varchar, NULL::varchar,
    0::numeric,
    p.monto_total - COALESCE(ap.aplicado, 0),
    NULL::varchar
FROM public.adm_pago p
LEFT JOIN (SELECT company_id, pago_id, SUM(monto_aplicado) aplicado
             FROM public.adm_pago_aplicacion GROUP BY 1, 2) ap
       ON ap.company_id = p.company_id AND ap.pago_id = p.pago_id
WHERE p.estado_id = 1
  AND p.monto_total - COALESCE(ap.aplicado, 0) > 0

UNION ALL
-- 4) Notas de débito vivas (documento nuevo, post-corte)
SELECT
    nd.company_id,
    cm.maestro_cliente_clave,
    nd.fecha_emision::date, nd.fecha_emision::date,
    '206'::varchar, NULL::varchar, NULL::varchar,
    nd.numero_documento::varchar,
    nd.total_nota, 0::numeric, NULL::varchar
FROM public.adm_nota_debito nd
JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = nd.cliente_id
                              AND cm.company_id = nd.company_id
WHERE nd.estado_id IN (1,2)

UNION ALL
-- 5) Notas de crédito vivas (documento nuevo, post-corte)
SELECT
    nc.company_id,
    cm.maestro_cliente_clave,
    nc.fecha_emision::date, nc.fecha_emision::date,
    '205'::varchar, NULL::varchar, NULL::varchar,
    nc.numero_documento::varchar,
    0::numeric, nc.total_nota, NULL::varchar
FROM public.adm_nota_credito nc
JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = nc.cliente_id
                              AND cm.company_id = nc.company_id
WHERE nc.estado_id IN (1,2);

COMMENT ON VIEW public.vw_rep_movimiento_vigente IS
'F7 H5 (2026-07-30): estado de cuenta del MODELO NUEVO para los reportes rep_*. Reemplaza a vw_transaccion_abonado_vigente (retirada). Créditos a nivel de aplicación + remanente, para que los reportes por servicio salgan de adm_pago_aplicacion.';

CREATE OR REPLACE FUNCTION public.rep_desglose_facturacion(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date)
 RETURNS TABLE(ciclo_orden integer, ciclo text, facturacion numeric, debitos numeric, creditos numeric, adulto_mayor numeric, pagos_registrados numeric, saldo numeric, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
    FROM public.vw_rep_movimiento_vigente ta
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = ta.company_id
     AND cm.maestro_cliente_clave = ta.cliente_clave
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    CROSS JOIN parametros p
    -- F4: pagos VIGENTES de todos los canales (ver rep_transacciones_periodo).
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND (ta.tipotransaccion IN ('201', '202') OR ta.tipotransaccion ILIKE '%PAGO%')
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

CREATE OR REPLACE FUNCTION public.rep_movimiento_periodo(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date)
 RETURNS TABLE(fila_orden bigint, fecha_movimiento date, fecha_texto text, transaccion_codigo text, descripcion text, debitos numeric, creditos numeric, saldo numeric, es_saldo_anterior boolean, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
ultimo_saldo_cliente AS (
    -- F4 (2026-07-28): suma de movimientos VIGENTES antes del periodo (la
    -- corrida ta.saldo esta corrupta para abonos).
    SELECT
        ta.cliente_clave,
        SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))::numeric(18, 2) AS saldo_cliente
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    GROUP BY ta.cliente_clave
),
saldo_anterior AS (
    SELECT COALESCE(SUM(usc.saldo_cliente), 0)::numeric(18, 2) AS saldo_anterior
    FROM ultimo_saldo_cliente usc
),
facturacion_raw AS (
    SELECT
        f.fechaemision AS fecha_movimiento,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN '101'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN '102'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN '103'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN '104'
            ELSE '111'
        END AS transaccion_codigo,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN 'Facturacion Agua Potable'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN 'Facturacion Alcantarillado'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN 'Facturacion Ambiental'
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN 'Facturacion ERSAP'
            ELSE 'Otros Cargos Facturacion'
        END AS descripcion,
        CASE
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AGUA', 'AGUA_POTABLE', 'POTABLE', 'AP', '1', '01')
                THEN 10
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ALCANTARILLADO', 'ALCANTARILLADO_SANITARIO', 'ALCANTARILLADO SANITARIO', 'AS', 'ALC', '2', '02')
                THEN 20
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('AMBIENTAL', 'TASA_AMBIENTAL', 'TA', '3', '03')
                THEN 30
            WHEN UPPER(TRIM(COALESCE(NULLIF(fd.tiposervicio, ''), NULLIF(fd.codigo, ''), fd.descripcion, ''))) IN ('ERSAP', 'ERSAPS', 'TASA_ERSAP', 'TASA_ERSAPS', 'TASA_SVA_ERSAPS', '4', '04')
                THEN 40
            ELSE 50
        END AS trans_orden,
        SUM(COALESCE(fd.montovalor, 0))::numeric(18, 2) AS debitos
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) > 0
    GROUP BY 1, 2, 3, 4
),
pagos_raw AS (
    SELECT
        COALESCE(ta.fecha_docu, ta.fecha_registro) AS fecha_movimiento,
        '201'::text AS transaccion_codigo,
        'Registro De Pagos'::text AS descripcion,
        60 AS trans_orden,
        SUM(ABS(COALESCE(ta.creditos, 0)))::numeric(18, 2) AS creditos
    -- F4 (2026-07-28): pagos VIGENTES (caja graba el vigente con 'C' y marca
    -- 'A' al anular — el filtro viejo contaba anulados). Entran tambien los
    -- pagos del banco (202) y los PAGO% migrados de SIMAFI.
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND (ta.tipotransaccion IN ('201', '202') OR ta.tipotransaccion ILIKE '%PAGO%')
      AND COALESCE(ta.creditos, 0) <> 0
    GROUP BY 1
),
descuentos_raw AS (
    SELECT
        f.fechaemision AS fecha_movimiento,
        '202'::text AS transaccion_codigo,
        'Descuento Tercera Edad'::text AS descripcion,
        70 AS trans_orden,
        SUM(ABS(COALESCE(fd.montovalor, 0)))::numeric(18, 2) AS creditos
    FROM public.factura f
    JOIN public.factura_detalle fd
      ON fd.company_id = f.company_id
     AND fd.factura_id = f.id
    CROSS JOIN parametros p
    WHERE f.company_id = p.company_id
      AND f.fechaemision BETWEEN p.fecha_desde AND p.fecha_hasta
      AND COALESCE(f.estado, 'A') <> 'N'
      AND COALESCE(fd.montovalor, 0) < 0
    GROUP BY 1
),
movimientos AS (
    SELECT
        fr.fecha_movimiento,
        fr.transaccion_codigo,
        fr.descripcion,
        fr.trans_orden,
        fr.debitos,
        0::numeric(18, 2) AS creditos
    FROM facturacion_raw fr

    UNION ALL

    SELECT
        pr.fecha_movimiento,
        pr.transaccion_codigo,
        pr.descripcion,
        pr.trans_orden,
        0::numeric(18, 2) AS debitos,
        pr.creditos
    FROM pagos_raw pr

    UNION ALL

    SELECT
        dr.fecha_movimiento,
        dr.transaccion_codigo,
        dr.descripcion,
        dr.trans_orden,
        0::numeric(18, 2) AS debitos,
        dr.creditos
    FROM descuentos_raw dr
),
movimientos_ordenados AS (
    SELECT
        ROW_NUMBER() OVER (
            ORDER BY
                m.fecha_movimiento,
                m.trans_orden,
                m.transaccion_codigo,
                m.descripcion
        ) AS fila_orden,
        m.fecha_movimiento,
        to_char(m.fecha_movimiento, 'DD/MM/YYYY')::text AS fecha_texto,
        m.transaccion_codigo,
        m.descripcion,
        m.debitos,
        m.creditos,
        m.trans_orden
    FROM movimientos m
),
movimientos_con_saldo AS (
    SELECT
        (mo.fila_orden + 1)::bigint AS fila_orden,
        mo.fecha_movimiento,
        mo.fecha_texto,
        mo.transaccion_codigo,
        mo.descripcion,
        mo.debitos,
        mo.creditos,
        (
            sa.saldo_anterior +
            SUM(mo.debitos - mo.creditos) OVER (
                ORDER BY
                    mo.fecha_movimiento,
                    mo.trans_orden,
                    mo.transaccion_codigo,
                    mo.descripcion,
                    mo.fila_orden
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            )
        )::numeric(18, 2) AS saldo,
        false AS es_saldo_anterior
    FROM movimientos_ordenados mo
    CROSS JOIN saldo_anterior sa
),
salida AS (
    SELECT
        1::bigint AS fila_orden,
        NULL::date AS fecha_movimiento,
        ''::text AS fecha_texto,
        ''::text AS transaccion_codigo,
        'Saldo Anterior'::text AS descripcion,
        0::numeric(18, 2) AS debitos,
        0::numeric(18, 2) AS creditos,
        sa.saldo_anterior AS saldo,
        true AS es_saldo_anterior
    FROM saldo_anterior sa

    UNION ALL

    SELECT
        mcs.fila_orden,
        mcs.fecha_movimiento,
        mcs.fecha_texto,
        mcs.transaccion_codigo,
        mcs.descripcion,
        mcs.debitos,
        mcs.creditos,
        mcs.saldo,
        mcs.es_saldo_anterior
    FROM movimientos_con_saldo mcs
)
SELECT
    s.fila_orden,
    s.fecha_movimiento,
    s.fecha_texto,
    s.transaccion_codigo,
    s.descripcion,
    s.debitos,
    s.creditos,
    s.saldo,
    s.es_saldo_anterior,
    e.empresa_nombre,
    (
        'Registro de Movimientos del '
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
      AND COALESCE(f.estado, 'A') <> 'N'
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
      AND COALESCE(f.estado, 'A') <> 'N'
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
$function$;

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
        COALESCE(ta.fecha_docu, ta.fecha_registro, p.fecha_corte) AS fecha_movimiento,
        ta.ide,
        ta.tiene_med
    FROM public.transaccion_abonado ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND ta.cliente_clave IS NOT NULL
      AND COALESCE(ta.estado, 'A') = 'A'
      AND COALESCE(ta.fecha_docu, ta.fecha_registro, p.fecha_corte) <= p.fecha_corte
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
      AND f.estado IN ('A','B')
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
      AND COALESCE(ta.fecha_docu, ta.fecha_registro, p.fecha_corte) <= p.fecha_corte
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
$function$;

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria_cobranza(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_categoria_servicio_id integer DEFAULT 0)
 RETURNS TABLE(categoria_orden integer, categoria text, cant_con_medidor integer, facturacion_con_medidor numeric, saldo_con_medidor numeric, consumo_con_medidor numeric, cant_sin_medidor integer, facturacion_sin_medidor numeric, saldo_sin_medidor numeric, cant_total integer, facturacion_total numeric, saldo_total numeric, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
    -- F4 (2026-07-28): suma de movimientos VIGENTES antes del periodo. La
    -- columna corrida ta.saldo quedo corrupta para los abonos y el filtro
    -- <> 'N' contaba pagos anulados de caja/WS.
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))::numeric(18, 2) AS saldo_anterior
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    GROUP BY ta.cliente_clave
),
movimientos_periodo AS (
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    -- F4: solo movimientos vigentes (el <> 'N' contaba pagos anulados).
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
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

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_categoria_detalle(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_categoria_servicio_id integer DEFAULT 0)
 RETURNS TABLE(ciclo_orden integer, ciclo text, cliente_codigo text, cliente_nombre text, ruta text, saldo_anterior numeric, debitos numeric, creditos numeric, saldo_actual numeric, empresa_nombre text, titulo_reporte text, fecha_reporte date, fecha_reporte_texto text)
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
        cm.maestro_cliente_nombre::text AS cliente_nombre,
        COALESCE(cm.maestro_cliente_indicativo_ruta, '')::text AS ruta,
        CASE
            WHEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0') ~ '^[0-9]+$'
                THEN COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), '0')::integer
            ELSE 9999
        END AS ciclo_orden,
        COALESCE(NULLIF(TRIM(ci.ciclos_codigo), ''), 'SIN CICLO')::text AS ciclo,
        COALESCE(cm.categoria_servicio_id, 9999) AS categoria_orden,
        COALESCE(NULLIF(TRIM(cs.descripcion), ''), 'Sin categoria')::text AS categoria
    FROM public.cliente_maestro cm
    CROSS JOIN parametros p
    LEFT JOIN public.ciclos ci
      ON ci.ciclos_id = cm.ciclos_id
    LEFT JOIN public.categoria_servicio cs
      ON cs.categoria_servicio_id = cm.categoria_servicio_id
    WHERE cm.company_id = p.company_id
      AND NULLIF(TRIM(cm.maestro_cliente_clave), '') IS NOT NULL
      AND (p.categoria_servicio_id IS NULL OR cm.categoria_servicio_id = p.categoria_servicio_id)
),
ultimo_saldo_anterior AS (
    -- F4 (2026-07-28): suma de movimientos VIGENTES antes del periodo. La
    -- columna corrida ta.saldo quedo corrupta para los abonos y el filtro
    -- <> 'N' contaba pagos anulados de caja/WS.
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))::numeric(18, 2) AS saldo_anterior
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    GROUP BY ta.cliente_clave
),
movimientos_periodo AS (
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    -- F4: solo movimientos vigentes (el <> 'N' contaba pagos anulados).
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
    GROUP BY ta.cliente_clave
)
SELECT
    cc.ciclo_orden,
    cc.ciclo,
    cc.cliente_clave AS cliente_codigo,
    cc.cliente_nombre,
    cc.ruta,
    COALESCE(usa.saldo_anterior, 0.00)::numeric(18, 2) AS saldo_anterior,
    COALESCE(mp.debitos, 0.00)::numeric(18, 2) AS debitos,
    COALESCE(mp.creditos, 0.00)::numeric(18, 2) AS creditos,
    (COALESCE(usa.saldo_anterior, 0.00) + COALESCE(mp.debitos, 0.00) - COALESCE(mp.creditos, 0.00))::numeric(18, 2) AS saldo_actual,
    e.empresa_nombre,
    (
        'Saldos de Clientes detallado por Categoria ' 
        || COALESCE(
            CASE 
                WHEN p.categoria_servicio_id IS NULL THEN 'Todas' 
                ELSE (
                    SELECT cs.descripcion 
                    FROM public.categoria_servicio cs 
                    WHERE cs.categoria_servicio_id = p.categoria_servicio_id
                ) 
            END, 
            'Todas'
        ) 
        || ' del ' 
        || TO_CHAR(p.fecha_desde, 'DD/MM/YYYY') 
        || ' al ' 
        || TO_CHAR(p.fecha_hasta, 'DD/MM/YYYY')
    )::text AS titulo_reporte,
    CURRENT_DATE AS fecha_reporte,
    TO_CHAR(CURRENT_DATE, 'DD/MM/YYYY') AS fecha_reporte_texto
FROM clientes_catalogo cc
LEFT JOIN ultimo_saldo_anterior usa
  ON usa.cliente_clave = cc.cliente_clave
LEFT JOIN movimientos_periodo mp
  ON mp.cliente_clave = cc.cliente_clave
CROSS JOIN parametros p
CROSS JOIN empresa e
ORDER BY cc.ciclo_orden, cc.ciclo, cc.cliente_clave;
$function$;

CREATE OR REPLACE FUNCTION public.rep_saldo_clientes_ciclo(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date, p_ciclo_id bigint DEFAULT 0)
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
ultimo_saldo_anterior AS (
    -- F4 (2026-07-28): suma de movimientos VIGENTES antes del periodo. La
    -- columna corrida ta.saldo quedo corrupta para los abonos y el filtro
    -- <> 'N' contaba pagos anulados de caja/WS.
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))::numeric(18, 2) AS saldo_anterior
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) < p.fecha_desde
    GROUP BY ta.cliente_clave
),
movimientos_periodo AS (
    SELECT
        ta.cliente_clave::text AS cliente_clave,
        SUM(COALESCE(ta.debitos, 0))::numeric(18, 2) AS debitos,
        SUM(COALESCE(ta.creditos, 0))::numeric(18, 2) AS creditos
    -- F4: solo movimientos vigentes (el <> 'N' contaba pagos anulados).
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND NULLIF(TRIM(ta.cliente_clave), '') IS NOT NULL
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
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
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
          AND f.estado IN ('A','B')
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
$function$;

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
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
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
          AND f.estado IN ('A','B')
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
$function$;

CREATE OR REPLACE FUNCTION public.rep_transacciones_periodo(p_company_id bigint, p_fecha_desde date, p_fecha_hasta date)
 RETURNS TABLE(fila_orden integer, concepto text, agua_potable numeric, alcantarillado_sanitario numeric, ambiental numeric, tasa_ersap numeric, convenio numeric, gestion_legal numeric, otros_cargos numeric, total numeric, es_total boolean, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
    -- F4 (2026-07-28): pagos VIGENTES (caja graba el vigente con 'C' y marca
    -- 'A' al anular — el filtro viejo contaba anulados). Entran tambien los
    -- pagos del banco (202) y los PAGO% migrados de SIMAFI.
    FROM public.vw_rep_movimiento_vigente ta
    CROSS JOIN parametros p
    WHERE ta.company_id = p.company_id
      AND COALESCE(ta.fecha_docu, ta.fecha_registro) BETWEEN p.fecha_desde AND p.fecha_hasta
      AND (ta.tipotransaccion IN ('201', '202') OR ta.tipotransaccion ILIKE '%PAGO%')
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



-- ----------------------------------------------------------------------------
-- Retiro definitivo de la vista de vigencia legacy
-- ----------------------------------------------------------------------------
DROP VIEW IF EXISTS public.vw_transaccion_abonado_vigente;

COMMIT;
