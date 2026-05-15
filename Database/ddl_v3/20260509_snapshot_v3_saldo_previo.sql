-- =============================================================================
-- Snapshot offline V3 con saldo previo del cliente (multi-empresa, dinamico)
-- Fecha: 2026-05-09
-- Plan: PLAN_ENTREGA_2026-05-25.md Sprint 2 dia 8
--
-- Cambios sobre 20260418_add_sp_adm_generar_snapshot_offline_cliente_lectura.sql:
--   1. contract_version sube a OFFLINE_SNAPSHOT_V3_2 para que el APK distinga
--      versiones (V3_1 = sin saldos, V3_2 = con saldos).
--   2. Nuevo CTE saldos_servicios: itera servicios activos del cliente y llama
--      a sp_obtener_cliente_saldo_servicio_detalle(p_company_id, ...) por cada uno.
--   3. snapshot_json incluye 2 campos nuevos:
--      - saldo_anterior_total (numeric): de sp_obtener_cliente_saldo con company_id
--      - saldos_por_servicio (jsonb array): [{servicio_codigo, servicio_nombre, saldo_anterior}]
--   4. Usa las firmas con p_company_id de los SPs de saldo (script previo:
--      20260509_sp_obtener_cliente_saldo_company.sql).
--
-- Precondiciones:
--   - 20260509_sp_obtener_cliente_saldo_company.sql aplicado.
--   - transaccion_abonado.company_id NOT NULL + backfill OK (07-may).
--
-- Idempotente.
-- =============================================================================

DROP FUNCTION IF EXISTS public.sp_adm_generar_snapshot_offline_cliente_lectura(bigint, bigint, integer, integer, date);

CREATE OR REPLACE FUNCTION public.sp_adm_generar_snapshot_offline_cliente_lectura(
    p_company_id bigint,
    p_cliente_id bigint,
    p_anio integer,
    p_mes integer,
    p_fecha_factura date DEFAULT current_date
)
RETURNS TABLE
(
    company_id bigint,
    cliente_id bigint,
    cliente_clave text,
    contador text,
    snapshot_json jsonb
)
LANGUAGE plpgsql
STABLE
AS
$$
DECLARE
    v_cliente public.cliente_maestro%ROWTYPE;
    v_historico public.historicomedicion%ROWTYPE;
    v_tiene_historico boolean := false;
    v_anchor_cliente_servicio_id bigint;
    v_anchor_categoria_codigo text;
    v_anchor_condicion_codigo text;
    v_anchor_segmento_codigo text;
    v_tiene_regla_tercera_edad boolean := false;
    v_warnings jsonb := '[]'::jsonb;
    v_saldo_anterior_total numeric(18,2);
BEGIN
    SELECT *
    INTO v_cliente
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.maestro_cliente_id = p_cliente_id
      AND cm.estado = true;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe cliente activo para company_id=% y cliente_id=%.',
            p_company_id, p_cliente_id;
    END IF;

    SELECT *
    INTO v_historico
    FROM public.historicomedicion hm
    WHERE hm.ano = p_anio
      AND hm.mes = p_mes
      AND hm.clave = v_cliente.maestro_cliente_clave
    ORDER BY hm.ide DESC
    LIMIT 1;

    v_tiene_historico := FOUND;

    SELECT
        cs.cliente_servicio_id,
        cat.codigo,
        cond.codigo,
        seg.codigo
    INTO
        v_anchor_cliente_servicio_id,
        v_anchor_categoria_codigo,
        v_anchor_condicion_codigo,
        v_anchor_segmento_codigo
    FROM public.adm_cliente_servicio cs
    JOIN public.adm_servicio s
      ON s.company_id = cs.company_id
     AND s.servicio_id = cs.servicio_id
    JOIN public.adm_tipo_servicio ts
      ON ts.company_id = s.company_id
     AND ts.tipo_servicio_id = s.tipo_servicio_id
    LEFT JOIN public.adm_categoria_regulatoria cat
      ON cat.company_id = cs.company_id
     AND cat.categoria_regulatoria_id = cs.categoria_regulatoria_id
    LEFT JOIN public.adm_condicion_medicion cond
      ON cond.company_id = cs.company_id
     AND cond.condicion_medicion_id = cs.condicion_medicion_id
    LEFT JOIN public.adm_segmento_tarifario seg
      ON seg.company_id = cs.company_id
     AND seg.segmento_tarifario_id = cs.segmento_tarifario_id
    WHERE cs.company_id = p_company_id
      AND cs.cliente_id = p_cliente_id
      AND cs.status_id = 1
      AND p_fecha_factura >= cs.fecha_alta
      AND (cs.fecha_baja IS NULL OR p_fecha_factura <= cs.fecha_baja)
      AND s.status_id = 1
      AND ts.codigo = 'BASE'
    ORDER BY CASE WHEN s.codigo = 'AGUA_POTABLE' THEN 0 ELSE 1 END,
             s.app_orden,
             cs.cliente_servicio_id
    LIMIT 1;

    IF v_anchor_cliente_servicio_id IS NULL THEN
        RAISE EXCEPTION 'El cliente % no tiene servicios base activos en adm_cliente_servicio.',
            v_cliente.maestro_cliente_clave;
    END IF;

    IF COALESCE(v_cliente.maestro_cliente_tercera_edad, false) THEN
        SELECT EXISTS (
            SELECT 1
            FROM public.adm_cliente_servicio cs
            JOIN public.adm_servicio s
              ON s.company_id = cs.company_id
             AND s.servicio_id = cs.servicio_id
            JOIN public.adm_categoria_regulatoria cat
              ON cat.company_id = cs.company_id
             AND cat.categoria_regulatoria_id = cs.categoria_regulatoria_id
            JOIN public.adm_cuadro_tarifario ct
              ON ct.company_id = cs.company_id
             AND ct.servicio_id = cs.servicio_id
             AND ct.status_id = 1
             AND p_fecha_factura >= ct.vigencia_desde
             AND (ct.vigencia_hasta IS NULL OR p_fecha_factura <= ct.vigencia_hasta)
             AND (ct.categoria_regulatoria_id IS NULL OR ct.categoria_regulatoria_id = cs.categoria_regulatoria_id)
            JOIN public.adm_ajuste_tarifario aj
              ON aj.company_id = ct.company_id
             AND aj.cuadro_tarifario_id = ct.cuadro_tarifario_id
             AND aj.status_id = 1
             AND aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
            WHERE cs.company_id = p_company_id
              AND cs.cliente_id = p_cliente_id
              AND cs.status_id = 1
              AND p_fecha_factura >= cs.fecha_alta
              AND (cs.fecha_baja IS NULL OR p_fecha_factura <= cs.fecha_baja)
              AND s.codigo = 'AGUA_POTABLE'
              AND cat.codigo = 'DOMESTICO'
        )
        INTO v_tiene_regla_tercera_edad;

        IF NOT v_tiene_regla_tercera_edad THEN
            v_warnings := v_warnings || jsonb_build_array('TERCERA_EDAD_SIN_REGLA_ACTIVA');
        END IF;
    END IF;

    IF NOT v_tiene_historico THEN
        v_warnings := v_warnings || jsonb_build_array('SIN_HISTORICO_PERIODO');
    END IF;

    -- Saldo total del cliente (multi-empresa). Fuente unica: transaccion_abonado.saldo.
    SELECT s.saldo_actual
      INTO v_saldo_anterior_total
      FROM public.sp_obtener_cliente_saldo(p_company_id, v_cliente.maestro_cliente_clave) s
     LIMIT 1;
    v_saldo_anterior_total := COALESCE(v_saldo_anterior_total, 0);

    RETURN QUERY
    WITH base_services AS (
        SELECT
            cs.cliente_servicio_id,
            cs.cliente_id,
            cs.servicio_id,
            cs.categoria_regulatoria_id,
            cs.condicion_medicion_id,
            cs.segmento_tarifario_id,
            s.codigo AS servicio_codigo,
            s.nombre AS servicio_nombre,
            ts.codigo AS tipo_servicio_codigo,
            s.app_orden,
            s.usa_condicion_medicion,
            cat.codigo AS categoria_codigo,
            cat.nombre AS categoria_nombre,
            cond.codigo AS condicion_codigo,
            cond.nombre AS condicion_nombre,
            seg.codigo AS segmento_codigo,
            seg.nombre AS segmento_nombre
        FROM public.adm_cliente_servicio cs
        JOIN public.adm_servicio s
          ON s.company_id = cs.company_id
         AND s.servicio_id = cs.servicio_id
        JOIN public.adm_tipo_servicio ts
          ON ts.company_id = s.company_id
         AND ts.tipo_servicio_id = s.tipo_servicio_id
        LEFT JOIN public.adm_categoria_regulatoria cat
          ON cat.company_id = cs.company_id
         AND cat.categoria_regulatoria_id = cs.categoria_regulatoria_id
        LEFT JOIN public.adm_condicion_medicion cond
          ON cond.company_id = cs.company_id
         AND cond.condicion_medicion_id = cs.condicion_medicion_id
        LEFT JOIN public.adm_segmento_tarifario seg
          ON seg.company_id = cs.company_id
         AND seg.segmento_tarifario_id = cs.segmento_tarifario_id
        WHERE cs.company_id = p_company_id
          AND cs.cliente_id = p_cliente_id
          AND cs.status_id = 1
          AND p_fecha_factura >= cs.fecha_alta
          AND (cs.fecha_baja IS NULL OR p_fecha_factura <= cs.fecha_baja)
          AND s.status_id = 1
          AND ts.codigo = 'BASE'
    ),
    base_cuadros AS (
        SELECT
            bs.*,
            ct.cuadro_tarifario_id,
            ct.codigo AS cuadro_codigo,
            ct.nombre AS cuadro_nombre,
            ct.prioridad
        FROM base_services bs
        JOIN LATERAL (
            SELECT ct.*
            FROM public.adm_cuadro_tarifario ct
            WHERE ct.company_id = p_company_id
              AND ct.status_id = 1
              AND p_fecha_factura >= ct.vigencia_desde
              AND (ct.vigencia_hasta IS NULL OR p_fecha_factura <= ct.vigencia_hasta)
              AND ct.servicio_id = bs.servicio_id
              AND (ct.categoria_regulatoria_id IS NULL OR ct.categoria_regulatoria_id = bs.categoria_regulatoria_id)
              AND (ct.condicion_medicion_id IS NULL OR ct.condicion_medicion_id = bs.condicion_medicion_id)
              AND (
                    (bs.segmento_tarifario_id IS NULL AND ct.segmento_tarifario_id IS NULL)
                    OR
                    (
                        bs.segmento_tarifario_id IS NOT NULL
                        AND (
                            ct.segmento_tarifario_id = bs.segmento_tarifario_id
                            OR ct.segmento_tarifario_id IS NULL
                        )
                    )
                  )
            ORDER BY
                CASE
                    WHEN ct.segmento_tarifario_id = bs.segmento_tarifario_id THEN 0
                    WHEN ct.segmento_tarifario_id IS NULL THEN 1
                    ELSE 2
                END,
                CASE
                    WHEN ct.condicion_medicion_id = bs.condicion_medicion_id THEN 0
                    WHEN ct.condicion_medicion_id IS NULL THEN 1
                    ELSE 2
                END,
                CASE
                    WHEN ct.categoria_regulatoria_id = bs.categoria_regulatoria_id THEN 0
                    WHEN ct.categoria_regulatoria_id IS NULL THEN 1
                    ELSE 2
                END,
                ct.prioridad ASC,
                ct.vigencia_desde DESC,
                ct.cuadro_tarifario_id DESC
            LIMIT 1
        ) ct ON true
    ),
    base_rules AS (
        SELECT
            bc.cliente_servicio_id,
            bc.cliente_id,
            bc.servicio_id,
            bc.servicio_codigo,
            bc.servicio_nombre,
            bc.tipo_servicio_codigo,
            bc.app_orden,
            bc.usa_condicion_medicion,
            bc.categoria_codigo,
            bc.categoria_nombre,
            bc.condicion_codigo,
            bc.condicion_nombre,
            bc.segmento_codigo,
            bc.segmento_nombre,
            bc.cuadro_tarifario_id,
            bc.cuadro_codigo,
            bc.cuadro_nombre,
            rt.regla_tarifaria_id,
            rt.orden AS regla_orden,
            tr.codigo AS tipo_regla_codigo,
            tr.nombre AS tipo_regla_nombre,
            COALESCE(rt.parametros ->> 'modo_calculo', tr.codigo) AS modo_calculo,
            rt.consumo_minimo,
            rt.consumo_maximo,
            rt.monto_fijo,
            rt.monto_unitario,
            rt.porcentaje,
            COALESCE((rt.parametros ->> 'alquiler')::numeric, 0)::numeric(18, 4) AS alquiler,
            srv_ref.codigo AS servicio_referencia_codigo,
            rt.parametros
        FROM base_cuadros bc
        JOIN public.adm_regla_tarifaria rt
          ON rt.company_id = p_company_id
         AND rt.cuadro_tarifario_id = bc.cuadro_tarifario_id
         AND rt.status_id = 1
        JOIN public.adm_tipo_regla_tarifaria tr
          ON tr.company_id = rt.company_id
         AND tr.tipo_regla_tarifaria_id = rt.tipo_regla_tarifaria_id
        LEFT JOIN public.adm_servicio srv_ref
          ON srv_ref.company_id = rt.company_id
         AND srv_ref.servicio_id = rt.servicio_referencia_id
    ),
    base_grouped AS (
        SELECT
            br.cliente_servicio_id,
            br.cliente_id,
            br.servicio_id,
            br.servicio_codigo,
            br.servicio_nombre,
            br.tipo_servicio_codigo,
            br.app_orden,
            br.usa_condicion_medicion,
            br.categoria_codigo,
            br.categoria_nombre,
            br.condicion_codigo,
            br.condicion_nombre,
            br.segmento_codigo,
            br.segmento_nombre,
            br.cuadro_tarifario_id,
            br.cuadro_codigo,
            br.cuadro_nombre,
            jsonb_agg(
                jsonb_build_object(
                    'regla_tarifaria_id', br.regla_tarifaria_id,
                    'orden', br.regla_orden,
                    'tipo_regla_codigo', br.tipo_regla_codigo,
                    'tipo_regla_nombre', br.tipo_regla_nombre,
                    'modo_calculo', br.modo_calculo,
                    'consumo_minimo', br.consumo_minimo,
                    'consumo_maximo', br.consumo_maximo,
                    'monto_fijo', br.monto_fijo,
                    'monto_unitario', br.monto_unitario,
                    'porcentaje', br.porcentaje,
                    'alquiler', br.alquiler,
                    'servicio_referencia_codigo', br.servicio_referencia_codigo,
                    'parametros', COALESCE(br.parametros, '{}'::jsonb)
                )
                ORDER BY br.regla_orden, br.regla_tarifaria_id
            ) AS reglas_json
        FROM base_rules br
        GROUP BY
            br.cliente_servicio_id,
            br.cliente_id,
            br.servicio_id,
            br.servicio_codigo,
            br.servicio_nombre,
            br.tipo_servicio_codigo,
            br.app_orden,
            br.usa_condicion_medicion,
            br.categoria_codigo,
            br.categoria_nombre,
            br.condicion_codigo,
            br.condicion_nombre,
            br.segmento_codigo,
            br.segmento_nombre,
            br.cuadro_tarifario_id,
            br.cuadro_codigo,
            br.cuadro_nombre
    ),
    anchor_context AS (
        SELECT
            cs.cliente_servicio_id,
            cs.cliente_id,
            cs.categoria_regulatoria_id,
            cs.condicion_medicion_id,
            cs.segmento_tarifario_id
        FROM public.adm_cliente_servicio cs
        WHERE cs.company_id = p_company_id
          AND cs.cliente_servicio_id = v_anchor_cliente_servicio_id
    ),
    derived_services AS (
        SELECT
            s.servicio_id,
            s.codigo AS servicio_codigo,
            s.nombre AS servicio_nombre,
            ts.codigo AS tipo_servicio_codigo,
            s.app_orden,
            s.usa_condicion_medicion,
            ac.cliente_servicio_id AS anchor_cliente_servicio_id,
            ac.cliente_id,
            ac.categoria_regulatoria_id,
            ac.condicion_medicion_id,
            ac.segmento_tarifario_id
        FROM public.adm_servicio s
        JOIN public.adm_tipo_servicio ts
          ON ts.company_id = s.company_id
         AND ts.tipo_servicio_id = s.tipo_servicio_id
        CROSS JOIN anchor_context ac
        WHERE s.company_id = p_company_id
          AND s.status_id = 1
          AND s.genera_por_regla = true
          AND ts.codigo = 'DERIVADO'
    ),
    cond_no_aplica AS (
        SELECT condicion_medicion_id
        FROM public.adm_condicion_medicion
        WHERE adm_condicion_medicion.company_id = p_company_id
          AND codigo = 'NO_APLICA'
        LIMIT 1
    ),
    derived_cuadros AS (
        SELECT
            ds.anchor_cliente_servicio_id AS cliente_servicio_id,
            ds.cliente_id,
            ds.servicio_id,
            ds.servicio_codigo,
            ds.servicio_nombre,
            ds.tipo_servicio_codigo,
            ds.app_orden,
            ds.usa_condicion_medicion,
            cat.codigo AS categoria_codigo,
            cat.nombre AS categoria_nombre,
            cond.codigo AS condicion_codigo,
            cond.nombre AS condicion_nombre,
            seg.codigo AS segmento_codigo,
            seg.nombre AS segmento_nombre,
            ct.cuadro_tarifario_id,
            ct.codigo AS cuadro_codigo,
            ct.nombre AS cuadro_nombre
        FROM derived_services ds
        JOIN LATERAL (
            SELECT ct.*
            FROM public.adm_cuadro_tarifario ct
            WHERE ct.company_id = p_company_id
              AND ct.servicio_id = ds.servicio_id
              AND ct.status_id = 1
              AND p_fecha_factura >= ct.vigencia_desde
              AND (ct.vigencia_hasta IS NULL OR p_fecha_factura <= ct.vigencia_hasta)
              AND (ct.categoria_regulatoria_id IS NULL OR ct.categoria_regulatoria_id = ds.categoria_regulatoria_id)
              AND (
                    ct.condicion_medicion_id IS NULL
                    OR ct.condicion_medicion_id = ds.condicion_medicion_id
                    OR ct.condicion_medicion_id = (SELECT condicion_medicion_id FROM cond_no_aplica)
                  )
              AND (
                    (ds.segmento_tarifario_id IS NULL AND ct.segmento_tarifario_id IS NULL)
                    OR
                    (
                        ds.segmento_tarifario_id IS NOT NULL
                        AND (
                            ct.segmento_tarifario_id = ds.segmento_tarifario_id
                            OR ct.segmento_tarifario_id IS NULL
                        )
                    )
                  )
            ORDER BY
                CASE
                    WHEN ct.segmento_tarifario_id = ds.segmento_tarifario_id THEN 0
                    WHEN ct.segmento_tarifario_id IS NULL THEN 1
                    ELSE 2
                END,
                CASE
                    WHEN ct.condicion_medicion_id = ds.condicion_medicion_id THEN 0
                    WHEN ct.condicion_medicion_id = (SELECT condicion_medicion_id FROM cond_no_aplica) THEN 1
                    WHEN ct.condicion_medicion_id IS NULL THEN 2
                    ELSE 3
                END,
                CASE
                    WHEN ct.categoria_regulatoria_id = ds.categoria_regulatoria_id THEN 0
                    WHEN ct.categoria_regulatoria_id IS NULL THEN 1
                    ELSE 2
                END,
                ct.prioridad ASC,
                ct.vigencia_desde DESC,
                ct.cuadro_tarifario_id DESC
            LIMIT 1
        ) ct ON true
        LEFT JOIN public.adm_categoria_regulatoria cat
          ON cat.company_id = p_company_id
         AND cat.categoria_regulatoria_id = ct.categoria_regulatoria_id
        LEFT JOIN public.adm_condicion_medicion cond
          ON cond.company_id = p_company_id
         AND cond.condicion_medicion_id = ct.condicion_medicion_id
        LEFT JOIN public.adm_segmento_tarifario seg
          ON seg.company_id = p_company_id
         AND seg.segmento_tarifario_id = ct.segmento_tarifario_id
    ),
    derived_rules AS (
        SELECT
            dc.cliente_servicio_id,
            dc.cliente_id,
            dc.servicio_id,
            dc.servicio_codigo,
            dc.servicio_nombre,
            dc.tipo_servicio_codigo,
            dc.app_orden,
            dc.usa_condicion_medicion,
            dc.categoria_codigo,
            dc.categoria_nombre,
            dc.condicion_codigo,
            dc.condicion_nombre,
            dc.segmento_codigo,
            dc.segmento_nombre,
            dc.cuadro_tarifario_id,
            dc.cuadro_codigo,
            dc.cuadro_nombre,
            rt.regla_tarifaria_id,
            rt.orden AS regla_orden,
            tr.codigo AS tipo_regla_codigo,
            tr.nombre AS tipo_regla_nombre,
            COALESCE(rt.parametros ->> 'modo_calculo', tr.codigo) AS modo_calculo,
            rt.consumo_minimo,
            rt.consumo_maximo,
            rt.monto_fijo,
            rt.monto_unitario,
            rt.porcentaje,
            COALESCE((rt.parametros ->> 'alquiler')::numeric, 0)::numeric(18, 4) AS alquiler,
            srv_ref.codigo AS servicio_referencia_codigo,
            rt.parametros
        FROM derived_cuadros dc
        JOIN public.adm_regla_tarifaria rt
          ON rt.company_id = p_company_id
         AND rt.cuadro_tarifario_id = dc.cuadro_tarifario_id
         AND rt.status_id = 1
        JOIN public.adm_tipo_regla_tarifaria tr
          ON tr.company_id = rt.company_id
         AND tr.tipo_regla_tarifaria_id = rt.tipo_regla_tarifaria_id
        LEFT JOIN public.adm_servicio srv_ref
          ON srv_ref.company_id = rt.company_id
         AND srv_ref.servicio_id = rt.servicio_referencia_id
    ),
    derived_grouped AS (
        SELECT
            dr.cliente_servicio_id,
            dr.cliente_id,
            dr.servicio_id,
            dr.servicio_codigo,
            dr.servicio_nombre,
            dr.tipo_servicio_codigo,
            dr.app_orden,
            dr.usa_condicion_medicion,
            dr.categoria_codigo,
            dr.categoria_nombre,
            dr.condicion_codigo,
            dr.condicion_nombre,
            dr.segmento_codigo,
            dr.segmento_nombre,
            dr.cuadro_tarifario_id,
            dr.cuadro_codigo,
            dr.cuadro_nombre,
            jsonb_agg(
                jsonb_build_object(
                    'regla_tarifaria_id', dr.regla_tarifaria_id,
                    'orden', dr.regla_orden,
                    'tipo_regla_codigo', dr.tipo_regla_codigo,
                    'tipo_regla_nombre', dr.tipo_regla_nombre,
                    'modo_calculo', dr.modo_calculo,
                    'consumo_minimo', dr.consumo_minimo,
                    'consumo_maximo', dr.consumo_maximo,
                    'monto_fijo', dr.monto_fijo,
                    'monto_unitario', dr.monto_unitario,
                    'porcentaje', dr.porcentaje,
                    'alquiler', dr.alquiler,
                    'servicio_referencia_codigo', dr.servicio_referencia_codigo,
                    'parametros', COALESCE(dr.parametros, '{}'::jsonb)
                )
                ORDER BY dr.regla_orden, dr.regla_tarifaria_id
            ) AS reglas_json
        FROM derived_rules dr
        GROUP BY
            dr.cliente_servicio_id,
            dr.cliente_id,
            dr.servicio_id,
            dr.servicio_codigo,
            dr.servicio_nombre,
            dr.tipo_servicio_codigo,
            dr.app_orden,
            dr.usa_condicion_medicion,
            dr.categoria_codigo,
            dr.categoria_nombre,
            dr.condicion_codigo,
            dr.condicion_nombre,
            dr.segmento_codigo,
            dr.segmento_nombre,
            dr.cuadro_tarifario_id,
            dr.cuadro_codigo,
            dr.cuadro_nombre
    ),
    servicios_union AS (
        SELECT * FROM base_grouped
        UNION ALL
        SELECT * FROM derived_grouped
    ),
    servicios_json AS (
        SELECT jsonb_agg(
            jsonb_build_object(
                'cliente_servicio_id', su.cliente_servicio_id,
                'servicio_id', su.servicio_id,
                'servicio_codigo', su.servicio_codigo,
                'servicio_nombre', su.servicio_nombre,
                'tipo_servicio', su.tipo_servicio_codigo,
                'app_orden', su.app_orden,
                'usa_condicion_medicion', su.usa_condicion_medicion,
                'categoria_codigo', su.categoria_codigo,
                'categoria_nombre', su.categoria_nombre,
                'condicion_medicion_codigo', su.condicion_codigo,
                'condicion_medicion_nombre', su.condicion_nombre,
                'segmento_tarifario_codigo', su.segmento_codigo,
                'segmento_tarifario_nombre', su.segmento_nombre,
                'cuadro_tarifario_id', su.cuadro_tarifario_id,
                'cuadro_codigo', su.cuadro_codigo,
                'cuadro_nombre', su.cuadro_nombre,
                'reglas', COALESCE(su.reglas_json, '[]'::jsonb)
            )
            ORDER BY CASE WHEN su.tipo_servicio_codigo = 'BASE' THEN 0 ELSE 1 END,
                     su.app_orden, su.servicio_codigo
        ) AS servicios
        FROM servicios_union su
    ),
    -- Saldos por servicio (multi-empresa, dinamico). Itera todos los servicios
    -- activos del cliente (base + derivados) y consulta saldo_detalle por cada uno.
    saldos_servicios AS (
        SELECT
            sv.servicio_codigo,
            sv.servicio_nombre,
            COALESCE(
                public.sp_obtener_cliente_saldo_servicio_detalle(
                    p_company_id,
                    v_cliente.maestro_cliente_clave,
                    sv.servicio_codigo
                ),
                0
            )::numeric(18,2) AS saldo_anterior
        FROM (
            SELECT DISTINCT s.codigo AS servicio_codigo, s.nombre AS servicio_nombre, s.app_orden
              FROM public.adm_cliente_servicio cs
              JOIN public.adm_servicio s
                ON s.company_id = cs.company_id
               AND s.servicio_id = cs.servicio_id
             WHERE cs.company_id = p_company_id
               AND cs.cliente_id = p_cliente_id
               AND cs.status_id = 1
               AND p_fecha_factura >= cs.fecha_alta
               AND (cs.fecha_baja IS NULL OR p_fecha_factura <= cs.fecha_baja)
               AND s.status_id = 1
            UNION
            -- Servicios derivados (genera_por_regla = true) que aplican al cliente
            SELECT s.codigo, s.nombre, s.app_orden
              FROM public.adm_servicio s
             WHERE s.company_id = p_company_id
               AND s.status_id = 1
               AND s.genera_por_regla = true
        ) sv
    ),
    saldos_json AS (
        SELECT jsonb_agg(
            jsonb_build_object(
                'servicio_codigo', ss.servicio_codigo,
                'servicio_nombre', ss.servicio_nombre,
                'saldo_anterior', ss.saldo_anterior
            )
            ORDER BY ss.servicio_codigo
        ) AS saldos
        FROM saldos_servicios ss
    )
    SELECT
        p_company_id,
        p_cliente_id,
        v_cliente.maestro_cliente_clave::text,
        NULLIF(BTRIM(COALESCE(v_cliente.contador, '')), '')::text,
        jsonb_build_object(
            'contract_version', 'OFFLINE_SNAPSHOT_V3_2',
            'company_id', p_company_id,
            'cliente_id', p_cliente_id,
            'cliente_clave', v_cliente.maestro_cliente_clave,
            'cliente_nombre', v_cliente.maestro_cliente_nombre,
            'contador', NULLIF(BTRIM(COALESCE(v_cliente.contador, '')), ''),
            'tiene_medidor', COALESCE(v_cliente.maestro_cliente_tiene_medidor, false),
            'categoria_ancla_codigo', v_anchor_categoria_codigo,
            'condicion_ancla_codigo', v_anchor_condicion_codigo,
            'segmento_ancla_codigo', v_anchor_segmento_codigo,
            'anio', p_anio,
            'mes', p_mes,
            'fecha_factura', p_fecha_factura,
            'lectura_anterior_referencia', COALESCE(v_historico.lect_ant, 0),
            'promedio_referencia', COALESCE(v_historico.lec_prom, 0),
            'saldo_anterior_total', v_saldo_anterior_total,
            'saldos_por_servicio', COALESCE((SELECT saldos FROM saldos_json), '[]'::jsonb),
            'warnings', COALESCE(v_warnings, '[]'::jsonb),
            'servicios', COALESCE((SELECT servicios FROM servicios_json), '[]'::jsonb)
        ) AS snapshot_json;
END;
$$;

COMMENT ON FUNCTION public.sp_adm_generar_snapshot_offline_cliente_lectura(bigint, bigint, integer, integer, date)
IS 'Genera el paquete offline V3 de reglas tarifarias por cliente para la app lectora.
contract_version OFFLINE_SNAPSHOT_V3_2 (2026-05-09): incluye saldo_anterior_total y saldos_por_servicio dinamico.
Saldos vienen de la fuente unica transaccion_abonado.saldo y .saldo_detalle (multi-empresa).';

-- =============================================================================
-- Verificacion / smoke test (correr manualmente despues del COMMIT, ajustar IDs)
-- =============================================================================
--
-- SELECT contract_version, saldo_anterior_total, saldos_por_servicio
--   FROM (
--     SELECT snapshot_json ->> 'contract_version' AS contract_version,
--            (snapshot_json ->> 'saldo_anterior_total')::numeric AS saldo_anterior_total,
--            snapshot_json -> 'saldos_por_servicio' AS saldos_por_servicio
--       FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(
--           1::bigint,           -- p_company_id (ajustar)
--           1::bigint,           -- p_cliente_id (cliente con saldo > 0)
--           2026,                -- p_anio
--           5,                   -- p_mes
--           CURRENT_DATE
--       )
--   ) x;
--
-- Esperado:
--   contract_version       = 'OFFLINE_SNAPSHOT_V3_2'
--   saldo_anterior_total   numeric (puede ser 0 o positivo)
--   saldos_por_servicio    jsonb array, 1 entry por servicio activo del cliente +
--                          servicios derivados que apliquen
