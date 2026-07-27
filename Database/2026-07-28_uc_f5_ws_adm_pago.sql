-- ============================================================================
-- Unificación de cobranza — F5 (2026-07-28)
-- WS bancario sobre el modelo nuevo: sp_ban_ws_pagar registra el pago como
-- adm_pago (canal 2 = banco, tipo PAGO_BANCO, folio RECIBO_PAGO) con sus
-- adm_pago_aplicacion por línea (espejo de _ws_aplicacion), y sp_ban_ws_reversar
-- lo marca REVERSADO (estado 4) conservando las aplicaciones como auditoría —
-- misma semántica que CobroService.ReversarAsync.
--
-- ban_ws_pago se conserva como bitácora del canal y gana adm_pago_id (link 1:1
-- al documento del motor). La fila legacy 202 en transaccion_abonado se sigue
-- escribiendo hasta F7 (dual-write). FIRMAS Y XML INTACTOS: los golden files
-- del contrato SIMAFI pasan sin modificación (aceptación F5).
-- Base: definiciones vigentes de F8 (pg_get_functiondef).
-- ============================================================================

BEGIN;

ALTER TABLE public.ban_ws_pago
    ADD COLUMN IF NOT EXISTS adm_pago_id bigint REFERENCES public.adm_pago (pago_id);

COMMENT ON COLUMN public.ban_ws_pago.adm_pago_id IS
'F5: documento adm_pago del motor único generado por este pago del canal banco (1:1; NULL para pagos previos a F5).';

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

        INSERT INTO public.transaccion_abonado (
            company_id, cliente_clave, recibo, tipotransaccion, fecha_docu, tipo_partida,
            banco, descripcion, debitos, creditos, saldo, tipo_servicio, periodo, tasa,
            estado, fecha_registro, ciclo, ruta, secuencia, tiene_med, usuario,
            saldo_detalle, docuaplicar, trans_aplicar)
        VALUES (
            p_company_id, v_clave, v_factura.numrecibo, '202', p_fecha_registro, '002',
            btrim(p_banco),
            'Pago WS banco ' || btrim(p_banco) || ' Ref:' || v_referencia || ' :Recibo # :' || v_factura.numrecibo,
            0, v_factura.aplicado, v_saldo_cliente, 'E', v_periodo, '0',
            'C', p_fecha_registro,
            v_cliente_info.ciclos_id::text,
            v_cliente_info.maestro_cliente_indicativo_ruta,
            v_cliente_info.maestro_cliente_secuencia,
            CASE WHEN v_cliente_info.maestro_cliente_tiene_medidor THEN 'S' ELSE 'N' END,
            v_usuario, v_factura.aplicado, v_pago_id, 'WSBANCO:' || v_pago_id);
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

CREATE OR REPLACE FUNCTION public.sp_ban_ws_reversar(p_company_id bigint, p_referencia character varying, p_usuario text DEFAULT 'wsbanco'::text)
 RETURNS TABLE(status text, pago_id bigint, poliza_reverso_id bigint, ban_kardex_reverso_id bigint)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_referencia varchar := btrim(p_referencia);
    v_usuario text := COALESCE(NULLIF(btrim(p_usuario), ''), 'wsbanco');
    v_pago record;
    v_trans record;
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
    FOR v_trans IN
        SELECT t.ide, t.recibo, t.creditos
        FROM public.transaccion_abonado t
        WHERE t.company_id = p_company_id
          AND t.docuaplicar = v_pago.pago_id
          -- El LIKE textual (además del '=') hace usable el índice parcial
          -- ix_transaccion_abonado_wsbanco (su WHERE es LIKE 'WSBANCO:%'): el
          -- planner no deriva el predicado parcial de una igualdad con expresión.
          AND t.trans_aplicar LIKE 'WSBANCO:%'
          AND t.trans_aplicar = 'WSBANCO:' || v_pago.pago_id
          AND t.estado = 'C'
        ORDER BY t.ide
    LOOP
        -- Filtra por clientecodigo (usa ix_factura_clientecodigo_numrecibo y evita
        -- tomar una factura de otro cliente si numrecibo estuviera duplicado —
        -- numrecibo es identity pero NO tiene UNIQUE).
        SELECT f.id INTO v_factura
        FROM public.factura f
        WHERE f.company_id = p_company_id
          AND f.clientecodigo = v_pago.clave
          AND f.numrecibo = v_trans.recibo::integer;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'sp_ban_ws_reversar: no existe la factura del recibo % (pago %).',
                v_trans.recibo, v_pago.pago_id;
        END IF;

        WITH lineas AS (
            SELECT d.id,
                   COALESCE(d.montovalor, 0) - COALESCE(d.montovalor_saldo, 0) AS restituible,
                   SUM(COALESCE(d.montovalor, 0) - COALESCE(d.montovalor_saldo, 0))
                       OVER (ORDER BY d.id ROWS UNBOUNDED PRECEDING) AS acumulado
            FROM public.factura_detalle d
            WHERE d.factura_id = v_factura.id
              AND COALESCE(d.montovalor, 0) - COALESCE(d.montovalor_saldo, 0) > 0
        ), aplicar AS (
            SELECT l.id,
                   GREATEST(0::numeric,
                       LEAST(l.restituible, COALESCE(v_trans.creditos, 0) - (l.acumulado - l.restituible))) AS restituir
            FROM lineas l
        )
        UPDATE public.factura_detalle d
        SET montovalor_saldo = COALESCE(d.montovalor_saldo, 0) + a.restituir
        FROM aplicar a
        WHERE d.id = a.id AND a.restituir > 0;

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

        UPDATE public.transaccion_abonado t
        SET estado = 'A',
            usuario = v_usuario,
            descripcion = 'REVERSADO WS: ref ' || v_referencia
        WHERE t.ide = v_trans.ide;
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
