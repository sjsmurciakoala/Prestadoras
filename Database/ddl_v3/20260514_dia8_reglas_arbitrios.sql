-- =============================================================================
-- Sprint 3 Día 8 — Reglas regulatorias del Plan de Arbitrios PC 2026
-- Fecha: 2026-05-14
--
-- Contenido:
--   1. Tercera edad: aclarar semántica del tope (tope_mensual = 300, Art. 355).
--      El motor YA aplica el descuento 25% AGUA_POTABLE/DOMESTICO con tope; este
--      UPDATE solo deja explícito que el tope es MENSUAL (antes el JSON decía
--      tope_por_factura, que el SP igual usaba como mensual restando el acumulado).
--   2. Recargo por mora (Art. 130): tabla cfg_recargo_mora + cálculo en
--      sp_adm_calcular_factura_lectura (hoy v_recargos estaba hardcodeado a 0).
--
-- NO incluye descuento por pago anticipado (Art. 153, 10%): ese se aplica al
-- MOMENTO DEL PAGO en Captación de Pagos, y Captación está marcada para reforma
-- completa post-25. Se difiere a esa reforma (decisión 2026-05-14).
--
-- Idempotente.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Tercera edad — tope mensual explícito (Plan de Arbitrios Art. 355: L300/mes)
-- -----------------------------------------------------------------------------
UPDATE public.adm_ajuste_tarifario
SET parametros = jsonb_set(
        COALESCE(parametros, '{}'::jsonb),
        '{tope_mensual}',
        to_jsonb(300.00),
        true
    ),
    updated_at = now(),
    updated_by = current_user
WHERE condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
  AND NOT (parametros ? 'tope_mensual');

-- -----------------------------------------------------------------------------
-- 2. Recargo por mora — configuración por empresa
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_recargo_mora (
    company_id bigint PRIMARY KEY,
    tasa_mensual numeric(9, 6) NOT NULL DEFAULT 0,   -- fracción mensual sobre el saldo vencido (ej. 0.001667 = 2% anual)
    dias_gracia integer NOT NULL DEFAULT 0,          -- reservado: días de gracia antes de aplicar (uso futuro)
    descripcion varchar(200),
    activo boolean NOT NULL DEFAULT false,           -- inicia inactivo: la empresa lo activa cuando confirma la tasa
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(100) NOT NULL DEFAULT current_user,
    updated_at timestamptz,
    updated_by varchar(100),
    CONSTRAINT fk_cfg_recargo_mora_company
        FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id) ON DELETE CASCADE,
    CONSTRAINT ck_cfg_recargo_mora_tasa CHECK (tasa_mensual >= 0 AND tasa_mensual <= 1)
);

COMMENT ON TABLE public.cfg_recargo_mora IS
'Configuración del recargo por mora (Plan de Arbitrios PC 2026 Art. 130: 2% anual + interés activo bancario sobre saldos vencidos). tasa_mensual es la fracción mensual aplicada sobre el saldo previo. Inicia activo=false; la empresa la activa y fija la tasa real.';

-- Seed para cada empresa existente (inactivo, tasa de referencia Art. 130 = 2% anual / 12).
INSERT INTO public.cfg_recargo_mora (company_id, tasa_mensual, dias_gracia, descripcion, activo)
SELECT c.company_id, 0.001667, 0,
       'Recargo por mora — Plan de Arbitrios Art. 130 (2% anual sobre saldos vencidos). Ajustar tasa y activar.',
       false
FROM public.cfg_company c
ON CONFLICT (company_id) DO NOTHING;

-- -----------------------------------------------------------------------------
-- 3. sp_adm_calcular_factura_lectura — cálculo real de v_recargos
--    (idéntico a 20260507_sar_compliance_07, con el bloque de recargo agregado
--    tras resolver v_saldos_anteriores)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_calcular_factura_lectura(p_company_id bigint, p_anio integer, p_mes integer, p_cliente_id bigint, p_contador character varying DEFAULT NULL::character varying, p_fecha_lectura date DEFAULT CURRENT_DATE, p_lectura_actual numeric DEFAULT NULL::numeric, p_condicion_lectura character varying DEFAULT 'N'::character varying, p_lectura_promedio numeric DEFAULT NULL::numeric, p_usuario character varying DEFAULT NULL::character varying, p_observacion character varying DEFAULT NULL::character varying, p_id_cai integer DEFAULT NULL::integer, p_correlativo_cai integer DEFAULT NULL::integer, p_numero_factura character varying DEFAULT NULL::character varying, p_informativo character varying DEFAULT NULL::character varying)
 RETURNS TABLE(company_id bigint, cliente_id bigint, cliente_clave text, cliente_nombre text, anio integer, mes integer, contador text, ciclo text, ruta text, secuencia text, tiene_medidor boolean, condicion_lectura_aplicada text, lectura_anterior numeric, lectura_actual_efectiva numeric, consumo_facturable numeric, numero_factura text, id_cai integer, correlativo_cai integer, fecha_factura date, fecha_vencimiento date, subtotal_servicios numeric, subtotal_ajustes numeric, saldos_anteriores numeric, recargos numeric, total_factura numeric, taservi1 numeric, taservi2 numeric, taservi3 numeric, taservi4 numeric, detalle_servicios_json jsonb, warnings_json jsonb, snapshot_contract_version text)
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_cliente public.cliente_maestro%ROWTYPE;
    v_historico public.historicomedicion%ROWTYPE;
    v_fechavence date;
    v_diasvence integer;
    v_contador text;
    v_clave text;
    v_nombre text;
    v_tiene_medidor boolean;
    v_condicion text;
    v_lectura_anterior numeric := 0;
    v_lectura_actual_efectiva numeric := 0;
    v_consumo_facturable numeric := 0;
    v_subtotal_servicios numeric := 0;
    v_subtotal_ajustes numeric := 0;
    v_saldos_anteriores numeric := 0;
    v_recargos numeric := 0;
    v_tasa_mora numeric := 0;
    v_total_factura numeric := 0;
    v_descuento_tercera_edad_acumulado_mes numeric := 0;
    v_taservi1 numeric := 0;
    v_taservi2 numeric := 0;
    v_taservi3 numeric := 0;
    v_taservi4 numeric := 0;
    v_detalle_servicios jsonb := '[]'::jsonb;
    v_warnings jsonb := '[]'::jsonb;
    v_snapshot_contract_version text := 'LECTURA_V3_CONTRACT_1';
    v_periodo_abierto boolean := false;
    v_tiene_historico boolean := false;
    v_base_anchor_cliente_servicio_id bigint;
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

    v_clave := v_cliente.maestro_cliente_clave;
    v_nombre := v_cliente.maestro_cliente_nombre;
    v_tiene_medidor := COALESCE(v_cliente.maestro_cliente_tiene_medidor, false);
    v_contador := NULLIF(BTRIM(COALESCE(p_contador, v_cliente.contador)), '');
    v_condicion := UPPER(BTRIM(COALESCE(p_condicion_lectura, 'N')));

    IF p_id_cai IS NOT NULL THEN
        PERFORM 1
        FROM public.adm_cai_facturacion c
        WHERE c.company_id = p_company_id
          AND c.cai_id = p_id_cai
          AND c.status_id = 1
        LIMIT 1;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'No existe CAI V3 activo con id=% para company_id=%.', p_id_cai, p_company_id;
        END IF;
    END IF;

    SELECT *
    INTO v_historico
    FROM public.historicomedicion hm
    WHERE hm.company_id = p_company_id
      AND hm.ano = p_anio
      AND hm.mes = p_mes
      AND (
            (v_contador IS NOT NULL AND hm.contador = v_contador)
            OR hm.clave = v_clave
          )
    ORDER BY
        CASE WHEN v_contador IS NOT NULL AND hm.contador = v_contador THEN 0 ELSE 1 END,
        hm.ide DESC
    LIMIT 1;

    v_tiene_historico := FOUND;

    IF v_tiene_historico THEN
        v_lectura_anterior := COALESCE(v_historico.lect_ant, 0);

        SELECT cp.fechavence, cp.diasvence
        INTO v_fechavence, v_diasvence
        FROM public.calendariopro cp
        WHERE cp.ano = p_anio
          AND cp.mes = p_mes
          AND cp.ciclo = v_historico.ciclo
        ORDER BY cp.ide DESC
        LIMIT 1;

        SELECT EXISTS (
            SELECT 1
            FROM public.historialmes hm
            WHERE hm.ano = p_anio
              AND hm.mes = p_mes
              AND hm.ciclo = v_historico.ciclo
              AND UPPER(COALESCE(hm.cerrado, 'N')) NOT IN ('S', 'SI', '1', 'Y', 'TRUE', 'T')
        )
        INTO v_periodo_abierto;
    ELSE
        v_warnings := v_warnings || jsonb_build_array('SIN_HISTORICO_PERIODO');
    END IF;

    IF v_tiene_historico AND NOT v_periodo_abierto THEN
        RAISE EXCEPTION 'No hay periodo abierto para anio=% mes=% ciclo=%.',
            p_anio, p_mes, COALESCE(v_historico.ciclo, '(sin ciclo)');
    END IF;

    IF v_tiene_medidor THEN
        IF v_condicion IN ('MIN', 'PND') THEN
            v_consumo_facturable := 0;
            v_lectura_actual_efectiva := v_lectura_anterior;
        ELSIF v_condicion = 'PD' THEN
            v_consumo_facturable := GREATEST(COALESCE(p_lectura_promedio, 0), 0);
            v_lectura_actual_efectiva := v_lectura_anterior + v_consumo_facturable;
        ELSE
            v_lectura_actual_efectiva := COALESCE(p_lectura_actual, v_lectura_anterior);
            v_consumo_facturable := GREATEST(v_lectura_actual_efectiva - v_lectura_anterior, 0);
        END IF;
    ELSE
        v_lectura_actual_efectiva := COALESCE(p_lectura_actual, v_lectura_anterior, 0);
        v_consumo_facturable := 0;
    END IF;

    SELECT COALESCE((
        SELECT s.saldo_actual
        FROM public.sp_obtener_cliente_saldo(v_clave) s
        LIMIT 1
    ), 0)
    INTO v_saldos_anteriores;

    -- ── Recargo por mora (Plan de Arbitrios PC 2026 Art. 130) ──
    -- Si la empresa tiene configurado y ACTIVO el recargo, se aplica la tasa
    -- mensual sobre el saldo vencido. Mes a mes el recargo se acumula porque el
    -- saldo previo del periodo siguiente ya lo incluye.
    IF v_saldos_anteriores > 0 THEN
        SELECT COALESCE(rm.tasa_mensual, 0)
        INTO v_tasa_mora
        FROM public.cfg_recargo_mora rm
        WHERE rm.company_id = p_company_id
          AND rm.activo = true
        LIMIT 1;

        v_recargos := ROUND(COALESCE(v_saldos_anteriores, 0) * COALESCE(v_tasa_mora, 0), 4);

        IF v_recargos > 0 THEN
            v_warnings := v_warnings || jsonb_build_array('RECARGO_MORA_APLICADO');
        END IF;
    END IF;

    IF COALESCE(v_cliente.maestro_cliente_tercera_edad, false) THEN
        SELECT COALESCE(SUM(COALESCE(hm.descuentoapp, 0)), 0)
        INTO v_descuento_tercera_edad_acumulado_mes
        FROM public.historicomedicion hm
        WHERE hm.company_id = p_company_id
          AND hm.ano = p_anio
          AND hm.mes = p_mes
          AND hm.clave = v_clave
          AND (
                NULLIF(BTRIM(COALESCE(p_numero_factura, '')), '') IS NULL
                OR COALESCE(hm.numerofactura, '') <> NULLIF(BTRIM(COALESCE(p_numero_factura, '')), '')
              );
    END IF;

    SELECT cs.cliente_servicio_id
    INTO v_base_anchor_cliente_servicio_id
    FROM public.adm_cliente_servicio cs
    JOIN public.adm_servicio s
      ON s.company_id = cs.company_id
     AND s.servicio_id = cs.servicio_id
    WHERE cs.company_id = p_company_id
      AND cs.cliente_id = p_cliente_id
      AND cs.status_id = 1
      AND p_fecha_lectura >= cs.fecha_alta
      AND (cs.fecha_baja IS NULL OR p_fecha_lectura <= cs.fecha_baja)
      AND s.status_id = 1
      AND s.tipo_servicio_id IN (
            SELECT ts.tipo_servicio_id
            FROM public.adm_tipo_servicio ts
            WHERE ts.company_id = p_company_id
              AND ts.codigo = 'BASE'
      )
    ORDER BY CASE WHEN s.codigo = 'AGUA_POTABLE' THEN 0 ELSE 1 END,
             s.app_orden,
             cs.cliente_servicio_id
    LIMIT 1;

    IF v_base_anchor_cliente_servicio_id IS NULL THEN
        RAISE EXCEPTION 'El cliente % no tiene servicios base activos en adm_cliente_servicio.', v_clave;
    END IF;

    WITH base_services AS (
        SELECT
            cs.cliente_servicio_id,
            cs.cliente_id,
            s.servicio_id,
            s.codigo AS servicio_codigo,
            s.nombre AS servicio_nombre,
            ts.codigo AS tipo_servicio_codigo,
            s.app_orden,
            s.usa_condicion_medicion
        FROM public.adm_cliente_servicio cs
        JOIN public.adm_servicio s
          ON s.company_id = cs.company_id
         AND s.servicio_id = cs.servicio_id
        JOIN public.adm_tipo_servicio ts
          ON ts.company_id = s.company_id
         AND ts.tipo_servicio_id = s.tipo_servicio_id
        WHERE cs.company_id = p_company_id
          AND cs.cliente_id = p_cliente_id
          AND cs.status_id = 1
          AND p_fecha_lectura >= cs.fecha_alta
          AND (cs.fecha_baja IS NULL OR p_fecha_lectura <= cs.fecha_baja)
          AND s.status_id = 1
          AND ts.codigo = 'BASE'
    ),
    base_rule_rows AS (
        SELECT
            bs.cliente_servicio_id,
            bs.cliente_id,
            bs.servicio_id,
            bs.servicio_codigo,
            bs.servicio_nombre,
            bs.tipo_servicio_codigo,
            bs.app_orden,
            rr.cuadro_tarifario_id,
            rr.cuadro_codigo,
            rr.cuadro_nombre,
            rr.regla_tarifaria_id,
            rr.regla_orden,
            rr.tipo_regla_codigo,
            rr.tipo_regla_nombre,
            rr.modo_calculo,
            rr.consumo,
            rr.consumo_minimo,
            rr.consumo_maximo,
            rr.monto_fijo,
            rr.monto_unitario,
            rr.porcentaje,
            rr.unidades_aplicadas,
            rr.alquiler_aplicado,
            rr.monto_calculado,
            rr.servicio_referencia_id,
            rr.servicio_referencia_codigo,
            rr.parametros,
            rr.detalle_calculo,
            'BASE'::text AS origen_calculo
        FROM base_services bs
        CROSS JOIN LATERAL public.sp_adm_resolver_tarifa_cliente_servicio(
            p_company_id,
            bs.cliente_servicio_id,
            p_fecha_lectura,
            CASE
                WHEN bs.usa_condicion_medicion THEN v_consumo_facturable
                ELSE NULL
            END
        ) rr
    ),
    base_lines AS (
        SELECT
            br.cliente_servicio_id,
            br.cliente_id,
            br.servicio_id,
            br.servicio_codigo,
            br.servicio_nombre,
            br.tipo_servicio_codigo,
            br.app_orden,
            MIN(br.cuadro_tarifario_id) AS cuadro_tarifario_id,
            MIN(br.cuadro_codigo) AS cuadro_codigo,
            MIN(br.cuadro_nombre) AS cuadro_nombre,
            MIN(br.regla_tarifaria_id) AS regla_tarifaria_id,
            MIN(br.regla_orden) AS regla_orden,
            SUM(COALESCE(br.monto_calculado, 0))::numeric(18, 4) AS monto,
            false AS aplica_descuento,
            0::numeric(18, 4) AS monto_descuento,
            SUM(COALESCE(br.monto_calculado, 0))::numeric(18, 4) AS monto_final,
            'BASE'::text AS origen_calculo,
            jsonb_agg(
                jsonb_build_object(
                    'regla_tarifaria_id', br.regla_tarifaria_id,
                    'regla_orden', br.regla_orden,
                    'tipo_regla_codigo', br.tipo_regla_codigo,
                    'tipo_regla_nombre', br.tipo_regla_nombre,
                    'modo_calculo', br.modo_calculo,
                    'consumo', br.consumo,
                    'consumo_minimo', br.consumo_minimo,
                    'consumo_maximo', br.consumo_maximo,
                    'monto_fijo', br.monto_fijo,
                    'monto_unitario', br.monto_unitario,
                    'porcentaje', br.porcentaje,
                    'unidades_aplicadas', br.unidades_aplicadas,
                    'alquiler_aplicado', br.alquiler_aplicado,
                    'monto_calculado', br.monto_calculado,
                    'detalle_calculo', br.detalle_calculo
                )
                ORDER BY br.regla_orden, br.regla_tarifaria_id
            ) AS componentes
        FROM base_rule_rows br
        GROUP BY
            br.cliente_servicio_id,
            br.cliente_id,
            br.servicio_id,
            br.servicio_codigo,
            br.servicio_nombre,
            br.tipo_servicio_codigo,
            br.app_orden
    ),
    derived_services AS (
        SELECT
            s.servicio_id,
            s.codigo AS servicio_codigo,
            s.nombre AS servicio_nombre,
            ts.codigo AS tipo_servicio_codigo,
            s.app_orden
        FROM public.adm_servicio s
        JOIN public.adm_tipo_servicio ts
          ON ts.company_id = s.company_id
         AND ts.tipo_servicio_id = s.tipo_servicio_id
        WHERE s.company_id = p_company_id
          AND s.status_id = 1
          AND s.genera_por_regla = true
          AND ts.codigo = 'DERIVADO'
          AND s.codigo IN ('TASA_AMBIENTAL', 'TASA_SVA_ERSAPS')
    ),
    derived_rule_rows AS (
        SELECT
            ds.servicio_id,
            ds.servicio_codigo,
            ds.servicio_nombre,
            ds.tipo_servicio_codigo,
            ds.app_orden,
            rr.cuadro_tarifario_id,
            rr.cuadro_codigo,
            rr.cuadro_nombre,
            rr.regla_tarifaria_id,
            rr.regla_orden,
            rr.tipo_regla_codigo,
            rr.tipo_regla_nombre,
            rr.modo_calculo,
            rr.consumo,
            rr.consumo_minimo,
            rr.consumo_maximo,
            rr.monto_fijo,
            rr.monto_unitario,
            rr.porcentaje,
            rr.unidades_aplicadas,
            rr.alquiler_aplicado,
            rr.monto_referencia,
            rr.monto_calculado,
            rr.servicio_referencia_id,
            rr.servicio_referencia_codigo,
            rr.cliente_servicio_referencia_id,
            rr.parametros,
            rr.detalle_calculo,
            'DERIVADO'::text AS origen_calculo
        FROM derived_services ds
        CROSS JOIN LATERAL public.sp_adm_resolver_servicio_derivado_cliente_servicio(
            p_company_id,
            v_base_anchor_cliente_servicio_id,
            ds.servicio_codigo,
            p_fecha_lectura,
            v_consumo_facturable
        ) rr
    ),
    derived_lines AS (
        SELECT
            NULL::bigint AS cliente_servicio_id,
            p_cliente_id AS cliente_id,
            dr.servicio_id,
            dr.servicio_codigo,
            dr.servicio_nombre,
            dr.tipo_servicio_codigo,
            dr.app_orden,
            MIN(dr.cuadro_tarifario_id) AS cuadro_tarifario_id,
            MIN(dr.cuadro_codigo) AS cuadro_codigo,
            MIN(dr.cuadro_nombre) AS cuadro_nombre,
            MIN(dr.regla_tarifaria_id) AS regla_tarifaria_id,
            MIN(dr.regla_orden) AS regla_orden,
            SUM(COALESCE(dr.monto_calculado, 0))::numeric(18, 4) AS monto,
            false AS aplica_descuento,
            0::numeric(18, 4) AS monto_descuento,
            SUM(COALESCE(dr.monto_calculado, 0))::numeric(18, 4) AS monto_final,
            'DERIVADO'::text AS origen_calculo,
            jsonb_agg(
                jsonb_build_object(
                    'regla_tarifaria_id', dr.regla_tarifaria_id,
                    'regla_orden', dr.regla_orden,
                    'tipo_regla_codigo', dr.tipo_regla_codigo,
                    'tipo_regla_nombre', dr.tipo_regla_nombre,
                    'modo_calculo', dr.modo_calculo,
                    'consumo', dr.consumo,
                    'consumo_minimo', dr.consumo_minimo,
                    'consumo_maximo', dr.consumo_maximo,
                    'monto_fijo', dr.monto_fijo,
                    'monto_unitario', dr.monto_unitario,
                    'porcentaje', dr.porcentaje,
                    'unidades_aplicadas', dr.unidades_aplicadas,
                    'alquiler_aplicado', dr.alquiler_aplicado,
                    'monto_referencia', dr.monto_referencia,
                    'monto_calculado', dr.monto_calculado,
                    'servicio_referencia_id', dr.servicio_referencia_id,
                    'servicio_referencia_codigo', dr.servicio_referencia_codigo,
                    'cliente_servicio_referencia_id', dr.cliente_servicio_referencia_id,
                    'detalle_calculo', dr.detalle_calculo
                )
                ORDER BY dr.regla_orden, dr.regla_tarifaria_id
            ) AS componentes
        FROM derived_rule_rows dr
        GROUP BY
            dr.servicio_id,
            dr.servicio_codigo,
            dr.servicio_nombre,
            dr.tipo_servicio_codigo,
            dr.app_orden
    ),
    all_lines AS (
        SELECT * FROM base_lines
        UNION ALL
        SELECT * FROM derived_lines
    ),
    line_adjustments AS (
        SELECT
            al.cliente_servicio_id,
            al.servicio_id,
            al.origen_calculo,
            LEAST(
                COALESCE(
                    SUM(
                        CASE
                            WHEN aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
                                 AND tat.codigo IN ('DESCUENTO', 'BENEFICIO_ESPECIAL') THEN
                                LEAST(
                                    COALESCE(aj.monto_fijo, 0)
                                    + CASE
                                        WHEN COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) IS NOT NULL
                                            THEN al.monto * (COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) / 100.0)
                                        ELSE 0
                                      END,
                                    COALESCE(
                                        NULLIF((aj.parametros ->> 'tope_por_factura')::numeric, 0),
                                        aj.tope_maximo,
                                        999999999::numeric
                                    )
                                )
                            ELSE 0
                        END
                    ),
                    0
                ),
                al.monto
            )::numeric(18, 4) AS monto_descuento_potencial_tercera_edad,
            LEAST(
                COALESCE(
                    SUM(
                        CASE
                            WHEN aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
                                 AND tat.codigo IN ('DESCUENTO', 'BENEFICIO_ESPECIAL') THEN
                                LEAST(
                                    LEAST(
                                        COALESCE(aj.monto_fijo, 0)
                                        + CASE
                                            WHEN COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) IS NOT NULL
                                                THEN al.monto * (COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) / 100.0)
                                            ELSE 0
                                          END,
                                        COALESCE(
                                            NULLIF((aj.parametros ->> 'tope_por_factura')::numeric, 0),
                                            aj.tope_maximo,
                                            999999999::numeric
                                        )
                                    ),
                                    GREATEST(
                                        COALESCE(
                                            NULLIF((aj.parametros ->> 'tope_mensual')::numeric, 0),
                                            NULLIF((aj.parametros ->> 'tope_por_factura')::numeric, 0),
                                            aj.tope_maximo,
                                            999999999::numeric
                                        ) - v_descuento_tercera_edad_acumulado_mes,
                                        0
                                    )
                                )
                            ELSE 0
                        END
                    ),
                    0
                ),
                al.monto
            )::numeric(18, 4) AS monto_descuento_aplicado_tercera_edad,
            LEAST(
                COALESCE(
                    SUM(
                        CASE
                            WHEN tat.codigo IN ('DESCUENTO', 'BENEFICIO_ESPECIAL') THEN
                                CASE
                                    WHEN aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO' THEN
                                        LEAST(
                                            LEAST(
                                                COALESCE(aj.monto_fijo, 0)
                                                + CASE
                                                    WHEN COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) IS NOT NULL
                                                        THEN al.monto * (COALESCE(NULLIF(v_cliente.descuento_tercera_edad, 0), aj.porcentaje) / 100.0)
                                                    ELSE 0
                                                  END,
                                                COALESCE(
                                                    NULLIF((aj.parametros ->> 'tope_por_factura')::numeric, 0),
                                                    aj.tope_maximo,
                                                    999999999::numeric
                                                )
                                            ),
                                            GREATEST(
                                                COALESCE(
                                                    NULLIF((aj.parametros ->> 'tope_mensual')::numeric, 0),
                                                    NULLIF((aj.parametros ->> 'tope_por_factura')::numeric, 0),
                                                    aj.tope_maximo,
                                                    999999999::numeric
                                                ) - v_descuento_tercera_edad_acumulado_mes,
                                                0
                                            )
                                        )
                                    ELSE
                                        LEAST(
                                            COALESCE(aj.monto_fijo, 0)
                                            + CASE
                                                WHEN aj.porcentaje IS NOT NULL
                                                    THEN al.monto * (aj.porcentaje / 100.0)
                                                ELSE 0
                                              END,
                                            COALESCE(aj.tope_maximo, 999999999::numeric)
                                        )
                                END
                            WHEN tat.codigo = 'EXONERACION' THEN
                                al.monto
                            ELSE 0
                        END
                    ),
                    0
                ),
                al.monto
            )::numeric(18, 4) AS monto_descuento
        FROM all_lines al
        JOIN public.adm_ajuste_tarifario aj
          ON aj.company_id = p_company_id
         AND aj.cuadro_tarifario_id = al.cuadro_tarifario_id
         AND aj.status_id = 1
        JOIN public.adm_tipo_ajuste_tarifario tat
          ON tat.company_id = aj.company_id
         AND tat.tipo_ajuste_tarifario_id = aj.tipo_ajuste_tarifario_id
        LEFT JOIN public.adm_cliente_servicio cs
          ON cs.company_id = p_company_id
         AND cs.cliente_servicio_id = al.cliente_servicio_id
        LEFT JOIN public.adm_categoria_regulatoria cr
          ON cr.company_id = cs.company_id
         AND cr.categoria_regulatoria_id = cs.categoria_regulatoria_id
        WHERE
            aj.condicion_codigo IS NULL
            OR aj.condicion_codigo = ''
            OR (
                aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
                AND COALESCE(v_cliente.maestro_cliente_tercera_edad, false)
                AND al.servicio_codigo = 'AGUA_POTABLE'
                AND COALESCE(cr.codigo, '') = 'DOMESTICO'
            )
        GROUP BY
            al.cliente_servicio_id,
            al.servicio_id,
            al.origen_calculo,
            al.monto
    ),
    adjusted_lines AS (
        SELECT
            al.cliente_servicio_id,
            al.cliente_id,
            al.servicio_id,
            al.servicio_codigo,
            al.servicio_nombre,
            al.tipo_servicio_codigo,
            al.app_orden,
            al.cuadro_tarifario_id,
            al.cuadro_codigo,
            al.cuadro_nombre,
            al.regla_tarifaria_id,
            al.regla_orden,
            al.monto,
            (COALESCE(la.monto_descuento, 0) > 0) AS aplica_descuento,
            COALESCE(la.monto_descuento_potencial_tercera_edad, 0)::numeric(18, 4) AS monto_descuento_potencial_tercera_edad,
            COALESCE(la.monto_descuento_aplicado_tercera_edad, 0)::numeric(18, 4) AS monto_descuento_aplicado_tercera_edad,
            COALESCE(la.monto_descuento, 0)::numeric(18, 4) AS monto_descuento,
            GREATEST(al.monto - COALESCE(la.monto_descuento, 0), 0)::numeric(18, 4) AS monto_final,
            al.origen_calculo,
            al.componentes
        FROM all_lines al
        LEFT JOIN line_adjustments la
         ON COALESCE(la.cliente_servicio_id, -1) = COALESCE(al.cliente_servicio_id, -1)
         AND la.servicio_id = al.servicio_id
         AND la.origen_calculo = al.origen_calculo
    ),
    warning_flags AS (
        SELECT
            COALESCE(SUM(al.monto_descuento_potencial_tercera_edad), 0)::numeric(18, 4) AS descuento_tercera_edad_potencial,
            COALESCE(SUM(al.monto_descuento_aplicado_tercera_edad), 0)::numeric(18, 4) AS descuento_tercera_edad_aplicado
        FROM adjusted_lines al
    ),
    totals AS (
        SELECT
            COALESCE(SUM(al.monto_final), 0)::numeric(18, 4) AS subtotal_servicios,
            COALESCE(SUM(al.monto_descuento), 0)::numeric(18, 4) AS subtotal_ajustes,
            COALESCE(MAX(CASE WHEN al.servicio_codigo = 'AGUA_POTABLE' THEN al.monto_final END), 0)::numeric(18, 4) AS taservi1,
            COALESCE(MAX(CASE WHEN al.servicio_codigo = 'ALCANTARILLADO' THEN al.monto_final END), 0)::numeric(18, 4) AS taservi2,
            COALESCE(MAX(CASE WHEN al.servicio_codigo = 'TASA_AMBIENTAL' THEN al.monto_final END), 0)::numeric(18, 4) AS taservi3,
            COALESCE(MAX(CASE WHEN al.servicio_codigo = 'TASA_SVA_ERSAPS' THEN al.monto_final END), 0)::numeric(18, 4) AS taservi4
        FROM adjusted_lines al
    ),
    detail_json AS (
        SELECT COALESCE(
            jsonb_agg(
                jsonb_build_object(
                    'servicio_codigo', al.servicio_codigo,
                    'servicio_nombre', al.servicio_nombre,
                    'tipo_servicio', al.tipo_servicio_codigo,
                    'origen_calculo', al.origen_calculo,
                    'cliente_servicio_id', al.cliente_servicio_id,
                    'cuadro_tarifario_id', al.cuadro_tarifario_id,
                    'cuadro_codigo', al.cuadro_codigo,
                    'cuadro_nombre', al.cuadro_nombre,
                    'regla_tarifaria_id', al.regla_tarifaria_id,
                    'cantidad', CASE
                        WHEN al.servicio_codigo = 'AGUA_POTABLE' AND v_tiene_medidor THEN v_consumo_facturable
                        ELSE 1
                    END,
                    'monto', al.monto,
                    'aplica_descuento', al.aplica_descuento,
                    'monto_descuento', al.monto_descuento,
                    'monto_final', al.monto_final,
                    'componentes', al.componentes
                )
                ORDER BY al.app_orden, al.servicio_nombre, al.servicio_codigo
            ),
            '[]'::jsonb
        ) AS detalle_servicios_json
        FROM adjusted_lines al
    )
    SELECT
        p_company_id,
        p_cliente_id,
        v_clave::text,
        v_nombre::text,
        p_anio,
        p_mes,
        v_contador::text,
        COALESCE(v_historico.ciclo, '')::text,
        COALESCE(v_historico.ruta, '')::text,
        COALESCE(v_historico.secuencia, '')::text,
        v_tiene_medidor,
        v_condicion::text,
        v_lectura_anterior,
        v_lectura_actual_efectiva,
        v_consumo_facturable,
        COALESCE(p_numero_factura, '')::text,
        p_id_cai,
        p_correlativo_cai,
        COALESCE(p_fecha_lectura, current_date),
        v_fechavence,
        t.subtotal_servicios,
        t.subtotal_ajustes,
        v_saldos_anteriores,
        v_recargos,
        (t.subtotal_servicios + v_saldos_anteriores + v_recargos)::numeric(18, 4),
        t.taservi1,
        t.taservi2,
        t.taservi3,
        t.taservi4,
        dj.detalle_servicios_json,
        v_warnings
            || CASE
                WHEN wf.descuento_tercera_edad_potencial > 0
                     AND wf.descuento_tercera_edad_aplicado = 0
                    THEN jsonb_build_array('TOPE_TERCERA_EDAD_AGOTADO')
                ELSE '[]'::jsonb
               END
            || CASE
                WHEN wf.descuento_tercera_edad_potencial > wf.descuento_tercera_edad_aplicado
                     AND wf.descuento_tercera_edad_aplicado > 0
                    THEN jsonb_build_array('TOPE_TERCERA_EDAD_PARCIAL')
                ELSE '[]'::jsonb
               END,
        v_snapshot_contract_version
    INTO
        company_id,
        cliente_id,
        cliente_clave,
        cliente_nombre,
        anio,
        mes,
        contador,
        ciclo,
        ruta,
        secuencia,
        tiene_medidor,
        condicion_lectura_aplicada,
        lectura_anterior,
        lectura_actual_efectiva,
        consumo_facturable,
        numero_factura,
        id_cai,
        correlativo_cai,
        fecha_factura,
        fecha_vencimiento,
        subtotal_servicios,
        subtotal_ajustes,
        saldos_anteriores,
        recargos,
        total_factura,
        taservi1,
        taservi2,
        taservi3,
        taservi4,
        detalle_servicios_json,
        warnings_json,
        snapshot_contract_version
    FROM totals t
    CROSS JOIN detail_json dj
    CROSS JOIN warning_flags wf;

    RETURN NEXT;
END;
$function$;
