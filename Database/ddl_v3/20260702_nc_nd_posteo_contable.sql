-- =============================================================================
-- NC/ND V3 — Posteo contable automático (cierra el hueco detectado en
-- HANDOFF_OPERACION_CONTABLE_FACTURACION_2026-03-31 §4: las notas de crédito
-- y débito se registraban en operacional pero NUNCA generaban partida contable)
-- Fecha: 2026-07-02
--
-- Contenido:
--   1. Columna poliza_id en adm_nota_credito / adm_nota_debito (trazabilidad).
--   2. Seed por empresa activa: cfg_document_type VENTAS/NC y VENTAS/ND,
--      reglas con_regla_integracion y plantillas con_plantilla_partida_*.
--      Las cuentas se derivan de la plantilla de facturación VENTAS/FAC para
--      garantizar simetría contable:
--        Factura: DEBE CxC        / HABER Ingresos
--        NC:      DEBE Ingresos   / HABER CxC      (reversa la venta)
--        ND:      DEBE CxC        / HABER Ingresos (amplía la venta)
--   3. Recrea sp_adm_emitir_nota_credito / sp_adm_emitir_nota_debito con el
--      paso de posteo (9c / 8c): llama sp_con_generar_comprobante DENTRO de
--      la misma transacción — si no hay plantilla o el período contable está
--      cerrado, la emisión completa se revierte (atómico).
--
-- Prerequisitos: 20260514_nc_nd_v3_modelo.sql,
--   20260122_contabilidad_comprobantes_cobranza_facturacion.sql (motor central).
--
-- Idempotente. NO tocar Database/Backups ni correr en SQL Server.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Trazabilidad nota → partida contable
-- -----------------------------------------------------------------------------
ALTER TABLE public.adm_nota_credito ADD COLUMN IF NOT EXISTS poliza_id bigint;
ALTER TABLE public.adm_nota_debito  ADD COLUMN IF NOT EXISTS poliza_id bigint;

COMMENT ON COLUMN public.adm_nota_credito.poliza_id IS
'Partida contable (con_partida_hdr) generada automáticamente al emitir la NC.';
COMMENT ON COLUMN public.adm_nota_debito.poliza_id IS
'Partida contable (con_partida_hdr) generada automáticamente al emitir la ND.';

-- -----------------------------------------------------------------------------
-- 2. Seed: document types, reglas y plantillas VENTAS/NC y VENTAS/ND
-- -----------------------------------------------------------------------------
DO $$
DECLARE
    v_company record;
    v_cxc_account_id bigint;
    v_ing_account_id bigint;
    v_doc_type_nc bigint;
    v_doc_type_nd bigint;
    v_template_id bigint;
BEGIN
    FOR v_company IN
        SELECT company_id
          FROM public.cfg_company
         WHERE upper(status) = 'ACTIVE'
         ORDER BY company_id
    LOOP
        -- 2.1 Derivar cuentas desde la plantilla activa VENTAS/FAC:
        --     línea con debit_formula → CxC; línea con credit_formula → Ingresos.
        SELECT d.account_id
          INTO v_cxc_account_id
          FROM public.con_plantilla_partida_hdr h
          JOIN public.con_plantilla_partida_dtl d
            ON d.template_id = h.template_id AND d.company_id = h.company_id
         WHERE h.company_id = v_company.company_id
           AND h.module = 'VENTAS' AND h.document_type = 'FAC' AND h.is_active
           AND d.debit_formula IS NOT NULL
         ORDER BY h.template_id DESC, d.line_number
         LIMIT 1;

        SELECT d.account_id
          INTO v_ing_account_id
          FROM public.con_plantilla_partida_hdr h
          JOIN public.con_plantilla_partida_dtl d
            ON d.template_id = h.template_id AND d.company_id = h.company_id
         WHERE h.company_id = v_company.company_id
           AND h.module = 'VENTAS' AND h.document_type = 'FAC' AND h.is_active
           AND d.credit_formula IS NOT NULL
         ORDER BY h.template_id DESC, d.line_number
         LIMIT 1;

        -- Fallback: regla de integración activa de VENTAS/FAC.
        IF v_cxc_account_id IS NULL OR v_ing_account_id IS NULL THEN
            SELECT r.debit_account_id, r.credit_account_id
              INTO v_cxc_account_id, v_ing_account_id
              FROM public.con_regla_integracion r
              JOIN public.cfg_document_type dt ON dt.document_type_id = r.document_type_id
             WHERE r.company_id = v_company.company_id
               AND dt.module = 'VENTAS' AND dt.code = 'FAC'
               AND r.is_active = true
             ORDER BY r.updated_at DESC NULLS LAST, r.regla_id DESC
             LIMIT 1;
        END IF;

        IF v_cxc_account_id IS NULL OR v_ing_account_id IS NULL THEN
            RAISE NOTICE 'SKIP company %: sin plantilla/regla VENTAS-FAC para derivar cuentas NC/ND.',
                v_company.company_id;
            CONTINUE;
        END IF;

        -- 2.2 cfg_document_type VENTAS/NC y VENTAS/ND (las notas SÍ llevan CAI).
        INSERT INTO public.cfg_document_type (
            company_id, module, code, name, description, requires_cai, is_active,
            created_at, created_by
        ) VALUES (
            v_company.company_id, 'VENTAS', 'NC',
            'Nota de Crédito', 'Nota de crédito fiscal (SAR) — disminuye la venta',
            true, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, code) DO UPDATE
           SET is_active = true, updated_at = now(), updated_by = 'system'
        RETURNING document_type_id INTO v_doc_type_nc;

        INSERT INTO public.cfg_document_type (
            company_id, module, code, name, description, requires_cai, is_active,
            created_at, created_by
        ) VALUES (
            v_company.company_id, 'VENTAS', 'ND',
            'Nota de Débito', 'Nota de débito fiscal (SAR) — amplía la venta',
            true, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, code) DO UPDATE
           SET is_active = true, updated_at = now(), updated_by = 'system'
        RETURNING document_type_id INTO v_doc_type_nd;

        -- 2.3 Reglas de integración (documentan las cuentas por escenario).
        INSERT INTO public.con_regla_integracion (
            company_id, module, document_type_id, scenario_code, description,
            debit_account_id, credit_account_id, is_active, created_at, created_by
        ) VALUES (
            v_company.company_id, 'VENTAS', v_doc_type_nc, 'NC_EMISION',
            'Emisión de Nota de Crédito (reversa venta)',
            v_ing_account_id, v_cxc_account_id, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, scenario_code) DO NOTHING;

        INSERT INTO public.con_regla_integracion (
            company_id, module, document_type_id, scenario_code, description,
            debit_account_id, credit_account_id, is_active, created_at, created_by
        ) VALUES (
            v_company.company_id, 'VENTAS', v_doc_type_nd, 'ND_EMISION',
            'Emisión de Nota de Débito (amplía venta)',
            v_cxc_account_id, v_ing_account_id, true, now(), 'system'
        )
        ON CONFLICT (company_id, module, scenario_code) DO NOTHING;

        -- 2.4 Plantilla VENTAS/NC: DEBE Ingresos / HABER CxC.
        SELECT template_id INTO v_template_id
          FROM public.con_plantilla_partida_hdr
         WHERE company_id = v_company.company_id
           AND module = 'VENTAS' AND document_type = 'NC'
           AND upper(btrim(name)) = 'VENTAS NC EMISION'
         LIMIT 1;

        IF v_template_id IS NULL THEN
            INSERT INTO public.con_plantilla_partida_hdr (
                company_id, module, document_type, name, description, is_active,
                created_at, created_by, updated_at, updated_by
            ) VALUES (
                v_company.company_id, 'VENTAS', 'NC', 'VENTAS NC EMISION',
                'Plantilla emisión Nota de Crédito — reversa de venta',
                true, now(), 'system', now(), 'system'
            )
            RETURNING template_id INTO v_template_id;
        ELSE
            UPDATE public.con_plantilla_partida_hdr
               SET is_active = true, updated_at = now(), updated_by = 'system'
             WHERE template_id = v_template_id;
        END IF;

        DELETE FROM public.con_plantilla_partida_dtl
         WHERE company_id = v_company.company_id AND template_id = v_template_id;

        INSERT INTO public.con_plantilla_partida_dtl (
            company_id, template_id, line_number, account_id,
            debit_formula, credit_formula, description
        ) VALUES
            (v_company.company_id, v_template_id, 1, v_ing_account_id,
             '{total}', NULL, 'Debe: reversión de ingresos por N/C'),
            (v_company.company_id, v_template_id, 2, v_cxc_account_id,
             NULL, '{total}', 'Haber: disminución CxC clientes por N/C');

        -- 2.5 Plantilla VENTAS/ND: DEBE CxC / HABER Ingresos.
        v_template_id := NULL;
        SELECT template_id INTO v_template_id
          FROM public.con_plantilla_partida_hdr
         WHERE company_id = v_company.company_id
           AND module = 'VENTAS' AND document_type = 'ND'
           AND upper(btrim(name)) = 'VENTAS ND EMISION'
         LIMIT 1;

        IF v_template_id IS NULL THEN
            INSERT INTO public.con_plantilla_partida_hdr (
                company_id, module, document_type, name, description, is_active,
                created_at, created_by, updated_at, updated_by
            ) VALUES (
                v_company.company_id, 'VENTAS', 'ND', 'VENTAS ND EMISION',
                'Plantilla emisión Nota de Débito — ampliación de venta',
                true, now(), 'system', now(), 'system'
            )
            RETURNING template_id INTO v_template_id;
        ELSE
            UPDATE public.con_plantilla_partida_hdr
               SET is_active = true, updated_at = now(), updated_by = 'system'
             WHERE template_id = v_template_id;
        END IF;

        DELETE FROM public.con_plantilla_partida_dtl
         WHERE company_id = v_company.company_id AND template_id = v_template_id;

        INSERT INTO public.con_plantilla_partida_dtl (
            company_id, template_id, line_number, account_id,
            debit_formula, credit_formula, description
        ) VALUES
            (v_company.company_id, v_template_id, 1, v_cxc_account_id,
             '{total}', NULL, 'Debe: aumento CxC clientes por N/D'),
            (v_company.company_id, v_template_id, 2, v_ing_account_id,
             NULL, '{total}', 'Haber: ingresos adicionales por N/D');

        RAISE NOTICE 'Seed NC/ND company %: CxC=% Ingresos=%',
            v_company.company_id, v_cxc_account_id, v_ing_account_id;

        v_cxc_account_id := NULL;
        v_ing_account_id := NULL;
        v_template_id := NULL;
    END LOOP;
END;
$$;

-- -----------------------------------------------------------------------------
-- 3. sp_adm_emitir_nota_credito — se agrega paso 9c (posteo contable)
--    Cuerpo idéntico a 20260514_nc_nd_v3_modelo.sql salvo el posteo.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_credito(
    p_company_id bigint,
    p_factura_origen_id integer,
    p_motivo_anulacion_id smallint,
    p_motivo_detalle varchar,
    p_monto_disminuir numeric,          -- NULL = total de la factura origen
    p_lineas jsonb,                     -- NULL = copia las líneas de la factura origen
    p_usuario_emisor varchar,
    p_cai_id bigint                     -- CAI específico tipo NC
)
RETURNS TABLE (
    success boolean,
    codigo text,
    mensaje text,
    nota_credito_id bigint,
    numero_documento text,
    correlativo bigint
)
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
    v_type_id bigint;
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
    END IF;

    -- 9b. Reflejar la NC en el estado de cuenta del cliente (transaccion_abonado).
    -- Una NC DISMINUYE el saldo del cliente → creditos = monto, saldo_detalle negativo.
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
        p_company_id, v_factura.clientecodigo, '205', v_nota_id,
        current_date, '01',
        concat('N/C ', v_numero, ' s/factura ', v_factura.numfactura),
        0, v_monto, COALESCE(v_saldo_anterior, 0) - v_monto,
        'A', 1, current_date, p_usuario_emisor, -v_monto
    );

    -- 9c. Posteo contable automático vía motor central (plantilla VENTAS/NC:
    -- DEBE Ingresos / HABER CxC). Misma transacción: si no hay plantilla o el
    -- período contable está cerrado, la emisión completa se revierte.
    SELECT tt.type_id
    INTO v_type_id
    FROM public.con_tipo_transaccion tt
    WHERE tt.company_id = p_company_id
      AND COALESCE(tt.status_id,
                   CASE WHEN upper(tt.status) = 'ACTIVE' THEN 1 ELSE 0 END) = 1
    ORDER BY (upper(tt.code) = 'NC') DESC, tt.is_default DESC, tt.type_id
    LIMIT 1;

    v_poliza_id := public.sp_con_generar_comprobante(
        p_company_id,
        'VENTAS',
        'NC',
        v_nota_id,
        v_numero,
        current_date,
        concat('N/C ', v_numero, ' s/factura ', v_factura.numfactura),
        p_usuario_emisor,
        NULL,
        COALESCE(v_type_id, 0),
        NULL,
        jsonb_build_object('total', v_monto),
        false);

    UPDATE public.adm_nota_credito
    SET poliza_id = v_poliza_id
    WHERE adm_nota_credito.nota_credito_id = v_nota_id;

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
$function$;

-- -----------------------------------------------------------------------------
-- 4. sp_adm_emitir_nota_debito — se agrega paso 8c (posteo contable)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_debito(
    p_company_id bigint,
    p_factura_origen_id integer,
    p_motivo_aumento_id smallint,
    p_motivo_detalle varchar,
    p_monto_aumentar numeric,            -- requerido (un ND siempre indica cuánto aumenta)
    p_lineas jsonb,                      -- NULL = una sola línea con el motivo
    p_usuario_emisor varchar,
    p_cai_id bigint                      -- CAI específico tipo ND
)
RETURNS TABLE (
    success boolean,
    codigo text,
    mensaje text,
    nota_debito_id bigint,
    numero_documento text,
    correlativo bigint
)
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
    v_type_id bigint;
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
        estado_id, usuario_emisor, created_by
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
        1, p_usuario_emisor, p_usuario_emisor
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

    -- 8c. Posteo contable automático vía motor central (plantilla VENTAS/ND:
    -- DEBE CxC / HABER Ingresos). Misma transacción: si no hay plantilla o el
    -- período contable está cerrado, la emisión completa se revierte.
    SELECT tt.type_id
    INTO v_type_id
    FROM public.con_tipo_transaccion tt
    WHERE tt.company_id = p_company_id
      AND COALESCE(tt.status_id,
                   CASE WHEN upper(tt.status) = 'ACTIVE' THEN 1 ELSE 0 END) = 1
    ORDER BY (upper(tt.code) = 'ND') DESC, tt.is_default DESC, tt.type_id
    LIMIT 1;

    v_poliza_id := public.sp_con_generar_comprobante(
        p_company_id,
        'VENTAS',
        'ND',
        v_nota_id,
        v_numero,
        current_date,
        concat('N/D ', v_numero, ' s/factura ', v_factura.numfactura),
        p_usuario_emisor,
        NULL,
        COALESCE(v_type_id, 0),
        NULL,
        jsonb_build_object('total', v_monto),
        false);

    UPDATE public.adm_nota_debito
    SET poliza_id = v_poliza_id
    WHERE adm_nota_debito.nota_debito_id = v_nota_id;

    -- 9. Resultado (un ND NO anula la factura origen, solo aumenta el saldo del cliente)
    RETURN QUERY SELECT
        true,
        'OK'::text,
        'Nota de débito emitida correctamente.'::text,
        v_nota_id,
        v_numero,
        v_correlativo;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_emitir_nota_credito(bigint, integer, smallint, varchar, numeric, jsonb, varchar, bigint) IS
'Emite NC V3 (SAR) y postea automáticamente la partida contable (VENTAS/NC: Debe Ingresos / Haber CxC) vía sp_con_generar_comprobante. Atómico.';
COMMENT ON FUNCTION public.sp_adm_emitir_nota_debito(bigint, integer, smallint, varchar, numeric, jsonb, varchar, bigint) IS
'Emite ND V3 (SAR) y postea automáticamente la partida contable (VENTAS/ND: Debe CxC / Haber Ingresos) vía sp_con_generar_comprobante. Atómico.';
