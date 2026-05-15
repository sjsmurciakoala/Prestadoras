-- 2026-03-05_update_sp_lectura_v2_contabilidad.sql
-- Integra generacion de partida contable automatica (sin IVA) al registrar lectura

CREATE OR REPLACE PROCEDURE public.sp_lectura_v2(
    IN p_anio integer,
    IN p_mes integer,
    IN p_contador character varying,
    IN p_fecha date,
    IN p_usuario character varying,
    IN p_lecturaactual numeric,
    IN p_consumo numeric,
    IN p_ser3 character,
    IN p_ser4 character,
    IN p_observacion character varying,
    IN p_condicionlectura character varying,
    IN p_lecturapromedio numeric,
    IN p_codigolectura character,
    IN p_codigoinfo character,
    IN p_numerofactura character varying,
    IN p_correlativocai integer,
    IN p_idcai integer,
    IN p_tienemedidor character,
    IN p_clave character varying,
    IN p_informativo character varying,
    IN p_imagen bytea,
    IN p_descuento numeric,
    IN p_categoria character,
    IN p_detalle jsonb
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    v_maestro_cliente_id integer;
    v_fechavence date;
    v_ciclo character varying;
    v_recibo integer;
    v_facturaid integer;
    v_plazo integer;
    v_ruta character varying;
    v_secuencia character varying;
    v_saldoabonado numeric;
    v_cai RECORD;
    v_monto numeric;
    v_total numeric := 0;
    v_saldo_total numeric;
    v_saldo_detalle numeric;
    v_line RECORD;
    v_descripcion character varying;

    -- contabilidad
    v_company_id bigint;
    v_doc_fac bigint;
    v_type_id bigint;
    v_debit_account_id bigint;
    v_credit_account_default bigint;
    v_cost_center_id bigint;
    v_poliza_id bigint;
    v_period_id bigint;
    v_seq bigint;
    v_poliza_number text;
    v_credit_total numeric := 0;
    v_line_number smallint := 2;
    v_service_account_id bigint;
    v_poliza_date date;
BEGIN
    IF p_tienemedidor = 'N' THEN
        INSERT INTO public.historicosinmedidor(
            cuenta, ano, mes, numerofactura, correlativocai, idcai, fecha, usuario)
        VALUES (p_clave, p_anio, p_mes, p_numerofactura, p_correlativocai, p_idcai, now(), p_usuario);
    ELSE
        UPDATE historicomedicion
           SET fecha_lect_act   = now(),
               usuario          = p_usuario,
               lect_act         = p_lecturaactual,
               consumo          = p_consumo,
               taservi1         = 0,
               taservi2         = 0,
               taservi3         = 0,
               taservi4         = 0,
               ser3             = p_ser3,
               ser4             = p_ser4,
               observacion      = p_observacion,
               condicion        = p_condicionlectura,
               lec_prom         = p_lecturapromedio,
               numerofactura    = p_numerofactura,
               correlativocai   = p_correlativocai,
               idcai            = p_idcai,
               codinfo          = p_informativo,
               imagenmedidor    = p_imagen,
               descuentoaPP     = p_descuento,
               categoriacliente = p_categoria
         WHERE contador = p_contador AND ano = p_anio AND mes = p_mes;
    END IF;

    -- total desde detalle
    IF p_detalle IS NOT NULL THEN
        SELECT COALESCE(sum((d->>'Monto')::numeric), 0)
          INTO v_total
          FROM jsonb_array_elements(p_detalle) d;
    END IF;

    -- obtener CAI
    SELECT ide, ruta, cai, codigo_base, contador_actual
      INTO v_cai
      FROM cai
     WHERE ide = p_idcai
     LIMIT 1;

    SELECT INTO v_maestro_cliente_id maestro_cliente_id
      FROM cliente_maestro
     WHERE maestro_cliente_clave = p_clave
     LIMIT 1;

    SELECT INTO v_ciclo ciclo
      FROM historicomedicion
     WHERE contador = p_contador AND ano = p_anio AND mes = p_mes
     LIMIT 1;

    SELECT INTO v_fechavence, v_plazo fechavence, diasvence
      FROM calendariopro
     WHERE ano = p_anio AND mes = p_mes AND ciclo = v_ciclo
     ORDER BY ide DESC
     LIMIT 1;

    SELECT INTO v_saldoabonado sp_obtener_cliente_saldo
      FROM sp_obtener_cliente_saldo(p_clave);
    IF COALESCE(v_saldoabonado, 0) = 0 THEN
        v_saldoabonado := 0;
    END IF;

    -- cerrar factura activa
    UPDATE factura
       SET estado = 'C'
     WHERE clientecodigo = p_clave AND tipofacturacion = 'S' AND estado = 'A';

    INSERT INTO public.factura(
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
        p_numerofactura,
        p_clave,
        'F',
        p_anio,
        p_mes,
        now(),
        v_fechavence,
        '',
        concat_ws('/', cast(p_anio as character varying), cast(p_mes as character varying)),
        concat(v_cai.codigo_base, '-', cast(rpad('00', 8, cast(v_cai.contador_actual as character varying)) as character varying)),
        v_saldoabonado + v_total,
        p_usuario,
        '',
        'A',
        'S'
    )
    RETURNING numrecibo, id INTO v_recibo, v_facturaid;

    v_saldo_total := v_saldoabonado;

    -- detalle por servicio (orden del JSON)
    FOR v_line IN
        SELECT
            (d->>'ServicioCodigo')::varchar AS servicio_codigo,
            NULLIF(trim(d->>'Descripcion'), '') AS descripcion,
            COALESCE((d->>'Monto')::numeric, 0) AS monto
        FROM jsonb_array_elements(p_detalle) d
        WHERE COALESCE((d->>'Monto')::numeric, 0) <> 0
    LOOP
        v_monto := v_line.monto;

        v_descripcion := v_line.descripcion;
        IF v_descripcion IS NULL THEN
            SELECT s.servicios_descripcioncorta
              INTO v_descripcion
              FROM servicios s
             WHERE s.servicios_codigo = v_line.servicio_codigo
             LIMIT 1;
        END IF;

        v_saldo_total := v_saldo_total + v_monto;
        v_saldo_detalle := COALESCE(sp_obtener_cliente_saldo_servicio_detalle(p_clave, v_line.servicio_codigo), 0) + v_monto;

        INSERT INTO public.factura_detalle(
            numrecibo, codigo, tiposervicio, descripcion, montovalor, factura_id, montovalor_saldo)
        VALUES (
            v_recibo, '', v_line.servicio_codigo, v_descripcion, v_monto, v_facturaid,
            COALESCE(sp_obtener_cliente_saldo_servicio_detalle(p_clave, v_line.servicio_codigo), 0)
        );

        INSERT INTO public.transaccion_abonado(
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
            saldo_detalle)
        VALUES (
            p_clave,
            v_recibo,
            v_line.servicio_codigo,
            0,
            '',
            now(),
            '01',
            concat('Factura Periodo ', cast(p_anio as character varying), '/', cast(p_mes as character varying)),
            v_plazo,
            0,
            '',
            v_monto,
            0,
            v_saldo_total,
            v_line.servicio_codigo,
            '',
            concat_ws('/', cast(p_anio as character varying), cast(p_mes as character varying)),
            0,
            'A',
            now(),
            v_ciclo,
            v_ruta,
            v_secuencia,
            p_tienemedidor,
            '',
            '',
            p_usuario,
            v_saldo_detalle
        );
    END LOOP;

    -- =========================
    -- Contabilidad automatica (sin IVA)
    -- =========================
    IF COALESCE(v_total, 0) > 0 THEN
        -- Resolver regla/documento desde catalogos activos.
        SELECT r.company_id,
               r.document_type_id,
               r.debit_account_id,
               r.credit_account_id,
               r.cost_center_id
          INTO v_company_id,
               v_doc_fac,
               v_debit_account_id,
               v_credit_account_default,
               v_cost_center_id
          FROM public.con_regla_integracion r
          JOIN public.cfg_document_type dt
            ON dt.document_type_id = r.document_type_id
           AND dt.company_id = r.company_id
         WHERE r.module = 'VENTAS'
           AND dt.module = 'VENTAS'
           AND dt.code = 'FAC'
           AND r.is_active = true
           AND dt.is_active = true
         ORDER BY CASE WHEN upper(COALESCE(r.scenario_code, '')) = 'FAC_NETO' THEN 0 ELSE 1 END,
                  r.updated_at DESC NULLS LAST,
                  r.regla_id DESC
         LIMIT 1;

        IF v_company_id IS NULL THEN
            RAISE EXCEPTION 'No existe regla de integracion activa para VENTAS/FAC.';
        END IF;

        -- Resolver tipo de transaccion activo (default > FAC > primero activo).
        SELECT t.type_id
          INTO v_type_id
          FROM public.con_tipo_transaccion t
         WHERE t.company_id = v_company_id
           AND t.is_default = true
           AND COALESCE(
                t.status_id,
                CASE
                    WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                    WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                    ELSE 1
                END
           ) = 1
         ORDER BY t.type_id
         LIMIT 1;

        IF v_type_id IS NULL THEN
            SELECT t.type_id
              INTO v_type_id
              FROM public.con_tipo_transaccion t
             WHERE t.company_id = v_company_id
               AND upper(COALESCE(t.code, '')) = 'FAC'
               AND COALESCE(
                    t.status_id,
                    CASE
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                        ELSE 1
                    END
               ) = 1
             ORDER BY t.type_id
             LIMIT 1;
        END IF;

        IF v_type_id IS NULL THEN
            SELECT t.type_id
              INTO v_type_id
              FROM public.con_tipo_transaccion t
             WHERE t.company_id = v_company_id
               AND COALESCE(
                    t.status_id,
                    CASE
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                        ELSE 1
                    END
               ) = 1
             ORDER BY t.type_id
             LIMIT 1;
        END IF;

        IF v_type_id IS NULL THEN
            RAISE EXCEPTION 'No existe con_tipo_transaccion activa para la empresa %', v_company_id;
        END IF;

        SELECT period_id
          INTO v_period_id
          FROM public.con_periodo_contable
         WHERE company_id = v_company_id
           AND COALESCE(status_id, 2) = 0
           AND COALESCE(p_fecha, current_date) BETWEEN start_date::date AND end_date::date
         ORDER BY start_date DESC
         LIMIT 1;

        IF v_period_id IS NULL THEN
            RAISE EXCEPTION 'No hay periodo contable abierto (estado 0) para la fecha %', COALESCE(p_fecha, current_date);
        END IF;

        -- Evitar duplicar poliza si ya existe
        SELECT poliza_id
          INTO v_poliza_id
          FROM public.con_partida_hdr
         WHERE company_id = v_company_id
           AND module = 'VENTAS'
           AND document_type = 'FAC'
           AND document_id = v_facturaid
         LIMIT 1;

        IF v_poliza_id IS NULL THEN
            SELECT COUNT(*) + 1
              INTO v_seq
              FROM public.con_partida_hdr
             WHERE company_id = v_company_id;

            v_poliza_date := COALESCE(p_fecha, current_date);
            v_poliza_number := v_company_id::text || '-' || EXTRACT(YEAR FROM v_poliza_date)::text || '-' || lpad(v_seq::text, 6, '0');

            INSERT INTO public.con_partida_hdr (
                company_id, journal_id, period_id, template_id,
                module, document_type, document_id, document_number,
                poliza_number, sequence_number, poliza_date, description,
                status, source_reference, created_at, created_by, type_id,
                total_debit, total_credit
            ) VALUES (
                v_company_id, NULL, v_period_id, NULL,
                'VENTAS', 'FAC', v_facturaid, p_numerofactura,
                v_poliza_number, v_seq, v_poliza_date, 'Factura lectura ' || p_numerofactura,
                0, p_numerofactura, now(), p_usuario, v_type_id,
                v_total, v_total
            )
            RETURNING poliza_id INTO v_poliza_id;

            -- Linea debito CxC (total)
            INSERT INTO public.con_partida_dtl (
                company_id, poliza_id, line_number, account_id, cost_center_id,
                description, debit_amount, credit_amount, source_document
            ) VALUES (
                v_company_id, v_poliza_id, 1, v_debit_account_id, NULL,
                'CxC factura ' || p_numerofactura, v_total, 0, p_numerofactura
            );

            v_credit_total := 0;
            v_line_number := 2;

            IF p_detalle IS NOT NULL THEN
                FOR v_line IN
                    SELECT
                        (d->>'ServicioCodigo')::varchar AS servicio_codigo,
                        NULLIF(trim(d->>'Descripcion'), '') AS descripcion,
                        COALESCE((d->>'Monto')::numeric, 0) AS monto
                    FROM jsonb_array_elements(p_detalle) d
                    WHERE COALESCE((d->>'Monto')::numeric, 0) <> 0
                LOOP
                    v_monto := v_line.monto;
                    v_descripcion := v_line.descripcion;

                    SELECT s.cont_account_id
                      INTO v_service_account_id
                      FROM public.servicios s
                     WHERE s.company_id = v_company_id
                       AND s.servicios_codigo = v_line.servicio_codigo
                     LIMIT 1;

                    IF v_service_account_id IS NULL THEN
                        v_service_account_id := v_credit_account_default;
                    END IF;

                    INSERT INTO public.con_partida_dtl (
                        company_id, poliza_id, line_number, account_id, cost_center_id,
                        description, debit_amount, credit_amount, source_document
                    ) VALUES (
                        v_company_id, v_poliza_id, v_line_number, v_service_account_id, v_cost_center_id,
                        COALESCE(v_descripcion, 'Servicio ' || v_line.servicio_codigo), 0, v_monto, p_numerofactura
                    );

                    v_credit_total := v_credit_total + v_monto;
                    v_line_number := v_line_number + 1;
                END LOOP;
            END IF;

            IF v_credit_total = 0 THEN
                INSERT INTO public.con_partida_dtl (
                    company_id, poliza_id, line_number, account_id, cost_center_id,
                    description, debit_amount, credit_amount, source_document
                ) VALUES (
                    v_company_id, v_poliza_id, v_line_number, v_credit_account_default, v_cost_center_id,
                    'Ingresos factura ' || p_numerofactura, 0, v_total, p_numerofactura
                );
                v_credit_total := v_total;
            END IF;

            IF abs(v_credit_total - v_total) > 0.01 THEN
                RAISE EXCEPTION 'Partida no cuadra. Debe % , Haber %', v_total, v_credit_total;
            END IF;
        END IF;

        PERFORM public.sp_con_postear_poliza(v_company_id, v_poliza_id, p_usuario);
    END IF;

    -- actualizar CAI
    IF (v_cai.contador_actual < p_correlativocai) THEN
        UPDATE cai
           SET contador_actual = p_correlativocai
         WHERE ide = p_idcai;
    END IF;
END
$BODY$;

ALTER PROCEDURE public.sp_lectura_v2(
    integer, integer, character varying, date, character varying, numeric, numeric,
    character, character, character varying, character varying, numeric, character, character,
    character varying, integer, integer, character, character varying, character varying, bytea,
    numeric, character, jsonb)
    OWNER TO postgres;

