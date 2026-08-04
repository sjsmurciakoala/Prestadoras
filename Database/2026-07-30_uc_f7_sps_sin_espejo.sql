-- ============================================================================
-- Unificación de cobranza — F7 hito 2c-4 (2026-07-30)
-- SE APAGA EL ESPEJO EN LOS SPs. Los 4 procedimientos vivos del censo dejan de
-- escribir en transaccion_abonado; cada uno ya tiene su casa en el modelo de
-- documentos:
--   * sp_lectura_v3            → la línea de factura ES el cargo (F4).
--   * sp_ban_ws_pagar          → el pago vive en adm_pago (F5).
--   * sp_adm_emitir_nota_credito → la NC aplica al documento (F7 H2a).
--   * sp_adm_emitir_nota_debito  → la ND es documento cobrable (F7 H2b).
--   * sp_ban_ws_reversar       → restituye por adm_pago_aplicacion (antes
--     iteraba el espejo: sin él NO restituía saldo alguno).
-- Con esto NINGÚN camino del sistema escribe la tabla legacy: queda lista para
-- el freeze de H4. El cálculo tarifario, la contabilidad por configuración, el
-- kardex y el contrato XML del banco quedan intactos.
-- Generado desde las definiciones vigentes (pg_get_functiondef).
-- ============================================================================

BEGIN;

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
    -- F4: saldos por servicio de los documentos pendientes, capturados ANTES
    -- de compensar la factura anterior (la fuente por documentos devolvería 0
    -- después del UPDATE de compensación).
    v_saldos_previos jsonb := '{}'::jsonb;
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

    -- F4: capturar el saldo pendiente por servicio (líneas de facturas A/B)
    -- ANTES de la compensación — es el arrastre que llevarán las líneas de la
    -- factura nueva (montovalor_saldo = saldo previo del servicio + mes).
    SELECT COALESCE(jsonb_object_agg(t.tiposervicio, t.saldo), '{}'::jsonb)
      INTO v_saldos_previos
      FROM (
          SELECT d.tiposervicio, SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)) AS saldo
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = v_cliente.maestro_cliente_clave
            AND f.estado IN ('A','B')
          GROUP BY d.tiposervicio
      ) t;

    UPDATE public.factura
       SET estado = 'C',
           estado_id = 2  -- Cobrada/Compensada (cfg_estado_documento_comercial)
     WHERE company_id = p_company_id  -- F4: faltaba el filtro de empresa
       AND clientecodigo = v_cliente.maestro_cliente_clave
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
        -- F4: arrastre desde la captura previa a la compensación (documentos
        -- pendientes de ESTA empresa; antes: corrida legacy cross-company que
        -- quedaba desactualizada con los pagos del motor).
        v_saldo_servicio_actual := COALESCE((v_saldos_previos ->> v_detalle.servicio_codigo)::numeric, 0);
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

        -- F7 H2c: sin espejo legacy — la línea de factura ES el documento.

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

CREATE OR REPLACE FUNCTION public.sp_ban_ws_pagar(p_company_id bigint, p_banco character varying, p_referencia character varying, p_clave character varying, p_monto numeric, p_fecha_registro date, p_hora_registro time without time zone DEFAULT NULL::time without time zone, p_fecha_efectiva date DEFAULT NULL::date, p_sucursal character varying DEFAULT NULL::character varying, p_cajero character varying DEFAULT NULL::character varying, p_banco_cuenta_id bigint DEFAULT NULL::bigint, p_tipo character varying DEFAULT 'S'::character varying, p_validar_monto boolean DEFAULT true, p_usuario text DEFAULT 'wsbanco'::text)
 RETURNS TABLE(status text, pago_id bigint, poliza_id bigint, ban_kardex_id bigint, total_pendiente numeric)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_referencia varchar := btrim(p_referencia);
    v_clave varchar := btrim(p_clave);
    v_usuario text := COALESCE(NULLIF(btrim(p_usuario), ''), 'wsbanco');
    v_existente record;
    v_total numeric;
    v_pago_id bigint;
    v_cuenta_id bigint;
    v_kardex_id bigint;
    v_saldo_kardex numeric;
    v_poliza_id bigint;
    v_config record;
    v_cuenta_banco bigint;
    v_lineas jsonb;
    v_descripcion text;
    v_saldo_cliente numeric;
    v_periodo varchar(6);
    v_factura record;
    v_cliente_info record;
    v_restante numeric;
    -- F5: documento del motor único (adm_pago) generado por este pago.
    v_adm_pago_id bigint;
    v_numero_recibo varchar;
BEGIN
    IF v_referencia IS NULL OR v_referencia = '' THEN
        RAISE EXCEPTION 'sp_ban_ws_pagar: referencia vacía.';
    END IF;
    IF p_monto IS NULL OR p_monto <= 0 THEN
        RAISE EXCEPTION 'sp_ban_ws_pagar: monto inválido (%).', p_monto;
    END IF;

    -- Serializa por referencia dentro del tenant (idempotencia bajo concurrencia).
    PERFORM pg_advisory_xact_lock(
        hashtextextended('ban_ws_pago:' || p_company_id::text || ':' || v_referencia, 0));
    -- Y por abonado: dos pagos con referencias DISTINTAS del mismo cliente (o un
    -- cobro de caja simultáneo) no deben aplicarse contra el mismo snapshot de
    -- saldos. Sin este lock, el UPDATE de montovalor_saldo (valor absoluto del
    -- snapshot, no decremento) permitía doble aplicación.
    PERFORM pg_advisory_xact_lock(
        hashtextextended('ban_ws_clave:' || p_company_id::text || ':' || v_clave, 0));

    SELECT * INTO v_existente
    FROM public.ban_ws_pago p
    WHERE p.company_id = p_company_id AND p.referencia = v_referencia;

    IF FOUND THEN
        IF v_existente.status_id = 1 THEN
            -- Replay: solo es idempotente si es el MISMO abonado. Una referencia
            -- reutilizada para OTRA clave (el WS viejo lo permitía y aplicaba el
            -- segundo pago) NO debe devolver "Pago exitoso" sin aplicar nada:
            -- se rechaza para no liquidar a un cliente sin cobrar.
            IF v_existente.clave IS DISTINCT FROM v_clave THEN
                RETURN QUERY SELECT 'REFERENCIA_EN_USO'::text, v_existente.pago_id,
                    NULL::bigint, NULL::bigint, NULL::numeric;
                RETURN;
            END IF;
            -- Mismo abonado: replay real, misma respuesta, cero duplicación.
            RETURN QUERY SELECT 'IDEMPOTENTE'::text, v_existente.pago_id,
                v_existente.poliza_id, v_existente.ban_kardex_id, v_existente.monto;
            RETURN;
        END IF;
        -- Referencia reversada: el WS viejo la rechazaba (intención de
        -- existeReferencia(ref,'R') en Control.PagarServicios).
        RETURN QUERY SELECT 'REFERENCIA_REVERSADA'::text, v_existente.pago_id,
            NULL::bigint, NULL::bigint, NULL::numeric;
        RETURN;
    END IF;

    -- Cliente y pendientes (fuente única fn_ban_ws_pendientes).
    SELECT cm.ciclos_id, cm.maestro_cliente_indicativo_ruta, cm.maestro_cliente_secuencia,
           cm.maestro_cliente_tiene_medidor
    INTO v_cliente_info
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id AND cm.maestro_cliente_clave = v_clave;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'SIN_REGISTRO'::text, NULL::bigint, NULL::bigint, NULL::bigint, NULL::numeric;
        RETURN;
    END IF;

    DROP TABLE IF EXISTS _ws_pendientes;
    CREATE TEMP TABLE _ws_pendientes ON COMMIT DROP AS
    SELECT * FROM public.fn_ban_ws_pendientes(p_company_id, v_clave);

    SELECT COALESCE(SUM(w.saldo), 0) INTO v_total FROM _ws_pendientes w;

    IF v_total <= 0 THEN
        RETURN QUERY SELECT 'SIN_PENDIENTES'::text, NULL::bigint, NULL::bigint, NULL::bigint, v_total;
        RETURN;
    END IF;

    -- Contrato /pago/servicios: el monto debe ser EXACTAMENTE el total pendiente.
    IF p_validar_monto AND round(p_monto, 2) <> round(v_total, 2) THEN
        RETURN QUERY SELECT 'MONTO_NO_COINCIDE'::text, NULL::bigint, NULL::bigint, NULL::bigint, v_total;
        RETURN;
    END IF;

    -- /pago/otros (parcial = abono, plan §5 F8.4): se permite monto < total, pero
    -- NUNCA monto > total. Un sobrepago no tiene destino (no hay saldo a favor):
    -- el kardex registraría el monto completo y el comprobante solo lo aplicado,
    -- descuadrando banco vs contabilidad. Se rechaza el excedente.
    IF NOT p_validar_monto AND round(p_monto, 2) > round(v_total, 2) THEN
        RETURN QUERY SELECT 'MONTO_NO_COINCIDE'::text, NULL::bigint, NULL::bigint, NULL::bigint, v_total;
        RETURN;
    END IF;

    -- Cuenta bancaria destino: parámetro explícito o la de la credencial.
    v_cuenta_id := p_banco_cuenta_id;
    IF v_cuenta_id IS NULL THEN
        SELECT c.banco_cuenta_id INTO v_cuenta_id
        FROM public.ban_ws_credencial c
        WHERE c.company_id = p_company_id AND c.banco = btrim(p_banco) AND c.activo;
    END IF;

    INSERT INTO public.ban_ws_pago (
        company_id, banco, referencia, clave, tipo, monto,
        fecha_registro, hora_registro, fecha_efectiva, sucursal, cajero,
        banco_cuenta_id, status_id, created_by)
    VALUES (
        p_company_id, btrim(p_banco), v_referencia, v_clave, COALESCE(p_tipo, 'S'), round(p_monto, 2),
        p_fecha_registro, p_hora_registro, p_fecha_efectiva,
        NULLIF(btrim(p_sucursal), ''), NULLIF(btrim(p_cajero), ''),
        v_cuenta_id, 1, v_usuario)
    RETURNING ban_ws_pago.pago_id INTO v_pago_id;

    -- Derrame FIFO sobre las líneas (mismo orden y clamp que AbonoService F4):
    -- aplicado = LEAST(saldo, restante acumulado); solo líneas con saldo > 0.
    DROP TABLE IF EXISTS _ws_aplicacion;
    CREATE TEMP TABLE _ws_aplicacion ON COMMIT DROP AS
    WITH orden AS (
        SELECT w.*,
               SUM(w.saldo) OVER (ORDER BY w.fechaemision, w.numrecibo, w.detalle_id
                                  ROWS UNBOUNDED PRECEDING) AS acumulado
        FROM _ws_pendientes w
        WHERE w.saldo > 0
    )
    SELECT o.*,
           GREATEST(0::numeric, LEAST(o.saldo, round(p_monto, 2) - (o.acumulado - o.saldo))) AS aplicado
    FROM orden o;

    UPDATE public.factura_detalle d
    SET montovalor_saldo = a.saldo - a.aplicado
    FROM _ws_aplicacion a
    WHERE d.id = a.detalle_id AND a.aplicado > 0;

    -- Saldo del cliente para la corrida de transacciones (igual que AbonoService).
    SELECT COALESCE(s.saldo_actual, 0) INTO v_saldo_cliente
    FROM public.sp_obtener_cliente_saldo(p_company_id, v_clave) s;
    v_saldo_cliente := COALESCE(v_saldo_cliente, 0);

    -- Período comercial actual vía la fuente única de F7 (no inlinear la consulta:
    -- era la 5.ª copia). Fallback = mes de la fecha del banco si la empresa aún
    -- no tiene período abierto.
    SELECT COALESCE(
        (SELECT lpad(pa.anio::text, 4, '0') || lpad(pa.mes::text, 2, '0')
         FROM public.fn_adm_periodo_comercial_actual(p_company_id) pa),
        to_char(p_fecha_registro, 'YYYYMM'))
    INTO v_periodo;

    -- Por factura (FIFO): estado B/C + transaccion_abonado 202 con banco.
    FOR v_factura IN
        SELECT a.factura_id, a.numrecibo, a.numfactura,
               SUM(a.aplicado) AS aplicado
        FROM _ws_aplicacion a
        WHERE a.aplicado > 0
        GROUP BY a.factura_id, a.numrecibo, a.numfactura, a.fechaemision
        ORDER BY a.fechaemision, a.numrecibo
    LOOP
        v_restante := (
            SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)), 0)
            FROM public.factura_detalle d
            WHERE d.factura_id = v_factura.factura_id
              AND COALESCE(d.montovalor_saldo, d.montovalor, 0) > 0);

        UPDATE public.factura f
        SET estado = CASE WHEN v_restante <= 0 THEN 'C' ELSE 'B' END,
            fechapago = CASE WHEN v_restante <= 0 THEN p_fecha_registro ELSE f.fechapago END,
            recolectora = CASE WHEN v_restante <= 0 THEN btrim(p_banco) ELSE f.recolectora END,
            usuario = v_usuario
        WHERE f.id = v_factura.factura_id AND f.company_id = p_company_id;

        v_saldo_cliente := v_saldo_cliente - v_factura.aplicado;

        -- F7 H2c: sin espejo 202 — el pago vive en adm_pago (F5).

    END LOOP;

    -- Movimiento bancario (kardex DEP) si hay cuenta destino configurada.
    v_descripcion := 'Pago WS banco ' || btrim(p_banco) || ' clave ' || v_clave || ' ref ' || v_referencia;
    IF v_cuenta_id IS NOT NULL THEN
        CALL public.sp_ban_kardex_registrar_movimiento(
            p_company_id, v_cuenta_id, 0, 'DEP', p_fecha_registro,
            v_descripcion::varchar, v_referencia, 1::numeric, round(p_monto, 2),
            v_usuario::varchar, v_kardex_id, v_saldo_kardex);
    END IF;

    -- Comprobante contable por configuración (F4): Debe banco / Haber CxC analítica.
    SELECT cfg.modo_cxc, cfg.activo_bancos INTO v_config
    FROM public.con_integracion_config cfg
    WHERE cfg.company_id = p_company_id;

    IF FOUND AND v_config.activo_bancos THEN
        SELECT COALESCE(
            (SELECT bc.cont_account_id FROM public.ban_cuenta bc
             WHERE bc.company_id = p_company_id AND bc.banco_cuenta_id = v_cuenta_id
               AND bc.cont_account_id IS NOT NULL AND bc.cont_account_id > 0),
            public.fn_con_resolver_cuenta(p_company_id, 'BANCO_DEFAULT', NULL, NULL, NULL))
        INTO v_cuenta_banco;

        -- Haberes CxC por cuenta resuelta (modo de la config, snapshot de la
        -- factura) redondeados por cuenta; el Debe es la suma de esos redondeos
        -- (regla F4: nunca redondear la suma cruda de un lado).
        WITH resuelto AS (
            SELECT public.fn_con_resolver_cuenta_modo(
                       p_company_id, 'CXC', v_config.modo_cxc, s.servicio_id,
                       a.categoria_servicio_id, a.con_medicion) AS cuenta_id,
                   a.aplicado
            FROM _ws_aplicacion a
            LEFT JOIN LATERAL (
                SELECT sv.servicio_id
                FROM public.adm_servicio sv
                WHERE sv.company_id = p_company_id
                  AND upper(btrim(sv.codigo)) = upper(btrim(COALESCE(NULLIF(a.tiposervicio, ''), a.codigo, '')))
                ORDER BY sv.servicio_id
                LIMIT 1
            ) s ON true
            WHERE a.aplicado > 0
        ), haberes AS (
            SELECT r.cuenta_id, round(SUM(r.aplicado), 2) AS monto
            FROM resuelto r
            GROUP BY r.cuenta_id
            HAVING round(SUM(r.aplicado), 2) > 0
        )
        SELECT jsonb_insert(
                   COALESCE(jsonb_agg(
                       jsonb_build_object(
                           'account_id', h.cuenta_id,
                           'debe', 0,
                           'haber', h.monto,
                           'descripcion', v_descripcion)
                       ORDER BY h.cuenta_id), '[]'::jsonb),
                   '{0}',
                   jsonb_build_object(
                       'account_id', v_cuenta_banco,
                       'debe', (SELECT round(SUM(h2.monto), 2) FROM haberes h2),
                       'haber', 0,
                       'descripcion', v_descripcion))
        INTO v_lineas
        FROM haberes h;

        v_poliza_id := public.sp_con_generar_comprobante_config(
            p_company_id, 'BANCOS', 'PGB', v_pago_id, 'WSB-' || v_referencia,
            p_fecha_registro, v_descripcion, v_usuario, v_lineas);

        IF v_poliza_id IS NOT NULL AND v_kardex_id IS NOT NULL THEN
            UPDATE public.ban_kardex k
            SET partida_cuenta_id = v_poliza_id
            WHERE k.company_id = p_company_id AND k.ban_kardex_id = v_kardex_id;
        END IF;
    END IF;

    -- F5: el pago queda como documento del motor único (adm_pago, canal 2 =
    -- banco, tipo 3 = PAGO_BANCO) con una aplicación por línea cobrada —
    -- espejo exacto del derrame FIFO de _ws_aplicacion. La fila legacy 202 de
    -- arriba se conserva hasta F7 (dual-write). transaccion_abonado_ide queda
    -- NULL a propósito: el espejo del WS es una fila POR FACTURA (1:N) y su
    -- link vive en trans_aplicar = 'WSBANCO:<pago_id de ban_ws_pago>'.
    v_numero_recibo := public.fn_adm_siguiente_correlativo_documento(
        p_company_id, 'RECIBO_PAGO', 2::smallint);

    INSERT INTO public.adm_pago (
        company_id, numero_recibo, cliente_clave, fecha, canal_id,
        tipo_transaccion_id, estado_id, monto_total, forma_pago,
        banco_cuenta_id, ban_kardex_id, poliza_id, referencia_externa, usuario)
    VALUES (
        p_company_id, v_numero_recibo, v_clave, p_fecha_registro, 2,
        3, 1, round(p_monto, 2), 'BANCO',
        v_cuenta_id::integer, v_kardex_id, v_poliza_id, v_referencia, v_usuario)
    RETURNING adm_pago.pago_id INTO v_adm_pago_id;

    INSERT INTO public.adm_pago_aplicacion (
        company_id, pago_id, documento_tipo, factura_id, factura_detalle_id, monto_aplicado)
    SELECT p_company_id, v_adm_pago_id, 1, a.factura_id, a.detalle_id, a.aplicado
    FROM _ws_aplicacion a
    WHERE a.aplicado > 0;

    UPDATE public.ban_ws_pago p
    SET ban_kardex_id = v_kardex_id,
        poliza_id = v_poliza_id,
        adm_pago_id = v_adm_pago_id
    WHERE p.pago_id = v_pago_id;

    RETURN QUERY SELECT 'OK'::text, v_pago_id, v_poliza_id, v_kardex_id, v_total;
END;
$function$

;

CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_credito(p_company_id bigint, p_factura_origen_id integer, p_motivo_anulacion_id smallint, p_motivo_detalle character varying, p_monto_disminuir numeric, p_lineas jsonb, p_usuario_emisor character varying, p_cai_id bigint)
 RETURNS TABLE(success boolean, codigo text, mensaje text, nota_credito_id bigint, numero_documento text, correlativo bigint)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_factura record;
    v_cai record;
    v_company record;
    v_correlativo bigint;
    v_numero text;
    v_nota_id bigint;
    v_total numeric(18,4);
    v_monto numeric(18,4);
    v_anula boolean;
    v_linea record;
    v_saldo_anterior numeric(18,4) := 0;
    v_activo_notas boolean;
    v_poliza_id bigint;
    -- F7 H2a: saldo restante del documento tras aplicar la NC parcial.
    v_saldo_restante numeric(18,4);
BEGIN
    -- 1. Validar factura origen
    SELECT f.id, f.numfactura, f.fechaemision, f.clientecodigo, f.saldototal,
           f.estado, f.company_id
    INTO v_factura
    FROM public.factura f
    WHERE f.id = p_factura_origen_id
      AND f.company_id = p_company_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'FACTURA_NO_EXISTE: factura origen % no existe para company %.',
            p_factura_origen_id, p_company_id;
    END IF;

    IF COALESCE(v_factura.estado, '') = 'N' THEN
        RAISE EXCEPTION 'FACTURA_YA_ANULADA: la factura origen % ya está anulada.', v_factura.numfactura;
    END IF;

    -- 2. Validar CAI emitible y que sea tipo NC (6)
    IF NOT public.fn_adm_validar_cai_emitible(p_company_id, p_cai_id) THEN
        RAISE EXCEPTION 'CAI_NO_EMITIBLE: el CAI % no está vigente o pasó su fecha límite de emisión.', p_cai_id;
    END IF;

    SELECT c.cai_id, c.prefijo_documento, c.correlativo_actual, c.rango_hasta,
           c.fecha_limite_emision, c.leyenda_rango, c.tipo_documento_fiscal_id,
           c.establecimiento_codigo
    INTO v_cai
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id AND c.cai_id = p_cai_id;

    IF v_cai.tipo_documento_fiscal_id <> 6 THEN
        RAISE EXCEPTION 'CAI_TIPO_INCORRECTO: el CAI % es tipo %, se requiere tipo 6 (Nota de Crédito).',
            p_cai_id, v_cai.tipo_documento_fiscal_id;
    END IF;

    -- 3. Tomar correlativo siguiente
    v_correlativo := v_cai.correlativo_actual + 1;
    IF v_correlativo > v_cai.rango_hasta THEN
        RAISE EXCEPTION 'CAI_AGOTADO: el CAI % alcanzó su rango máximo (%).', p_cai_id, v_cai.rango_hasta;
    END IF;

    v_numero := concat(COALESCE(v_cai.prefijo_documento, ''), lpad(v_correlativo::text, 8, '0'));

    -- 4. Snapshot emisor desde cfg_company
    SELECT co.tax_id, co.legal_name, co.commercial_name, co.address
    INTO v_company
    FROM public.cfg_company co
    WHERE co.company_id = p_company_id;

    -- 5. Monto a disminuir (default = saldo total de la factura)
    v_total := COALESCE(v_factura.saldototal, 0)::numeric(18,4);
    v_monto := COALESCE(p_monto_disminuir, v_total)::numeric(18,4);
    IF v_monto <= 0 THEN
        RAISE EXCEPTION 'MONTO_INVALIDO: el monto a disminuir debe ser mayor a 0.';
    END IF;
    IF v_monto > v_total THEN
        RAISE EXCEPTION 'MONTO_EXCEDE_FACTURA: monto a disminuir % supera el saldo de la factura %.',
            v_monto, v_total;
    END IF;
    v_anula := (v_monto >= v_total);

    -- 6. INSERT cabecera
    INSERT INTO public.adm_nota_credito (
        company_id, establecimiento_codigo,
        tipo_documento_fiscal_id, numero_documento, cai_id, correlativo,
        fecha_limite_cai, leyenda_cai_rango,
        rtn_emisor, razon_social_emisor, direccion_emisor,
        cliente_id, rtn_receptor, razon_social_receptor, direccion_receptor,
        factura_origen_id, factura_origen_numero, factura_origen_fecha, factura_origen_cai,
        motivo_anulacion_id, motivo_detalle,
        monto_disminuir, isv_disminuir, total_nota, anula_factura_origen,
        estado_id, usuario_emisor, created_by
    )
    SELECT
        p_company_id, COALESCE(v_cai.establecimiento_codigo, '000'),
        6, v_numero, p_cai_id, v_correlativo,
        v_cai.fecha_limite_emision, v_cai.leyenda_rango,
        COALESCE(v_company.tax_id, ''), COALESCE(v_company.legal_name, v_company.commercial_name, ''), v_company.address,
        cm.maestro_cliente_id, cm.maestro_cliente_rtn,
        cm.maestro_cliente_nombre, NULL,
        v_factura.id, v_factura.numfactura, v_factura.fechaemision, v_factura.numfactura,
        p_motivo_anulacion_id, p_motivo_detalle,
        v_monto, 0, v_monto, v_anula,
        1, p_usuario_emisor, p_usuario_emisor
    FROM public.cliente_maestro cm
    WHERE cm.maestro_cliente_clave = v_factura.clientecodigo
      AND cm.company_id = p_company_id
    LIMIT 1
    RETURNING adm_nota_credito.nota_credito_id INTO v_nota_id;

    IF v_nota_id IS NULL THEN
        RAISE EXCEPTION 'CLIENTE_NO_EXISTE: no se encontró cliente % para la factura origen.',
            v_factura.clientecodigo;
    END IF;

    -- 7. INSERT detalle: desde p_lineas o copiando de factura_detalle de la factura origen
    IF p_lineas IS NOT NULL THEN
        FOR v_linea IN
            SELECT *
            FROM jsonb_to_recordset(p_lineas) AS l(
                servicio_id bigint,
                servicio_codigo text,
                descripcion text,
                cantidad numeric,
                monto_unitario numeric,
                monto_total numeric,
                isv_monto numeric,
                cuenta_contable_codigo text
            )
        LOOP
            INSERT INTO public.adm_nota_credito_detalle (
                nota_credito_id, servicio_id, servicio_codigo, descripcion,
                cantidad, monto_unitario, monto_total, isv_monto, cuenta_contable_codigo
            )
            VALUES (
                v_nota_id, v_linea.servicio_id, v_linea.servicio_codigo,
                COALESCE(v_linea.descripcion, v_linea.servicio_codigo, ''),
                COALESCE(v_linea.cantidad, 1), COALESCE(v_linea.monto_unitario, 0),
                COALESCE(v_linea.monto_total, 0), COALESCE(v_linea.isv_monto, 0),
                v_linea.cuenta_contable_codigo
            );
        END LOOP;
    ELSE
        INSERT INTO public.adm_nota_credito_detalle (
            nota_credito_id, servicio_id, servicio_codigo, descripcion,
            cantidad, monto_unitario, monto_total, isv_monto
        )
        SELECT
            v_nota_id,
            s.servicio_id,
            fd.tiposervicio,
            COALESCE(fd.descripcion, fd.tiposervicio, ''),
            1,
            COALESCE(fd.montovalor, 0),
            COALESCE(fd.montovalor, 0),
            0
        FROM public.factura_detalle fd
        LEFT JOIN public.adm_servicio s
          ON s.company_id = p_company_id
         AND s.codigo = fd.tiposervicio
        WHERE fd.factura_id = v_factura.id
          AND COALESCE(fd.montovalor, 0) <> 0;
    END IF;

    -- 8. Avanzar correlativo del CAI
    UPDATE public.adm_cai_facturacion
    SET correlativo_actual = v_correlativo,
        updated_at = now(),
        updated_by = p_usuario_emisor
    WHERE company_id = p_company_id AND cai_id = p_cai_id;

    -- 9. Marcar la factura origen como anulada SOLO si la NC cubre el total
    IF v_anula THEN
        UPDATE public.factura
        SET estado = 'N',
            estado_id = 3,  -- ANULADA
            motivo_anulacion_id = p_motivo_anulacion_id,
            updated_at = now()
        WHERE id = v_factura.id;
    ELSE
        -- F7 H2a (2026-07-30): la NC PARCIAL aplica al DOCUMENTO â€” rebaja
        -- montovalor_saldo de las líneas por derrame FIFO (mismo clamp que los
        -- pagos: nunca deja una línea negativa; el excedente sobre lo pendiente
        -- no tiene destino y se pierde, igual que el sobrepago del WS). Antes
        -- solo se escribía el crédito espejo y, tras el corte de F7, una NC
        -- parcial no bajaba la deuda del cliente.
        WITH lineas AS (
            SELECT d.id,
                   COALESCE(d.montovalor_saldo, d.montovalor, 0) AS saldo,
                   SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
                       OVER (ORDER BY d.id ROWS UNBOUNDED PRECEDING) AS acumulado
            FROM public.factura_detalle d
            WHERE d.factura_id = v_factura.id
              AND COALESCE(d.montovalor_saldo, d.montovalor, 0) > 0
        ), aplicar AS (
            SELECT l.id,
                   GREATEST(0::numeric, LEAST(l.saldo, v_monto - (l.acumulado - l.saldo))) AS rebaja
            FROM lineas l
        )
        UPDATE public.factura_detalle d
        SET montovalor_saldo = COALESCE(d.montovalor_saldo, d.montovalor, 0) - a.rebaja
        FROM aplicar a
        WHERE d.id = a.id AND a.rebaja > 0;

        SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)), 0)
        INTO v_saldo_restante
        FROM public.factura_detalle d
        WHERE d.factura_id = v_factura.id;

        UPDATE public.factura
        SET estado = CASE WHEN v_saldo_restante <= 0 THEN 'C' ELSE 'B' END,
            estado_id = CASE WHEN v_saldo_restante <= 0 THEN 2 ELSE 4 END,
            updated_at = now()
        WHERE id = v_factura.id
          AND estado IN ('A', 'B');
    END IF;

    -- F7 H2c: sin espejo 205 — la NC ya aplica al documento (H2a).

    -- 9c. Posteo contable por configuración (plan F5, D1/D2/D10): partida
    -- espejo de la factura origen â€” Debe Ingresos (o DEVOLUCION_NC si está
    -- configurado) / Haber CxC analítica â€” SOLO si activo_notas está
    -- encendido. Misma transacción: si el posteo falla (sin asiento NOTAS,
    -- cuenta sin resolver, sin período y sin encolar), la emisión completa se
    -- revierte. Devuelve NULL si quedó encolada en con_partida_pendiente.
    SELECT c.activo_notas INTO v_activo_notas
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;

    IF COALESCE(v_activo_notas, false) THEN
        v_poliza_id := public.sp_con_generar_comprobante_config(
            p_company_id,
            'NOTAS',
            'NC',
            v_nota_id,
            v_numero,
            current_date,
            concat('N/C ', v_numero, ' s/factura ', v_factura.numfactura),
            p_usuario_emisor,
            public.fn_con_lineas_nota(p_company_id, 'NC', v_nota_id));

        UPDATE public.adm_nota_credito
        SET poliza_id = v_poliza_id
        WHERE adm_nota_credito.nota_credito_id = v_nota_id;
    END IF;

    -- 10. Resultado
    RETURN QUERY SELECT
        true,
        'OK'::text,
        CASE WHEN v_anula
             THEN 'Nota de crédito emitida y factura origen anulada.'
             ELSE 'Nota de crédito parcial emitida (factura origen sigue activa).'
        END::text,
        v_nota_id,
        v_numero,
        v_correlativo;
END;
$function$

;

CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_debito(p_company_id bigint, p_factura_origen_id integer, p_motivo_aumento_id smallint, p_motivo_detalle character varying, p_monto_aumentar numeric, p_lineas jsonb, p_usuario_emisor character varying, p_cai_id bigint)
 RETURNS TABLE(success boolean, codigo text, mensaje text, nota_debito_id bigint, numero_documento text, correlativo bigint)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_factura record;
    v_cai record;
    v_company record;
    v_correlativo bigint;
    v_numero text;
    v_nota_id bigint;
    v_monto numeric(18,4);
    v_linea record;
    v_motivo_desc text;
    v_saldo_anterior numeric(18,4) := 0;
    v_activo_notas boolean;
    v_poliza_id bigint;
BEGIN
    -- 1. Validar factura origen
    SELECT f.id, f.numfactura, f.fechaemision, f.clientecodigo, f.saldototal,
           f.estado, f.company_id
    INTO v_factura
    FROM public.factura f
    WHERE f.id = p_factura_origen_id
      AND f.company_id = p_company_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'FACTURA_NO_EXISTE: factura origen % no existe para company %.',
            p_factura_origen_id, p_company_id;
    END IF;

    IF COALESCE(v_factura.estado, '') = 'N' THEN
        RAISE EXCEPTION 'FACTURA_ANULADA: no se puede emitir ND sobre la factura anulada %.', v_factura.numfactura;
    END IF;

    -- 2. Validar CAI emitible y tipo ND (7)
    IF NOT public.fn_adm_validar_cai_emitible(p_company_id, p_cai_id) THEN
        RAISE EXCEPTION 'CAI_NO_EMITIBLE: el CAI % no está vigente o pasó su fecha límite de emisión.', p_cai_id;
    END IF;

    SELECT c.cai_id, c.prefijo_documento, c.correlativo_actual, c.rango_hasta,
           c.fecha_limite_emision, c.leyenda_rango, c.tipo_documento_fiscal_id,
           c.establecimiento_codigo
    INTO v_cai
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id AND c.cai_id = p_cai_id;

    IF v_cai.tipo_documento_fiscal_id <> 7 THEN
        RAISE EXCEPTION 'CAI_TIPO_INCORRECTO: el CAI % es tipo %, se requiere tipo 7 (Nota de Débito).',
            p_cai_id, v_cai.tipo_documento_fiscal_id;
    END IF;

    -- 3. Correlativo
    v_correlativo := v_cai.correlativo_actual + 1;
    IF v_correlativo > v_cai.rango_hasta THEN
        RAISE EXCEPTION 'CAI_AGOTADO: el CAI % alcanzó su rango máximo (%).', p_cai_id, v_cai.rango_hasta;
    END IF;
    v_numero := concat(COALESCE(v_cai.prefijo_documento, ''), lpad(v_correlativo::text, 8, '0'));

    -- 4. Snapshot emisor
    SELECT co.tax_id, co.legal_name, co.commercial_name, co.address
    INTO v_company
    FROM public.cfg_company co
    WHERE co.company_id = p_company_id;

    -- 5. Monto a aumentar (requerido)
    v_monto := COALESCE(p_monto_aumentar, 0)::numeric(18,4);
    IF v_monto <= 0 THEN
        RAISE EXCEPTION 'MONTO_INVALIDO: el monto a aumentar debe ser mayor a 0.';
    END IF;

    SELECT ma.descripcion INTO v_motivo_desc
    FROM public.cfg_motivo_aumento ma
    WHERE ma.motivo_aumento_id = p_motivo_aumento_id;

    -- 6. INSERT cabecera
    INSERT INTO public.adm_nota_debito (
        company_id, establecimiento_codigo,
        tipo_documento_fiscal_id, numero_documento, cai_id, correlativo,
        fecha_limite_cai, leyenda_cai_rango,
        rtn_emisor, razon_social_emisor, direccion_emisor,
        cliente_id, rtn_receptor, razon_social_receptor, direccion_receptor,
        factura_origen_id, factura_origen_numero, factura_origen_fecha, factura_origen_cai,
        motivo_aumento_id, motivo_detalle,
        monto_aumentar, isv_aumentar, total_nota,
        estado_id, usuario_emisor, created_by,
        saldo_pendiente  -- F7 H2b: la ND nace con su saldo vivo completo
    )
    SELECT
        p_company_id, COALESCE(v_cai.establecimiento_codigo, '000'),
        7, v_numero, p_cai_id, v_correlativo,
        v_cai.fecha_limite_emision, v_cai.leyenda_rango,
        COALESCE(v_company.tax_id, ''), COALESCE(v_company.legal_name, v_company.commercial_name, ''), v_company.address,
        cm.maestro_cliente_id, cm.maestro_cliente_rtn,
        cm.maestro_cliente_nombre, NULL,
        v_factura.id, v_factura.numfactura, v_factura.fechaemision, v_factura.numfactura,
        p_motivo_aumento_id, p_motivo_detalle,
        v_monto, 0, v_monto,
        1, p_usuario_emisor, p_usuario_emisor,
        v_monto
    FROM public.cliente_maestro cm
    WHERE cm.maestro_cliente_clave = v_factura.clientecodigo
      AND cm.company_id = p_company_id
    LIMIT 1
    RETURNING adm_nota_debito.nota_debito_id INTO v_nota_id;

    IF v_nota_id IS NULL THEN
        RAISE EXCEPTION 'CLIENTE_NO_EXISTE: no se encontró cliente % para la factura origen.',
            v_factura.clientecodigo;
    END IF;

    -- 7. Detalle
    IF p_lineas IS NOT NULL THEN
        FOR v_linea IN
            SELECT *
            FROM jsonb_to_recordset(p_lineas) AS l(
                servicio_id bigint,
                servicio_codigo text,
                descripcion text,
                cantidad numeric,
                monto_unitario numeric,
                monto_total numeric,
                isv_monto numeric,
                cuenta_contable_codigo text
            )
        LOOP
            INSERT INTO public.adm_nota_debito_detalle (
                nota_debito_id, servicio_id, servicio_codigo, descripcion,
                cantidad, monto_unitario, monto_total, isv_monto, cuenta_contable_codigo
            )
            VALUES (
                v_nota_id, v_linea.servicio_id, v_linea.servicio_codigo,
                COALESCE(v_linea.descripcion, v_linea.servicio_codigo, ''),
                COALESCE(v_linea.cantidad, 1), COALESCE(v_linea.monto_unitario, 0),
                COALESCE(v_linea.monto_total, 0), COALESCE(v_linea.isv_monto, 0),
                v_linea.cuenta_contable_codigo
            );
        END LOOP;
    ELSE
        -- Una sola línea con el motivo del aumento
        INSERT INTO public.adm_nota_debito_detalle (
            nota_debito_id, servicio_id, servicio_codigo, descripcion,
            cantidad, monto_unitario, monto_total, isv_monto
        )
        VALUES (
            v_nota_id, NULL, NULL,
            COALESCE(v_motivo_desc, 'Ajuste por nota de débito'),
            1, v_monto, v_monto, 0
        );
    END IF;

    -- 8. Avanzar correlativo
    UPDATE public.adm_cai_facturacion
    SET correlativo_actual = v_correlativo,
        updated_at = now(),
        updated_by = p_usuario_emisor
    WHERE company_id = p_company_id AND cai_id = p_cai_id;

    -- F7 H2c: sin espejo 206 — la ND es documento cobrable (H2b).

    -- 8c. Posteo contable por configuración (plan F5, D1/D2/D10): partida
    -- inversa a la NC — Debe CxC analítica / Haber Ingresos — SOLO si
    -- activo_notas está encendido. Misma transacción (atómico); NULL =
    -- encolada en con_partida_pendiente.
    SELECT c.activo_notas INTO v_activo_notas
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;

    IF COALESCE(v_activo_notas, false) THEN
        v_poliza_id := public.sp_con_generar_comprobante_config(
            p_company_id,
            'NOTAS',
            'ND',
            v_nota_id,
            v_numero,
            current_date,
            concat('N/D ', v_numero, ' s/factura ', v_factura.numfactura),
            p_usuario_emisor,
            public.fn_con_lineas_nota(p_company_id, 'ND', v_nota_id));

        UPDATE public.adm_nota_debito
        SET poliza_id = v_poliza_id
        WHERE adm_nota_debito.nota_debito_id = v_nota_id;
    END IF;

    -- 9. Resultado (un ND NO anula la factura origen, solo aumenta el saldo del cliente)
    RETURN QUERY SELECT
        true,
        'OK'::text,
        'Nota de débito emitida correctamente.'::text,
        v_nota_id,
        v_numero,
        v_correlativo;
END;
$function$

;

CREATE OR REPLACE FUNCTION public.sp_ban_ws_reversar(p_company_id bigint, p_referencia character varying, p_usuario text DEFAULT 'wsbanco'::text)
 RETURNS TABLE(status text, pago_id bigint, poliza_reverso_id bigint, ban_kardex_reverso_id bigint)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_referencia varchar := btrim(p_referencia);
    v_usuario text := COALESCE(NULLIF(btrim(p_usuario), ''), 'wsbanco');
    v_pago record;
    v_factura record;
    v_restante numeric;
    v_total_factura numeric;
    v_saldo_restaurado numeric;
    v_kardex_rev bigint;
    v_saldo_kardex numeric;
    v_poliza_rev bigint;
BEGIN
    PERFORM pg_advisory_xact_lock(
        hashtextextended('ban_ws_pago:' || p_company_id::text || ':' || v_referencia, 0));

    SELECT * INTO v_pago
    FROM public.ban_ws_pago p
    WHERE p.company_id = p_company_id AND p.referencia = v_referencia
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'NO_EXISTE'::text, NULL::bigint, NULL::bigint, NULL::bigint;
        RETURN;
    END IF;

    -- Lock por abonado (mismo orden que sp_ban_ws_pagar): serializa la reversión
    -- contra un pago/otra reversión del mismo cliente para no restituir saldos
    -- sobre un snapshot que otro flujo está modificando.
    PERFORM pg_advisory_xact_lock(
        hashtextextended('ban_ws_clave:' || p_company_id::text || ':' || v_pago.clave, 0));

    IF v_pago.status_id <> 1 THEN
        RETURN QUERY SELECT 'YA_REVERSADA'::text, v_pago.pago_id, NULL::bigint, NULL::bigint;
        RETURN;
    END IF;

    -- Restituir por transacción (una por factura, FIFO por ide): el derrame
    -- inverso replica ReversarAbonoAsync (max restituible por línea =
    -- montovalor - montovalor_saldo, en orden de detalle).
    -- F7 H2c: la restitución se hace por las APLICACIONES del pago
    -- (adm_pago_aplicacion), no por el espejo legacy que ya no se escribe.
    -- Devuelve el saldo exacto a cada línea cobrada — mejor que el derrame
    -- inverso heurístico anterior: la tabla sabe qué se cobró dónde.
    IF v_pago.adm_pago_id IS NULL THEN
        RAISE EXCEPTION 'sp_ban_ws_reversar: el pago % no tiene documento del motor (adm_pago); no se puede reversar.',
            v_pago.pago_id;
    END IF;

    UPDATE public.factura_detalle d
    SET montovalor_saldo = LEAST(
            COALESCE(d.montovalor, 0),
            COALESCE(d.montovalor_saldo, 0) + a.monto_aplicado)
    FROM public.adm_pago_aplicacion a
    WHERE a.pago_id = v_pago.adm_pago_id
      AND a.company_id = p_company_id
      AND a.factura_detalle_id = d.id;

    -- Estado de cada factura tocada: 'A' si recuperó todo su saldo, 'B' si
    -- quedó parcialmente abonada por otro pago.
    FOR v_factura IN
        SELECT DISTINCT a.factura_id AS id
        FROM public.adm_pago_aplicacion a
        WHERE a.pago_id = v_pago.adm_pago_id
          AND a.company_id = p_company_id
          AND a.factura_id IS NOT NULL
    LOOP
        SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, 0)), 0),
               COALESCE(SUM(COALESCE(d.montovalor, 0)), 0)
        INTO v_saldo_restaurado, v_total_factura
        FROM public.factura_detalle d
        WHERE d.factura_id = v_factura.id;

        UPDATE public.factura f
        SET estado = CASE WHEN v_saldo_restaurado >= v_total_factura THEN 'A' ELSE 'B' END,
            fechapago = CASE WHEN v_saldo_restaurado >= v_total_factura THEN NULL ELSE f.fechapago END,
            recolectora = CASE WHEN v_saldo_restaurado >= v_total_factura THEN NULL ELSE f.recolectora END,
            usuario = v_usuario
        WHERE f.id = v_factura.id AND f.company_id = p_company_id;
    END LOOP;

    -- Contramovimiento de kardex (si el pago registró movimiento).
    IF v_pago.ban_kardex_id IS NOT NULL THEN
        CALL public.sp_ban_kardex_anular_movimiento_recalcular(
            p_company_id, v_pago.banco_cuenta_id, v_pago.ban_kardex_id,
            ('Reversión WS ref ' || v_referencia)::varchar, v_usuario::varchar,
            v_kardex_rev, v_saldo_kardex);
    END IF;

    -- Reverso del comprobante por documento (o descarte de la pendiente viva).
    v_poliza_rev := public.sp_con_revertir_comprobante_config(
        p_company_id, 'BANCOS', ARRAY['PGB']::varchar[], v_pago.pago_id, v_usuario);

    -- F5: reverso en el modelo nuevo — adm_pago pasa a REVERSADO (estado 4)
    -- con motivo; las adm_pago_aplicacion se CONSERVAN como auditoría de lo
    -- que se aplicó (misma regla que CobroService.ReversarAsync). La
    -- restitución real de saldos ya ocurrió arriba sobre factura_detalle.
    IF v_pago.adm_pago_id IS NOT NULL THEN
        UPDATE public.adm_pago ap
        SET estado_id = 4,
            motivo_reverso = 'Reversión WS ref ' || v_referencia,
            actualizado_en = now()
        WHERE ap.company_id = p_company_id
          AND ap.pago_id = v_pago.adm_pago_id
          AND ap.estado_id = 1;
    END IF;

    UPDATE public.ban_ws_pago p
    SET status_id = 2,
        reversado_at = now(),
        reversado_por = v_usuario,
        ban_kardex_reverso_id = v_kardex_rev
    WHERE p.pago_id = v_pago.pago_id;

    RETURN QUERY SELECT 'OK'::text, v_pago.pago_id, v_poliza_rev, v_kardex_rev;
END;
$function$

;

COMMIT;
