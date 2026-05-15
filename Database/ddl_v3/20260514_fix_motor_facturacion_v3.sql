-- =============================================================================
-- Fix motor de facturación V3 — implementar PORCENTAJE_SERVICIO en SP BASE
-- Fecha: 2026-05-14
-- Plan: PLAN_ENTREGA_2026-05-25.md Sprint 3 dia 1
-- Bugs: BUGS_MOTOR_FACTURACION_2026-05-13.md
--
-- Cambios:
--   1. Normalizar regla 29 (ALCANTARILLADO) -> porcentaje decimal 0.60 (era 60.00).
--      Convencion del proyecto: porcentaje SIEMPRE en formato decimal 0..1.
--   2. sp_adm_resolver_tarifa_cliente_servicio (BASE): implementar PORCENTAJE_SERVICIO.
--      Antes devolvia NULL. Ahora calcula monto_referencia × porcentaje
--      (mismo patron que el SP de DERIVADOS, lineas 355-358 del archivo original).
--   3. PRESERVA la firma original 100% (mismo RETURNS TABLE, mismos parametros).
--      Solo cambia el body interno.
--
-- Idempotente.
-- =============================================================================

BEGIN;

-- 1. Fix dato: regla 29 ALCANTARILLADO DOMESTICA_BAJA porcentaje 60.00 -> 0.60
UPDATE public.adm_regla_tarifaria
   SET porcentaje = 0.600000
 WHERE regla_tarifaria_id = 29
   AND porcentaje > 1.0;

-- Verificar: NO deberian quedar reglas con porcentaje > 1 en Empresa Demo
SELECT 'reglas con porcentaje > 1 (deben ser 0)' AS check,
       COUNT(*) AS qty
FROM public.adm_regla_tarifaria
WHERE company_id = 2 AND status_id = 1 AND porcentaje > 1.0;

-- 2. DROP + CREATE del SP de BASE preservando firma original 100%.
--    (DROP necesario porque la version intermedia en BD tuvo otra firma.)

DROP FUNCTION IF EXISTS public.sp_adm_resolver_tarifa_cliente_servicio(bigint, bigint, date, numeric);

CREATE OR REPLACE FUNCTION public.sp_adm_resolver_tarifa_cliente_servicio(
    p_company_id bigint,
    p_cliente_servicio_id bigint,
    p_fecha_facturacion date,
    p_consumo numeric(18, 4) DEFAULT NULL
)
RETURNS TABLE
(
    cliente_servicio_id bigint,
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
    monto_calculado numeric(18, 4),
    servicio_referencia_id bigint,
    servicio_referencia_codigo text,
    parametros jsonb,
    detalle_calculo text
)
LANGUAGE plpgsql
STABLE
AS
$$
DECLARE
    v_cliente_servicio public.adm_cliente_servicio%ROWTYPE;
    v_cuadro public.adm_cuadro_tarifario%ROWTYPE;
    v_requiere_consumo boolean;
BEGIN
    SELECT *
    INTO v_cliente_servicio
    FROM public.adm_cliente_servicio cs
    WHERE cs.company_id = p_company_id
      AND cs.cliente_servicio_id = p_cliente_servicio_id
      AND cs.status_id = 1
      AND p_fecha_facturacion >= cs.fecha_alta
      AND (cs.fecha_baja IS NULL OR p_fecha_facturacion <= cs.fecha_baja);

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe adm_cliente_servicio activo para company_id=% y cliente_servicio_id=% en fecha %.',
            p_company_id, p_cliente_servicio_id, p_fecha_facturacion;
    END IF;

    SELECT ct.*
    INTO v_cuadro
    FROM public.adm_cuadro_tarifario ct
    WHERE ct.company_id = p_company_id
      AND ct.status_id = 1
      AND p_fecha_facturacion >= ct.vigencia_desde
      AND (ct.vigencia_hasta IS NULL OR p_fecha_facturacion <= ct.vigencia_hasta)
      AND (
            (
                v_cliente_servicio.cuadro_tarifario_id IS NOT NULL
                AND ct.cuadro_tarifario_id = v_cliente_servicio.cuadro_tarifario_id
            )
            OR
            (
                v_cliente_servicio.cuadro_tarifario_id IS NULL
                AND ct.servicio_id = v_cliente_servicio.servicio_id
                AND (ct.categoria_regulatoria_id IS NULL OR ct.categoria_regulatoria_id = v_cliente_servicio.categoria_regulatoria_id)
                AND (ct.condicion_medicion_id IS NULL OR ct.condicion_medicion_id = v_cliente_servicio.condicion_medicion_id)
                AND (
                    (v_cliente_servicio.segmento_tarifario_id IS NULL AND ct.segmento_tarifario_id IS NULL)
                    OR
                    (
                        v_cliente_servicio.segmento_tarifario_id IS NOT NULL
                        AND (
                            ct.segmento_tarifario_id = v_cliente_servicio.segmento_tarifario_id
                            OR ct.segmento_tarifario_id IS NULL
                        )
                    )
                )
            )
          )
    ORDER BY
        CASE
            WHEN v_cliente_servicio.cuadro_tarifario_id IS NOT NULL
             AND ct.cuadro_tarifario_id = v_cliente_servicio.cuadro_tarifario_id THEN 0
            ELSE 1
        END,
        CASE
            WHEN ct.segmento_tarifario_id = v_cliente_servicio.segmento_tarifario_id THEN 0
            WHEN ct.segmento_tarifario_id IS NULL THEN 1
            ELSE 2
        END,
        CASE
            WHEN ct.condicion_medicion_id = v_cliente_servicio.condicion_medicion_id THEN 0
            WHEN ct.condicion_medicion_id IS NULL THEN 1
            ELSE 2
        END,
        CASE
            WHEN ct.categoria_regulatoria_id = v_cliente_servicio.categoria_regulatoria_id THEN 0
            WHEN ct.categoria_regulatoria_id IS NULL THEN 1
            ELSE 2
        END,
        ct.prioridad ASC,
        ct.vigencia_desde DESC,
        ct.cuadro_tarifario_id DESC
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontro cuadro tarifario para company_id=% cliente_servicio_id=% servicio_id=% fecha=%.',
            p_company_id, p_cliente_servicio_id, v_cliente_servicio.servicio_id, p_fecha_facturacion;
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
        RAISE EXCEPTION 'El cuadro tarifario % requiere consumo y no se envio p_consumo.', v_cuadro.codigo;
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
            -- BUGFIX 2026-05-14: PORCENTAJE_SERVICIO requiere conocer el monto del
            -- servicio referenciado. Lo calculamos en linea via LATERAL al mismo SP.
            -- Para evitar loop infinito en deps mal modeladas, asumimos que el
            -- servicio referenciado NO tiene a su vez una regla PORCENTAJE_SERVICIO
            -- que apunte de regreso (regla del proyecto: deps unidireccionales).
            CASE
                WHEN r.tipo_regla_codigo = 'PORCENTAJE_SERVICIO'
                 AND r.servicio_referencia_id IS NOT NULL
                THEN (
                    SELECT COALESCE(SUM(rr.monto_calculado), 0)::numeric(18, 4)
                    FROM public.adm_cliente_servicio cs_ref
                    CROSS JOIN LATERAL public.sp_adm_resolver_tarifa_cliente_servicio(
                        p_company_id,
                        cs_ref.cliente_servicio_id,
                        p_fecha_facturacion,
                        p_consumo
                    ) rr
                    WHERE cs_ref.company_id = p_company_id
                      AND cs_ref.cliente_id = v_cliente_servicio.cliente_id
                      AND cs_ref.servicio_id = r.servicio_referencia_id
                      AND cs_ref.status_id = 1
                      AND p_fecha_facturacion >= cs_ref.fecha_alta
                      AND (cs_ref.fecha_baja IS NULL OR p_fecha_facturacion <= cs_ref.fecha_baja)
                )
                ELSE 0::numeric(18, 4)
            END AS monto_referencia
        FROM reglas r
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
        v_cliente_servicio.cliente_servicio_id,
        v_cliente_servicio.cliente_id,
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

            -- BUGFIX 2026-05-14: PORCENTAJE_SERVICIO antes devolvia NULL.
            -- Ahora calcula monto_referencia × porcentaje (mismo patron que el SP de DERIVADOS).
            WHEN ra.tipo_regla_codigo = 'PORCENTAJE_SERVICIO'
            THEN (
                COALESCE(ra.monto_referencia, 0) * COALESCE(ra.porcentaje, 0)
            )::numeric(18, 4)

            ELSE NULL::numeric(18, 4)
        END AS monto_calculado,
        ra.servicio_referencia_id,
        ra.servicio_referencia_codigo::text,
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
            THEN 'Porcentaje (decimal 0..1) sobre servicio referenciado resuelto'::text
            WHEN ra.tipo_regla_codigo = 'REGLA_ESPECIAL'
            THEN 'Regla especial pendiente de implementar'::text
            ELSE 'Regla generica'::text
        END AS detalle_calculo
    FROM reglas_aplicadas ra
    ORDER BY ra.orden;
END;
$$;

COMMENT ON FUNCTION public.sp_adm_resolver_tarifa_cliente_servicio(bigint, bigint, date, numeric)
IS 'Resuelve el cuadro tarifario y devuelve las reglas aplicadas. v2 (2026-05-14): soporta PORCENTAJE_SERVICIO usando servicio referenciado del mismo cliente. Porcentaje en formato decimal 0..1.';

COMMIT;

-- =============================================================================
-- Test E2E: cliente Jessel (LZV2CF1JFW) debe dar 331.11
-- =============================================================================
-- AGUA_POTABLE      MONTO_FIJO 199.27          -> 199.27
-- ALCANTARILLADO    60% AGUA = 199.27 × 0.60   -> 119.5620
-- TASA_AMBIENTAL    MONTO_FIJO 5.90            -> 5.90
-- TASA_SVA_ERSAPS   2% AGUA + 2% ALCANT
--                   3.9854 + 2.3912            -> 6.3766
-- TOTAL                                        -> 331.11

SELECT 'AGUA_POTABLE' AS srv, monto_calculado
FROM sp_adm_resolver_tarifa_cliente_servicio(2::bigint, 26::bigint, CURRENT_DATE, 0::numeric)
WHERE tipo_regla_codigo = 'MONTO_FIJO'
UNION ALL
SELECT 'ALCANTARILLADO', monto_calculado
FROM sp_adm_resolver_tarifa_cliente_servicio(2::bigint, 5::bigint, CURRENT_DATE, 0::numeric)
WHERE tipo_regla_codigo = 'PORCENTAJE_SERVICIO';
