-- =============================================================================
-- 2026-07-14  Calendario de facturación (Fase A del plan apertura-ciclo-único)
-- Plan: docs/plans/2026-07-14-plan-apertura-ciclo-unico.md
-- Rama: feat/calendario-facturacion
-- -----------------------------------------------------------------------------
-- PROBLEMA: calendariopro (el calendario de facturación por año/mes/ciclo que
-- SIMAFI mantiene vivo: fechas de lectura, facturación y vencimiento por ciclo)
-- está VACÍA en SIAD. sp_lectura_v3 y sp_adm_calcular_factura_lectura la leen
-- para poner fechavence/plazo en la factura → hoy las facturas V3 salen con
-- fechavence NULL. Además la tabla no era multitenant (regla #1 del repo).
--
-- CONTENIDO:
--   1. calendariopro multitenant: company_id NOT NULL (backfill 2 = APC) +
--      UNIQUE (company_id, ano, mes, ciclo) para carga/upserts idempotentes.
--      ano/mes/ciclo pasan a NOT NULL (la tabla está vacía en SIAD; en 0.9 no
--      hay carga previa — el backfill cubre cualquier fila huérfana).
--   2. Repunte de sp_adm_calcular_factura_lectura: la lectura de calendariopro
--      filtra por company_id y el match de ciclo tolera '1' vs '01' (SIMAFI
--      guarda el ciclo SIN cero a la izquierda; la planilla V3 lo normaliza a
--      2 dígitos — mismo patrón numérico-tolerante que
--      fn_adm_periodo_ciclo_rutas_pendientes de F7). ÚNICO cambio; el resto
--      del cuerpo se preserva byte-a-byte respecto a
--      20260707_fix_saldo_cross_company_calcular.sql.
--   3. Repunte de sp_lectura_v3: mismo cambio puntual sobre la definición de
--      20260709_fix_sp_lectura_v3_montovalor_saldo.sql.
--
-- La CARGA de datos desde SIMAFI (bdsimafi.calendariopro, 2,541 filas
-- 2016-2026) va en el script hermano 20260714_carga_calendariopro_simafi.sql.
--
-- Idempotente. NO ejecutar en producción fuera de la ventana de deploy.
-- =============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- 1. calendariopro multitenant
-- ----------------------------------------------------------------------------

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'calendariopro'
          AND column_name = 'company_id'
    ) THEN
        ALTER TABLE public.calendariopro ADD COLUMN company_id bigint;
    END IF;
END $$;

-- Backfill: toda fila existente pertenece a APC (única empresa con comercial).
UPDATE public.calendariopro SET company_id = 2 WHERE company_id IS NULL;

-- Filas sin período no tienen sentido en el calendario (tabla vacía en SIAD;
-- por si acaso, se eliminan antes de endurecer las columnas).
DELETE FROM public.calendariopro
WHERE ano IS NULL OR mes IS NULL OR NULLIF(btrim(ciclo), '') IS NULL;

ALTER TABLE public.calendariopro ALTER COLUMN company_id SET NOT NULL;
ALTER TABLE public.calendariopro ALTER COLUMN ano SET NOT NULL;
ALTER TABLE public.calendariopro ALTER COLUMN mes SET NOT NULL;
ALTER TABLE public.calendariopro ALTER COLUMN ciclo SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_calendariopro_company'
    ) THEN
        ALTER TABLE public.calendariopro
            ADD CONSTRAINT fk_calendariopro_company
            FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id);
    END IF;
END $$;

-- El ciclo se guarda normalizado a 2 dígitos ('01'..'21') — la carga desde
-- SIMAFI hace lpad. La unicidad es por período de empresa.
CREATE UNIQUE INDEX IF NOT EXISTS ux_calendariopro_company_periodo
    ON public.calendariopro (company_id, ano, mes, btrim(ciclo));

-- El índice único compara el ciclo como TEXTO ('1' <> '01') pero los SPs lo
-- casan por valor numérico ('1' == '01'): sin este CHECK podrían coexistir
-- '1' y '01' para el mismo período (únicas para el índice, iguales para el
-- match) y el LIMIT 1 del SP elegiría un calendario arbitrario. Se exige que
-- todo ciclo numérico entre ya normalizado a exactamente 2 dígitos.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_calendariopro_ciclo_normalizado'
    ) THEN
        ALTER TABLE public.calendariopro
            ADD CONSTRAINT ck_calendariopro_ciclo_normalizado
            CHECK (btrim(ciclo) !~ '^[0-9]+$' OR btrim(ciclo) ~ '^[0-9]{2}$');
    END IF;
END $$;

COMMENT ON TABLE public.calendariopro IS
'Calendario de facturación por empresa/año/mes/ciclo (Fase A apertura-ciclo-único, 2026-07-14): fechas de lectura (fechalec), facturación (fechafac), refacturación y vencimiento (fechavence + diasvence) de cada ciclo. Fuente original: SIMAFI. Lo leen sp_lectura_v3 y sp_adm_calcular_factura_lectura para fechavence/plazo de la factura.';

-- ----------------------------------------------------------------------------
-- 2. sp_adm_calcular_factura_lectura — calendariopro company-scoped
--    (definición completa; único cambio: el SELECT de calendariopro)
-- ----------------------------------------------------------------------------

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

        -- Fase A (2026-07-14): calendariopro es multitenant — filtro por
        -- company_id. El match de ciclo tolera '1' vs '01' (SIMAFI sin cero a
        -- la izquierda vs planilla V3 normalizada a 2 dígitos).
        SELECT cp.fechavence, cp.diasvence
        INTO v_fechavence, v_diasvence
        FROM public.calendariopro cp
        WHERE cp.company_id = p_company_id
          AND cp.ano = p_anio
          AND cp.mes = p_mes
          AND (
                btrim(cp.ciclo) = btrim(v_historico.ciclo)
                OR (cp.ciclo ~ '^[0-9]+$' AND v_historico.ciclo ~ '^[0-9]+$'
                    AND cp.ciclo::int = v_historico.ciclo::int)
              )
        ORDER BY cp.ide DESC
        LIMIT 1;

        -- F7 (2026-07-04): el período comercial vive en adm_periodo_comercial(_ciclo);
        -- historialmes queda como espejo de solo lectura durante la transición.
        -- (El chequeo legacy sobre historialmes trataba cerrado='C' como abierto
        --  porque su lista de letras no incluía 'C'; desde F7 el cierre bloquea.)
        v_periodo_abierto := public.fn_adm_periodo_comercial_ciclo_abierto(
            p_company_id, p_anio, p_mes, v_historico.ciclo);
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

    -- BUGFIX 2026-07-07 (fix-saldo-cross-company): overload company-scoped (2-arg).
    -- El 1-arg sp_obtener_cliente_saldo(v_clave) NO filtra company_id y en
    -- multi-empresa con claves colisionantes devolvía el saldo de otra empresa
    -- (viola la regla #1 del repo). p_company_id ya está en scope.
    SELECT COALESCE((
        SELECT s.saldo_actual
        FROM public.sp_obtener_cliente_saldo(p_company_id, v_clave) s
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
                -- BUGFIX 2026-07-06 (fix-resolver-medidos): en un cliente medido,
                -- los servicios base que NO usan condicion de medicion (ALCANTARILLADO,
                -- 100% PORCENTAJE_SERVICIO) tambien reciben el consumo del cliente para
                -- que su recursion resuelva el servicio referenciado medido (AGUA
                -- RANGO_CONSUMO). Sin esto el resolver base reventaba con "requiere
                -- consumo". Cliente NO medido queda intacto (cae al ELSE NULL como antes).
                WHEN v_tiene_medidor THEN v_consumo_facturable
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
$function$
;

-- ----------------------------------------------------------------------------
-- 3. sp_lectura_v3 — calendariopro company-scoped
--    (definición completa; único cambio: el SELECT de calendariopro)
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.sp_lectura_v3(p_company_id bigint, p_anio integer, p_mes integer, p_ciclo character varying DEFAULT NULL::character varying, p_clave character varying DEFAULT NULL::character varying, p_contador character varying DEFAULT NULL::character varying, p_fecha_lectura date DEFAULT CURRENT_DATE, p_usuario character varying DEFAULT NULL::character varying, p_lectura_actual numeric DEFAULT NULL::numeric, p_ser3 character DEFAULT NULL::bpchar, p_ser4 character DEFAULT NULL::bpchar, p_observacion character varying DEFAULT NULL::character varying, p_condicion_lectura character varying DEFAULT 'N'::character varying, p_lectura_promedio numeric DEFAULT NULL::numeric, p_numero_factura character varying DEFAULT NULL::character varying, p_correlativo_cai integer DEFAULT NULL::integer, p_id_cai integer DEFAULT NULL::integer, p_tienemedidor character DEFAULT NULL::bpchar, p_informativo character varying DEFAULT NULL::character varying, p_imagen bytea DEFAULT NULL::bytea, p_categoria character DEFAULT NULL::bpchar, p_lectura_uuid character varying DEFAULT NULL::character varying)
 RETURNS TABLE(success boolean, codigo text, mensaje text, factura_id integer, numrecibo integer, numero_factura text, cliente_id bigint, cliente_clave text, cliente_nombre text, consumo numeric, subtotal numeric, subtotal_ajustes numeric, saldos_anteriores numeric, recargos numeric, total numeric, taservi1 numeric, taservi2 numeric, taservi3 numeric, taservi4 numeric, detalle_servicios_json jsonb, warnings_json jsonb)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_cliente public.cliente_maestro%ROWTYPE;
    v_calc record;
    v_factura_id integer;
    v_numrecibo integer;
    v_fechavence date;
    v_plazo integer := 0;
    v_numdei text := '';
    v_prefijo_documento text := '';
    v_ciclo text;
    v_ruta text;
    v_secuencia text;
    v_tiene_medidor_char text;
    v_saldo_total numeric := 0;
    v_saldo_detalle numeric := 0;
    v_saldo_servicio_actual numeric := 0;
    v_uuid text := NULLIF(BTRIM(COALESCE(p_lectura_uuid, '')), '');
    v_factura_existente record;
    v_factura_periodo record;
    v_detalle record;
BEGIN
    SELECT *
    INTO v_cliente
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.maestro_cliente_clave = p_clave
      AND cm.estado = true
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe cliente activo con clave=% para company_id=%.',
            p_clave, p_company_id;
    END IF;

    IF p_id_cai IS NOT NULL THEN
        SELECT c.prefijo_documento
        INTO v_prefijo_documento
        FROM public.adm_cai_facturacion c
        WHERE c.company_id = p_company_id
          AND c.cai_id = p_id_cai
          AND c.status_id = 1
        LIMIT 1;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'No existe CAI V3 activo con id=% para company_id=%.', p_id_cai, p_company_id;
        END IF;
    END IF;

    IF p_id_cai IS NOT NULL AND (p_correlativo_cai IS NULL OR p_correlativo_cai <= 0) THEN
        RAISE EXCEPTION 'Correlativo CAI requerido para registrar lectura V3.';
    END IF;

    IF p_numero_factura IS NULL AND p_id_cai IS NOT NULL THEN
        p_numero_factura := concat(COALESCE(v_prefijo_documento, ''), lpad(COALESCE(p_correlativo_cai, 0)::text, 8, '0'));
    END IF;

    IF p_numero_factura IS NULL OR btrim(p_numero_factura) = '' THEN
        RAISE EXCEPTION 'Numero de factura requerido para registrar lectura.';
    END IF;

    IF v_uuid IS NOT NULL THEN
        SELECT
            e.factura_id,
            e.numero_factura
        INTO v_factura_existente
        FROM public.adm_cai_correlativo_emitido e
        WHERE e.company_id = p_company_id
          AND e.lectura_uuid = v_uuid
          AND e.factura_id IS NOT NULL
        ORDER BY e.cai_correlativo_emitido_id DESC
        LIMIT 1;

        IF FOUND THEN
            RETURN QUERY
            WITH factura_row AS (
                SELECT
                    f.id,
                    f.numrecibo,
                    f.numfactura,
                    COALESCE(f.saldototal, 0)::numeric(18, 4) AS total
                FROM public.factura f
                WHERE f.id = v_factura_existente.factura_id
                LIMIT 1
            ),
            historico_row AS (
                SELECT
                    COALESCE(hm.consumo, 0)::numeric(18, 4) AS consumo,
                    COALESCE(hm.descuentoapp, 0)::numeric(18, 4) AS subtotal_ajustes,
                    COALESCE(hm.taservi1, 0)::numeric(18, 4) AS taservi1,
                    COALESCE(hm.taservi2, 0)::numeric(18, 4) AS taservi2,
                    COALESCE(hm.taservi3, 0)::numeric(18, 4) AS taservi3,
                    COALESCE(hm.taservi4, 0)::numeric(18, 4) AS taservi4
                FROM public.historicomedicion hm
                JOIN factura_row fr
                  ON fr.numfactura = hm.numerofactura
                WHERE hm.clave = v_cliente.maestro_cliente_clave
                ORDER BY hm.ide DESC
                LIMIT 1
            ),
            detalle_json AS (
                SELECT
                    COALESCE(
                        jsonb_agg(
                            jsonb_build_object(
                                'servicio_codigo', fd.tiposervicio,
                                'servicio_nombre', fd.descripcion,
                                'monto_final', COALESCE(fd.montovalor, 0)
                            )
                            ORDER BY fd.tiposervicio, fd.descripcion
                        ),
                        '[]'::jsonb
                    ) AS detalle_servicios_json,
                    COALESCE(SUM(COALESCE(fd.montovalor, 0)), 0)::numeric(18, 4) AS subtotal
                FROM public.factura_detalle fd
                JOIN factura_row fr
                  ON fr.id = fd.factura_id
            )
            SELECT
                true,
                'IDEMPOTENTE'::text,
                'La lectura ya habia sido registrada anteriormente.'::text,
                fr.id,
                fr.numrecibo,
                fr.numfactura::text,
                v_cliente.maestro_cliente_id::bigint,
                v_cliente.maestro_cliente_clave::text,
                v_cliente.maestro_cliente_nombre::text,
                COALESCE(hr.consumo, 0),
                COALESCE(dj.subtotal, 0),
                COALESCE(hr.subtotal_ajustes, 0),
                GREATEST(fr.total - COALESCE(dj.subtotal, 0), 0)::numeric(18, 4),
                0::numeric,
                fr.total,
                COALESCE(hr.taservi1, 0),
                COALESCE(hr.taservi2, 0),
                COALESCE(hr.taservi3, 0),
                COALESCE(hr.taservi4, 0),
                dj.detalle_servicios_json,
                jsonb_build_array('LECTURA_IDEMPOTENTE')
            FROM factura_row fr
            CROSS JOIN detalle_json dj
            LEFT JOIN historico_row hr ON true;

            RETURN;
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.factura f
        WHERE f.clientecodigo = v_cliente.maestro_cliente_clave
          AND f.numfactura = p_numero_factura
    ) THEN
        RAISE EXCEPTION 'Ya existe factura con numero=% para cliente=%.', p_numero_factura, v_cliente.maestro_cliente_clave;
    END IF;

    SELECT f.id, f.numfactura, f.estado
    INTO v_factura_periodo
    FROM public.factura f
    WHERE f.clientecodigo = v_cliente.maestro_cliente_clave
      AND f.ano = p_anio::text
      AND f.mes = p_mes::text
      AND COALESCE(f.estado, '') <> 'N'
    ORDER BY f.id DESC
    LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION 'FACTURA_YA_EMITIDA: ya existe factura % (estado=%) para cliente=% en periodo %/%. Anule la factura previa antes de re-emitir.',
            v_factura_periodo.numfactura, v_factura_periodo.estado,
            v_cliente.maestro_cliente_clave, p_anio, p_mes;
    END IF;

    SELECT *
    INTO v_calc
    FROM public.sp_adm_calcular_factura_lectura(
        p_company_id,
        p_anio,
        p_mes,
        v_cliente.maestro_cliente_id,
        p_contador,
        COALESCE(p_fecha_lectura, current_date),
        p_lectura_actual,
        p_condicion_lectura,
        p_lectura_promedio,
        p_usuario,
        p_observacion,
        p_id_cai,
        p_correlativo_cai,
        p_numero_factura,
        p_informativo
    );

    IF NOT FOUND THEN
        RAISE EXCEPTION 'sp_adm_calcular_factura_lectura no devolvio resultado para cliente=%.', v_cliente.maestro_cliente_clave;
    END IF;

    v_ciclo := NULLIF(COALESCE(v_calc.ciclo, p_ciclo), '');
    v_ruta := NULLIF(COALESCE(v_calc.ruta, v_cliente.maestro_cliente_indicativo_ruta), '');
    v_secuencia := NULLIF(COALESCE(v_calc.secuencia, v_cliente.maestro_cliente_secuencia), '');
    v_tiene_medidor_char := CASE
        WHEN COALESCE(v_calc.tiene_medidor, v_cliente.maestro_cliente_tiene_medidor, false) THEN 'S'
        ELSE 'N'
    END;

    IF v_ciclo IS NOT NULL THEN
        -- Fase A (2026-07-14): calendariopro es multitenant — filtro por
        -- company_id. El match de ciclo tolera '1' vs '01' (SIMAFI sin cero a
        -- la izquierda vs planilla V3 normalizada a 2 dígitos).
        SELECT cp.fechavence, cp.diasvence
        INTO v_fechavence, v_plazo
        FROM public.calendariopro cp
        WHERE cp.company_id = p_company_id
          AND cp.ano = p_anio
          AND cp.mes = p_mes
          AND (
                btrim(cp.ciclo) = btrim(v_ciclo)
                OR (cp.ciclo ~ '^[0-9]+$' AND v_ciclo ~ '^[0-9]+$'
                    AND cp.ciclo::int = v_ciclo::int)
              )
        ORDER BY cp.ide DESC
        LIMIT 1;
    END IF;

    IF v_tiene_medidor_char = 'N' THEN
        UPDATE public.historicosinmedidor
        SET numerofactura = p_numero_factura,
            correlativocai = p_correlativo_cai,
            idcai = p_id_cai,
            fecha = now(),
            usuario = p_usuario
        WHERE cuenta = v_cliente.maestro_cliente_clave
          AND ano = p_anio
          AND mes = p_mes;

        IF NOT FOUND THEN
            INSERT INTO public.historicosinmedidor(
                cuenta, ano, mes, numerofactura, correlativocai, idcai, fecha, usuario
            )
            VALUES (
                v_cliente.maestro_cliente_clave, p_anio, p_mes, p_numero_factura, p_correlativo_cai, p_id_cai, now(), p_usuario
            );
        END IF;
    ELSE
        UPDATE public.historicomedicion
        SET fecha_lect_act   = COALESCE(p_fecha_lectura, current_date),
            usuario          = p_usuario,
            lect_act         = v_calc.lectura_actual_efectiva,
            consumo          = v_calc.consumo_facturable,
            taservi1         = COALESCE(v_calc.taservi1, 0),
            taservi2         = COALESCE(v_calc.taservi2, 0),
            taservi3         = COALESCE(v_calc.taservi3, 0),
            taservi4         = COALESCE(v_calc.taservi4, 0),
            ser3             = p_ser3,
            ser4             = p_ser4,
            observacion      = p_observacion,
            condicion        = v_calc.condicion_lectura_aplicada,
            lec_prom         = p_lectura_promedio,
            numerofactura    = p_numero_factura,
            correlativocai   = p_correlativo_cai,
            idcai            = p_id_cai,
            codinfo          = left(COALESCE(p_informativo, ''), 1),
            imagenmedidor    = p_imagen,
            descuentoapp     = COALESCE(v_calc.subtotal_ajustes, 0),
            categoriacliente = p_categoria
        WHERE contador = COALESCE(p_contador, v_calc.contador)
          AND ano = p_anio
          AND mes = p_mes;

        IF NOT FOUND THEN
            INSERT INTO public.historicomedicion(
                company_id,
                ano, mes, contador, ciclo, ruta, secuencia, clave, fecha,
                usuario, lect_act, lect_ant, fecha_lect_act, consumo,
                taservi1, taservi2, taservi3, taservi4,
                ser3, ser4, observacion, condicion, lec_prom,
                numerofactura, correlativocai, idcai, codinfo, imagenmedidor,
                descuentoapp, categoriacliente
            )
            VALUES (
                p_company_id,
                p_anio,
                p_mes,
                COALESCE(p_contador, v_calc.contador),
                v_ciclo,
                v_ruta,
                v_secuencia,
                v_cliente.maestro_cliente_clave,
                COALESCE(p_fecha_lectura, current_date),
                p_usuario,
                v_calc.lectura_actual_efectiva,
                v_calc.lectura_anterior,
                COALESCE(p_fecha_lectura, current_date),
                v_calc.consumo_facturable,
                COALESCE(v_calc.taservi1, 0),
                COALESCE(v_calc.taservi2, 0),
                COALESCE(v_calc.taservi3, 0),
                COALESCE(v_calc.taservi4, 0),
                p_ser3,
                p_ser4,
                p_observacion,
                v_calc.condicion_lectura_aplicada,
                p_lectura_promedio,
                p_numero_factura,
                p_correlativo_cai,
                p_id_cai,
                left(COALESCE(p_informativo, ''), 1),
                p_imagen,
                COALESCE(v_calc.subtotal_ajustes, 0),
                p_categoria
            );
        END IF;
    END IF;

    UPDATE public.factura
       SET estado = 'C',
           estado_id = 2  -- Cobrada/Compensada (cfg_estado_documento_comercial)
     WHERE clientecodigo = v_cliente.maestro_cliente_clave
       AND tipofacturacion = 'S'
       AND estado_id = 1;  -- Activa

    v_numdei := CASE
        WHEN p_id_cai IS NOT NULL THEN COALESCE(p_numero_factura, '')
        ELSE ''
    END;

    INSERT INTO public.factura AS f(
        company_id,
        numfactura,
        clientecodigo,
        tipofactura,
        ano,
        mes,
        fechaemision,
        fechavence,
        rtn,
        periodo,
        numdei,
        saldototal,
        usuario,
        identidad,
        estado,
        estado_id,
        tipofacturacion
    )
    VALUES (
        p_company_id,
        p_numero_factura,
        v_cliente.maestro_cliente_clave,
        'F',
        p_anio::text,
        p_mes::text,
        COALESCE(p_fecha_lectura, current_date),
        v_fechavence,
        COALESCE(v_cliente.maestro_cliente_rtn, ''),
        concat_ws('/', p_anio::text, p_mes::text),
        v_numdei,
        COALESCE(v_calc.total_factura, 0),
        p_usuario,
        COALESCE(v_cliente.maestro_cliente_identidad, ''),
        'A',
        1,  -- estado_id = Activa (cfg_estado_documento_comercial)
        'S'
    )
    RETURNING f.id, f.numrecibo INTO v_factura_id, v_numrecibo;

    v_saldo_total := COALESCE(v_calc.saldos_anteriores, 0);

    FOR v_detalle IN
        SELECT *
        FROM jsonb_to_recordset(COALESCE(v_calc.detalle_servicios_json, '[]'::jsonb)) AS d(
            servicio_codigo text,
            servicio_nombre text,
            monto_final numeric
        )
        WHERE COALESCE(d.monto_final, 0) <> 0
        ORDER BY servicio_codigo
    LOOP
        v_saldo_total := v_saldo_total + COALESCE(v_detalle.monto_final, 0);
        v_saldo_servicio_actual := COALESCE((
            SELECT *
            FROM public.sp_obtener_cliente_saldo_servicio_detalle(v_cliente.maestro_cliente_clave, v_detalle.servicio_codigo)
        ), 0);
        v_saldo_detalle := v_saldo_servicio_actual + COALESCE(v_detalle.monto_final, 0);

        INSERT INTO public.factura_detalle(
            company_id,
            numrecibo,
            codigo,
            tiposervicio,
            descripcion,
            montovalor,
            factura_id,
            montovalor_saldo
        )
        VALUES (
            p_company_id,
            v_numrecibo,
            '',
            v_detalle.servicio_codigo,
            v_detalle.servicio_nombre,
            COALESCE(v_detalle.monto_final, 0),
            v_factura_id,
            v_saldo_detalle
        );

        INSERT INTO public.transaccion_abonado(
            company_id,
            cliente_clave,
            recibo,
            tipotransaccion,
            docufuente,
            docufuente2,
            fecha_docu,
            tipo_partida,
            descripcion,
            plazo,
            docuaplicar,
            trans_aplicar,
            debitos,
            creditos,
            saldo,
            tipo_servicio,
            aplicar_alca,
            periodo,
            tasa,
            estado,
            estado_id,
            fecha_registro,
            ciclo,
            ruta,
            secuencia,
            tiene_med,
            codigoplan,
            motivo,
            usuario,
            saldo_detalle
        )
        VALUES (
            p_company_id,
            v_cliente.maestro_cliente_clave,
            v_numrecibo,
            v_detalle.servicio_codigo,
            0,
            '',
            COALESCE(p_fecha_lectura, current_date),
            '01',
            concat('Factura Periodo ', p_anio::text, '/', p_mes::text),
            COALESCE(v_plazo, 0),
            0,
            '',
            COALESCE(v_detalle.monto_final, 0),
            0,
            v_saldo_total,
            v_detalle.servicio_codigo,
            '',
            concat_ws('/', p_anio::text, p_mes::text),
            '0',
            'A',
            1,  -- estado_id = Activa
            COALESCE(p_fecha_lectura, current_date),
            v_ciclo,
            v_ruta,
            v_secuencia,
            v_tiene_medidor_char,
            '',
            '',
            p_usuario,
            v_saldo_detalle
        );
    END LOOP;

    RETURN QUERY
    SELECT
        true,
        'OK'::text,
        'Lectura registrada correctamente'::text,
        v_factura_id,
        v_numrecibo,
        p_numero_factura::text,
        v_cliente.maestro_cliente_id::bigint,
        v_cliente.maestro_cliente_clave::text,
        v_cliente.maestro_cliente_nombre::text,
        COALESCE(v_calc.consumo_facturable, 0),
        COALESCE(v_calc.subtotal_servicios, 0),
        COALESCE(v_calc.subtotal_ajustes, 0),
        COALESCE(v_calc.saldos_anteriores, 0),
        COALESCE(v_calc.recargos, 0),
        COALESCE(v_calc.total_factura, 0),
        COALESCE(v_calc.taservi1, 0),
        COALESCE(v_calc.taservi2, 0),
        COALESCE(v_calc.taservi3, 0),
        COALESCE(v_calc.taservi4, 0),
        COALESCE(v_calc.detalle_servicios_json, '[]'::jsonb),
        COALESCE(v_calc.warnings_json, '[]'::jsonb);
END;
$function$
;

COMMIT;
