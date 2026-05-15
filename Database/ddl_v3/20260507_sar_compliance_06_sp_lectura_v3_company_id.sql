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
        SELECT cp.fechavence, cp.diasvence
        INTO v_fechavence, v_plazo
        FROM public.calendariopro cp
        WHERE cp.ano = p_anio
          AND cp.mes = p_mes
          AND cp.ciclo = v_ciclo
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
       SET estado = 'C'
     WHERE clientecodigo = v_cliente.maestro_cliente_clave
       AND tipofacturacion = 'S'
       AND estado = 'A';

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
            v_saldo_servicio_actual
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

