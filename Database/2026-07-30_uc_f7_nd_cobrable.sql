-- ============================================================================
-- Unificación de cobranza — F7 hito 2b (2026-07-30)
-- La NOTA DE DÉBITO se vuelve DOCUMENTO COBRABLE por la caja única
-- (documento_tipo = 3, reservado en el motor desde F2 con la columna
-- adm_pago_aplicacion.nota_debito_id):
--   * adm_nota_debito gana saldo_pendiente (saldo vivo, análogo de
--     montovalor_saldo / saldo_cuota). El estado fiscal (cfg_estado_documento
--     _fiscal) NO se toca: "cobrada" = saldo_pendiente en 0.
--   * sp_adm_emitir_nota_debito inicializa saldo_pendiente = total_nota.
--   * sp_obtener_cliente_saldo v6: + ND vivas (estados fiscales 1/2). El
--     legacy ya sumaba el débito espejo 206 — el valor no cambia.
-- Antes la ND solo escribía el débito espejo: tras el corte de F7 la deuda
-- de una ND desaparecía del saldo y no había forma de cobrarla.
-- ============================================================================

BEGIN;

ALTER TABLE public.adm_nota_debito
    ADD COLUMN IF NOT EXISTS saldo_pendiente numeric(18,2) NOT NULL DEFAULT 0
        CHECK (saldo_pendiente >= 0);

-- Backfill: las ND vigentes (EMITIDA/APLICADA) aún no cobradas cargan su
-- total como saldo vivo (no hay cobros de ND por documento antes de F7).
UPDATE public.adm_nota_debito
SET saldo_pendiente = total_nota
WHERE estado_id IN (1, 2)
  AND saldo_pendiente = 0;

COMMENT ON COLUMN public.adm_nota_debito.saldo_pendiente IS
'F7 H2b: saldo vivo cobrable de la ND (el motor lo rebaja al aplicar pagos, documento_tipo=3). Cobrada = 0. El estado fiscal no cambia por cobros.';

CREATE INDEX IF NOT EXISTS ix_adm_nota_debito_cobrable
    ON public.adm_nota_debito (company_id, cliente_id)
    WHERE saldo_pendiente > 0;

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

    -- 8b. Reflejar la ND en el estado de cuenta del cliente (transaccion_abonado).
    -- Una ND AUMENTA el saldo del cliente → debitos = monto, saldo_detalle positivo.
    SELECT COALESCE(ta.saldo, 0)
    INTO v_saldo_anterior
    FROM public.transaccion_abonado ta
    WHERE ta.company_id = p_company_id
      AND ta.cliente_clave = v_factura.clientecodigo
      AND ta.estado = 'A'
    ORDER BY ta.ide DESC
    LIMIT 1;

    INSERT INTO public.transaccion_abonado (
        company_id, cliente_clave, tipotransaccion, docufuente,
        fecha_docu, tipo_partida, descripcion,
        debitos, creditos, saldo,
        estado, estado_id, fecha_registro, usuario, saldo_detalle
    )
    VALUES (
        p_company_id, v_factura.clientecodigo, '206', v_nota_id,
        current_date, '01',
        concat('N/D ', v_numero, ' s/factura ', v_factura.numfactura),
        v_monto, 0, COALESCE(v_saldo_anterior, 0) + v_monto,
        'A', 1, current_date, p_usuario_emisor, v_monto
    );

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

-- ----------------------------------------------------------------------------
-- Saldo del cliente v6: documentos pendientes + cuotas de plan activas +
-- NOTAS DE DÉBITO vivas + residuo migrado (muere en H3/H4). Misma firma.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(
    p_company_id     bigint,
    pcodigocliente   character varying)
RETURNS TABLE(saldo_actual numeric)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT
      COALESCE((
          SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = pcodigocliente
            AND f.estado IN ('A','B')
      ), 0)
    + COALESCE((
          SELECT SUM(dt.saldo_cuota)
          FROM public.cln_plan_pago_dtl dt
          JOIN public.cln_plan_pago_hdr h ON h.id = dt.idhdr
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = h.clienteid
                                        AND cm.company_id = h.company_id
          WHERE h.company_id = p_company_id
            AND h.estado_id  = 1
            AND cm.maestro_cliente_clave = pcodigocliente
            AND dt.estado_id IN (1, 4)
      ), 0)
    + COALESCE((
          -- F7 H2b: notas de débito vivas (estado fiscal EMITIDA/APLICADA).
          -- El legacy ya sumaba el débito espejo 206 — el valor no cambia.
          SELECT SUM(nd.saldo_pendiente)
          FROM public.adm_nota_debito nd
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = nd.cliente_id
                                        AND cm.company_id = nd.company_id
          WHERE nd.company_id = p_company_id
            AND cm.maestro_cliente_clave = pcodigocliente
            AND nd.estado_id IN (1, 2)
            AND nd.saldo_pendiente > 0
      ), 0)
    + COALESCE((
          SELECT SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))
          FROM public.vw_transaccion_abonado_vigente ta
          WHERE ta.company_id      = p_company_id
            AND ta.cliente_clave   = pcodigocliente
            AND ta.tipotransaccion IN ('SALDO_ANTERIOR', 'SALDO_INICIAL')
      ), 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'F7 H2b (2026-07-30): saldo = facturas A/B + cuotas de plan activas + notas de débito vivas + residuo migrado. Misma firma desde 2026-05-09.';

COMMIT;
