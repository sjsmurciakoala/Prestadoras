-- =============================================================================
-- Resolver de prueba: servicio derivado desde un cliente_servicio base
-- Fecha: 2026-04-17
-- Requiere:
--   - 20260416_adm_motor_tarifario_core.sql
--   - 20260416_adm_motor_tarifario_apc_seed.sql
--   - 20260417_adm_motor_tarifario_ersaps_seed.sql
--   - 20260417_add_sp_adm_resolver_tarifa_cliente_servicio.sql
--
-- Alcance:
--   - resuelve un servicio derivado (por ejemplo TASA_SVA_ERSAPS o TASA_AMBIENTAL)
--     a partir del contexto de un adm_cliente_servicio base
--   - soporta:
--       * MONTO_FIJO
--       * RANGO_CONSUMO con modo ACUMULADO_POR_RANGO_APC
--       * RANGO_CONSUMO con modo TRAMO_DIRECTO_APC
--       * PORCENTAJE_SERVICIO usando servicios base resueltos
--
-- Nota:
--   - Si una regla porcentual referencia un servicio que el cliente no tiene asignado,
--     la regla se devuelve con monto 0 para dejar trazabilidad.
-- =============================================================================

DROP FUNCTION IF EXISTS public.sp_adm_resolver_servicio_derivado_cliente_servicio(bigint, bigint, varchar, date, numeric);

CREATE OR REPLACE FUNCTION public.sp_adm_resolver_servicio_derivado_cliente_servicio(
    p_company_id bigint,
    p_cliente_servicio_base_id bigint,
    p_servicio_codigo varchar,
    p_fecha_facturacion date,
    p_consumo numeric(18, 4) DEFAULT NULL
)
RETURNS TABLE
(
    cliente_servicio_base_id bigint,
    cliente_id bigint,
    servicio_id bigint,
    servicio_codigo text,
    servicio_nombre text,
    cuadro_tarifario_id bigint,
    cuadro_codigo text,
    cuadro_nombre text,
    regla_tarifaria_id bigint,
    regla_orden integer,
    tipo_regla_codigo text,
    tipo_regla_nombre text,
    modo_calculo text,
    consumo numeric(18, 4),
    consumo_minimo numeric(18, 4),
    consumo_maximo numeric(18, 4),
    monto_fijo numeric(18, 4),
    monto_unitario numeric(18, 4),
    porcentaje numeric(18, 6),
    unidades_aplicadas numeric(18, 4),
    alquiler_aplicado numeric(18, 4),
    monto_referencia numeric(18, 4),
    monto_calculado numeric(18, 4),
    servicio_referencia_id bigint,
    servicio_referencia_codigo text,
    cliente_servicio_referencia_id bigint,
    parametros jsonb,
    detalle_calculo text
)
LANGUAGE plpgsql
STABLE
AS
$$
DECLARE
    v_base public.adm_cliente_servicio%ROWTYPE;
    v_servicio public.adm_servicio%ROWTYPE;
    v_cuadro public.adm_cuadro_tarifario%ROWTYPE;
    v_requiere_consumo boolean;
    v_condicion_no_aplica_id bigint;
    v_condicion_con_medicion_id bigint;
BEGIN
    SELECT *
    INTO v_base
    FROM public.adm_cliente_servicio cs
    WHERE cs.company_id = p_company_id
      AND cs.cliente_servicio_id = p_cliente_servicio_base_id
      AND cs.status_id = 1
      AND p_fecha_facturacion >= cs.fecha_alta
      AND (cs.fecha_baja IS NULL OR p_fecha_facturacion <= cs.fecha_baja);

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe adm_cliente_servicio base activo para company_id=% y cliente_servicio_id=% en fecha %.',
            p_company_id, p_cliente_servicio_base_id, p_fecha_facturacion;
    END IF;

    SELECT *
    INTO v_servicio
    FROM public.adm_servicio s
    WHERE s.company_id = p_company_id
      AND s.codigo = p_servicio_codigo
      AND s.status_id = 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe servicio activo con codigo=% para company_id=%.',
            p_servicio_codigo, p_company_id;
    END IF;

    SELECT condicion_medicion_id
    INTO v_condicion_no_aplica_id
    FROM public.adm_condicion_medicion
    WHERE company_id = p_company_id
      AND codigo = 'NO_APLICA';

    SELECT condicion_medicion_id
    INTO v_condicion_con_medicion_id
    FROM public.adm_condicion_medicion
    WHERE company_id = p_company_id
      AND codigo = 'CON_MEDICION';

    SELECT ct.*
    INTO v_cuadro
    FROM public.adm_cuadro_tarifario ct
    WHERE ct.company_id = p_company_id
      AND ct.servicio_id = v_servicio.servicio_id
      AND ct.status_id = 1
      AND p_fecha_facturacion >= ct.vigencia_desde
      AND (ct.vigencia_hasta IS NULL OR p_fecha_facturacion <= ct.vigencia_hasta)
      AND (ct.categoria_regulatoria_id IS NULL OR ct.categoria_regulatoria_id = v_base.categoria_regulatoria_id)
      AND (
            ct.condicion_medicion_id IS NULL
            OR ct.condicion_medicion_id = v_base.condicion_medicion_id
            OR ct.condicion_medicion_id = v_condicion_no_aplica_id
          )
      AND (
            (v_base.segmento_tarifario_id IS NULL AND ct.segmento_tarifario_id IS NULL)
            OR
            (
                v_base.segmento_tarifario_id IS NOT NULL
                AND (
                    ct.segmento_tarifario_id = v_base.segmento_tarifario_id
                    OR ct.segmento_tarifario_id IS NULL
                )
            )
          )
    ORDER BY
        CASE
            WHEN ct.segmento_tarifario_id = v_base.segmento_tarifario_id THEN 0
            WHEN ct.segmento_tarifario_id IS NULL THEN 1
            ELSE 2
        END,
        CASE
            WHEN ct.condicion_medicion_id = v_base.condicion_medicion_id THEN 0
            WHEN ct.condicion_medicion_id = v_condicion_no_aplica_id THEN 1
            WHEN ct.condicion_medicion_id IS NULL THEN 2
            ELSE 3
        END,
        CASE
            WHEN ct.categoria_regulatoria_id = v_base.categoria_regulatoria_id THEN 0
            WHEN ct.categoria_regulatoria_id IS NULL THEN 1
            ELSE 2
        END,
        ct.prioridad ASC,
        ct.vigencia_desde DESC,
        ct.cuadro_tarifario_id DESC
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontro cuadro derivado para company_id=% cliente_servicio_base_id=% servicio_codigo=% fecha=%.',
            p_company_id, p_cliente_servicio_base_id, p_servicio_codigo, p_fecha_facturacion;
    END IF;

    SELECT EXISTS (
        SELECT 1
        FROM public.adm_regla_tarifaria rt
        JOIN public.adm_tipo_regla_tarifaria tr
          ON tr.company_id = rt.company_id
         AND tr.tipo_regla_tarifaria_id = rt.tipo_regla_tarifaria_id
        WHERE rt.company_id = p_company_id
          AND rt.cuadro_tarifario_id = v_cuadro.cuadro_tarifario_id
          AND rt.status_id = 1
          AND tr.codigo = 'RANGO_CONSUMO'
    )
    INTO v_requiere_consumo;

    IF v_requiere_consumo AND p_consumo IS NULL THEN
        RAISE EXCEPTION 'El cuadro derivado % requiere consumo y no se envio p_consumo.', v_cuadro.codigo;
    END IF;

    RETURN QUERY
    WITH reglas AS (
        SELECT
            rt.regla_tarifaria_id,
            rt.orden,
            rt.consumo_minimo,
            rt.consumo_maximo,
            rt.monto_fijo,
            rt.monto_unitario,
            rt.porcentaje,
            rt.servicio_referencia_id,
            rt.parametros,
            tr.codigo AS tipo_regla_codigo,
            tr.nombre AS tipo_regla_nombre,
            COALESCE(rt.parametros ->> 'modo_calculo', tr.codigo) AS modo_calculo,
            COALESCE((rt.parametros ->> 'alquiler')::numeric, 0)::numeric(18, 4) AS alquiler_regla,
            srv.codigo AS servicio_codigo,
            srv.nombre AS servicio_nombre,
            srv_ref.codigo AS servicio_referencia_codigo
        FROM public.adm_regla_tarifaria rt
        JOIN public.adm_tipo_regla_tarifaria tr
          ON tr.company_id = rt.company_id
         AND tr.tipo_regla_tarifaria_id = rt.tipo_regla_tarifaria_id
        JOIN public.adm_servicio srv
          ON srv.company_id = rt.company_id
         AND srv.servicio_id = v_cuadro.servicio_id
        LEFT JOIN public.adm_servicio srv_ref
          ON srv_ref.company_id = rt.company_id
         AND srv_ref.servicio_id = rt.servicio_referencia_id
        WHERE rt.company_id = p_company_id
          AND rt.cuadro_tarifario_id = v_cuadro.cuadro_tarifario_id
          AND rt.status_id = 1
    ),
    reglas_con_referencia AS (
        SELECT
            r.*,
            cs_ref.cliente_servicio_id AS cliente_servicio_referencia_id,
            cs_ref.condicion_medicion_id AS condicion_referencia_id
        FROM reglas r
        LEFT JOIN LATERAL (
            SELECT
                cs.cliente_servicio_id,
                cs.condicion_medicion_id
            FROM public.adm_cliente_servicio cs
            WHERE cs.company_id = p_company_id
              AND cs.cliente_id = v_base.cliente_id
              AND cs.servicio_id = r.servicio_referencia_id
              AND cs.status_id = 1
              AND p_fecha_facturacion >= cs.fecha_alta
              AND (cs.fecha_baja IS NULL OR p_fecha_facturacion <= cs.fecha_baja)
            ORDER BY
                CASE WHEN cs.cliente_servicio_id = v_base.cliente_servicio_id THEN 0 ELSE 1 END,
                cs.fecha_alta DESC,
                cs.cliente_servicio_id DESC
            LIMIT 1
        ) cs_ref ON true
    ),
    reglas_calculadas AS (
        SELECT
            r.*,
            CASE
                WHEN r.tipo_regla_codigo = 'MONTO_FIJO' THEN NULL::numeric(18, 4)
                WHEN r.tipo_regla_codigo = 'RANGO_CONSUMO'
                 AND r.modo_calculo = 'ACUMULADO_POR_RANGO_APC' THEN
                    CASE
                        WHEN p_consumo < COALESCE(r.consumo_minimo, 0) THEN 0::numeric(18, 4)
                        ELSE GREATEST(
                            LEAST(
                                p_consumo,
                                COALESCE(r.consumo_maximo, p_consumo)
                            ) - GREATEST(COALESCE(r.consumo_minimo, 0), 1) + 1,
                            0
                        )::numeric(18, 4)
                    END
                WHEN r.tipo_regla_codigo = 'RANGO_CONSUMO' THEN
                    CASE
                        WHEN p_consumo >= COALESCE(r.consumo_minimo, p_consumo)
                         AND (
                            r.consumo_maximo IS NULL
                            OR p_consumo <= r.consumo_maximo
                         ) THEN 1::numeric(18, 4)
                        ELSE 0::numeric(18, 4)
                    END
                ELSE NULL::numeric(18, 4)
            END AS unidades_aplicadas,
            CASE
                WHEN r.cliente_servicio_referencia_id IS NULL THEN 0::numeric(18, 4)
                ELSE (
                    SELECT COALESCE(SUM(rr.monto_calculado), 0)::numeric(18, 4)
                    FROM public.sp_adm_resolver_tarifa_cliente_servicio(
                        p_company_id,
                        r.cliente_servicio_referencia_id,
                        p_fecha_facturacion,
                        CASE
                            WHEN r.condicion_referencia_id = v_condicion_con_medicion_id THEN p_consumo
                            ELSE NULL
                        END
                    ) rr
                )
            END AS monto_referencia
        FROM reglas_con_referencia r
    ),
    reglas_aplicadas AS (
        SELECT *
        FROM reglas_calculadas rc
        WHERE rc.tipo_regla_codigo = 'MONTO_FIJO'
           OR (
                rc.tipo_regla_codigo = 'RANGO_CONSUMO'
                AND rc.modo_calculo = 'ACUMULADO_POR_RANGO_APC'
                AND COALESCE(rc.unidades_aplicadas, 0) > 0
              )
           OR (
                rc.tipo_regla_codigo = 'RANGO_CONSUMO'
                AND rc.modo_calculo <> 'ACUMULADO_POR_RANGO_APC'
                AND COALESCE(rc.unidades_aplicadas, 0) = 1
              )
           OR rc.tipo_regla_codigo IN ('PORCENTAJE_SERVICIO', 'REGLA_ESPECIAL')
    )
    SELECT
        v_base.cliente_servicio_id,
        v_base.cliente_id,
        v_cuadro.servicio_id,
        ra.servicio_codigo::text,
        ra.servicio_nombre::text,
        v_cuadro.cuadro_tarifario_id,
        v_cuadro.codigo::text,
        v_cuadro.nombre::text,
        ra.regla_tarifaria_id,
        ra.orden,
        ra.tipo_regla_codigo::text,
        ra.tipo_regla_nombre::text,
        ra.modo_calculo::text,
        p_consumo,
        ra.consumo_minimo,
        ra.consumo_maximo,
        ra.monto_fijo,
        ra.monto_unitario,
        ra.porcentaje,
        ra.unidades_aplicadas,
        CASE
            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
             AND (ra.modo_calculo = 'ACUMULADO_POR_RANGO_APC' OR ra.modo_calculo = 'TRAMO_DIRECTO_APC')
            THEN ra.alquiler_regla
            ELSE 0::numeric(18, 4)
        END AS alquiler_aplicado,
        COALESCE(ra.monto_referencia, 0)::numeric(18, 4),
        CASE
            WHEN ra.tipo_regla_codigo = 'MONTO_FIJO'
            THEN COALESCE(ra.monto_fijo, 0)::numeric(18, 4)

            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
             AND ra.modo_calculo = 'ACUMULADO_POR_RANGO_APC'
            THEN (
                COALESCE(ra.monto_fijo, 0)
                + COALESCE(ra.monto_unitario, 0) * COALESCE(ra.unidades_aplicadas, 0)
                + COALESCE(ra.alquiler_regla, 0)
            )::numeric(18, 4)

            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
             AND ra.modo_calculo = 'TRAMO_DIRECTO_APC'
            THEN (
                COALESCE(ra.monto_fijo, 0)
                + COALESCE(ra.monto_unitario, 0)
                + COALESCE(ra.alquiler_regla, 0)
            )::numeric(18, 4)

            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
            THEN (
                COALESCE(ra.monto_fijo, 0)
                + COALESCE(ra.monto_unitario, 0) * COALESCE(ra.unidades_aplicadas, 0)
            )::numeric(18, 4)

            WHEN ra.tipo_regla_codigo = 'PORCENTAJE_SERVICIO'
            THEN (
                COALESCE(ra.monto_referencia, 0) * COALESCE(ra.porcentaje, 0)
            )::numeric(18, 4)

            ELSE NULL::numeric(18, 4)
        END AS monto_calculado,
        ra.servicio_referencia_id,
        ra.servicio_referencia_codigo::text,
        ra.cliente_servicio_referencia_id,
        ra.parametros,
        CASE
            WHEN ra.tipo_regla_codigo = 'MONTO_FIJO'
            THEN 'Monto fijo directo'::text
            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
             AND ra.modo_calculo = 'ACUMULADO_POR_RANGO_APC'
            THEN 'Rango APC acumulado: base + cuota por tramo + alquiler si aplica'::text
            WHEN ra.tipo_regla_codigo = 'RANGO_CONSUMO'
             AND ra.modo_calculo = 'TRAMO_DIRECTO_APC'
            THEN 'Tramo APC directo: toma el valor del tramo resuelto'::text
            WHEN ra.tipo_regla_codigo = 'PORCENTAJE_SERVICIO'
             AND ra.cliente_servicio_referencia_id IS NULL
            THEN 'Porcentaje sobre servicio no asignado al cliente: monto 0'::text
            WHEN ra.tipo_regla_codigo = 'PORCENTAJE_SERVICIO'
            THEN 'Porcentaje sobre servicio base resuelto'::text
            WHEN ra.tipo_regla_codigo = 'REGLA_ESPECIAL'
            THEN 'Regla especial pendiente de implementar'::text
            ELSE 'Regla generica'::text
        END AS detalle_calculo
    FROM reglas_aplicadas ra
    ORDER BY ra.orden;
END;
$$;

COMMENT ON FUNCTION public.sp_adm_resolver_servicio_derivado_cliente_servicio(bigint, bigint, varchar, date, numeric)
IS 'Resuelve un servicio derivado desde el contexto de un adm_cliente_servicio base, incluyendo reglas porcentuales sobre otros servicios.';
