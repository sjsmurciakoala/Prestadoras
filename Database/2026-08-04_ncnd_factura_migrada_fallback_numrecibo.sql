-- Backlog pruebas operativas (hallazgo 2026-08-04): emitir NC/ND contra una
-- factura MIGRADA de SIMAFI reventaba con 23502 (null en factura_origen_numero,
-- NOT NULL) porque las 3.9M facturas migradas no traen número fiscal
-- (numfactura vacío; SIMAFI solo manejaba numrecibo).
--
-- Fix de punto único en ambos SPs de emisión: el número de la factura origen
-- cae al número de recibo cuando no hay número fiscal. Aplica también a la
-- vista previa (usa el mismo SP) y a las descripciones/mensajes.
--
-- Fuente: 2026-07-30_uc_f7_sps_sin_espejo.sql (las versiones VIGENTES, sin
-- espejo legacy — OJO: nd_cobrable/nc_aplica_documento del mismo día son
-- anteriores y aún escribían transaccion_abonado). Sin ningún otro cambio.

BEGIN;

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
    -- Fallback para las 3.9M facturas migradas de SIMAFI sin número fiscal:
    -- el número de recibo identifica la factura origen (factura_origen_numero
    -- es NOT NULL y la emisión reventaba con 23502).
    SELECT f.id,
           COALESCE(NULLIF(btrim(f.numfactura), ''), f.numrecibo::text) AS numfactura,
           f.fechaemision, f.clientecodigo, f.saldototal,
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
        RAISE EXCEPTION 'FACTURA_YA_ANULADA: la factura origen % ya estÃ¡ anulada.', v_factura.numfactura;
    END IF;

    -- 2. Validar CAI emitible y que sea tipo NC (6)
    IF NOT public.fn_adm_validar_cai_emitible(p_company_id, p_cai_id) THEN
        RAISE EXCEPTION 'CAI_NO_EMITIBLE: el CAI % no estÃ¡ vigente o pasÃ³ su fecha lÃ­mite de emisiÃ³n.', p_cai_id;
    END IF;

    SELECT c.cai_id, c.prefijo_documento, c.correlativo_actual, c.rango_hasta,
           c.fecha_limite_emision, c.leyenda_rango, c.tipo_documento_fiscal_id,
           c.establecimiento_codigo
    INTO v_cai
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id AND c.cai_id = p_cai_id;

    IF v_cai.tipo_documento_fiscal_id <> 6 THEN
        RAISE EXCEPTION 'CAI_TIPO_INCORRECTO: el CAI % es tipo %, se requiere tipo 6 (Nota de CrÃ©dito).',
            p_cai_id, v_cai.tipo_documento_fiscal_id;
    END IF;

    -- 3. Tomar correlativo siguiente
    v_correlativo := v_cai.correlativo_actual + 1;
    IF v_correlativo > v_cai.rango_hasta THEN
        RAISE EXCEPTION 'CAI_AGOTADO: el CAI % alcanzÃ³ su rango mÃ¡ximo (%).', p_cai_id, v_cai.rango_hasta;
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
        RAISE EXCEPTION 'CLIENTE_NO_EXISTE: no se encontrÃ³ cliente % para la factura origen.',
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
        -- montovalor_saldo de las lÃ­neas por derrame FIFO (mismo clamp que los
        -- pagos: nunca deja una lÃ­nea negativa; el excedente sobre lo pendiente
        -- no tiene destino y se pierde, igual que el sobrepago del WS). Antes
        -- solo se escribÃ­a el crÃ©dito espejo y, tras el corte de F7, una NC
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

    -- 9c. Posteo contable por configuraciÃ³n (plan F5, D1/D2/D10): partida
    -- espejo de la factura origen â€” Debe Ingresos (o DEVOLUCION_NC si estÃ¡
    -- configurado) / Haber CxC analÃ­tica â€” SOLO si activo_notas estÃ¡
    -- encendido. Misma transacciÃ³n: si el posteo falla (sin asiento NOTAS,
    -- cuenta sin resolver, sin perÃ­odo y sin encolar), la emisiÃ³n completa se
    -- revierte. Devuelve NULL si quedÃ³ encolada en con_partida_pendiente.
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
             THEN 'Nota de crÃ©dito emitida y factura origen anulada.'
             ELSE 'Nota de crÃ©dito parcial emitida (factura origen sigue activa).'
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
    -- Fallback para las 3.9M facturas migradas de SIMAFI sin número fiscal:
    -- el número de recibo identifica la factura origen (factura_origen_numero
    -- es NOT NULL y la emisión reventaba con 23502).
    SELECT f.id,
           COALESCE(NULLIF(btrim(f.numfactura), ''), f.numrecibo::text) AS numfactura,
           f.fechaemision, f.clientecodigo, f.saldototal,
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

COMMIT;
