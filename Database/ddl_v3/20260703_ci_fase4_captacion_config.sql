-- =============================================================================
-- Integración Contable ↔ Comercial — Fase 4: captación / abonos / misceláneos
-- sobre la configuración única
-- Fecha: 2026-07-03
-- Plan: docs/plans/2026-07-02-plan-integracion-contable-comercial.md §5 Fase 4
--       (D2: las plantillas con_plantilla_partida_* y con_regla_integracion
--        dejan de consultarse desde los flujos — NO se dropean aquí.
--        D1: solo el motor único postea. D10: caja/misceláneos automáticos.)
--
-- Contenido:
--   1. fn_con_resolver_cuenta_modo — RESTAURACIÓN: la definición se creó en la
--      v1 del script de F3 y la v2 (rewrite del code review) la perdió; el
--      script de F3 comprometido la usa sin definirla, por lo que fallaría en
--      una BD limpia. Misma definición que quedó aplicada en los ambientes.
--   2. sp_con_generar_comprobante_config — comprobante automático por líneas
--      explícitas (reemplaza a sp_con_generar_comprobante/plantillas para los
--      flujos de captación, abonos y misceláneos): asiento por módulo desde
--      con_integracion_asiento (F2), numeración del motor (fn_con_siguiente_poliza,
--      F3), posteo vía sp_con_postear_poliza (motor único, D1), encolado en
--      con_partida_pendiente si no hay período abierto (según
--      con_integracion_config.encolar_sin_periodo), idempotente por documento.
--   3. sp_con_revertir_comprobante_config — reverso por documento: localiza la
--      partida del documento, la revierte vía sp_con_revertir_poliza y descarta
--      la pendiente viva si la hubiera. (De paso corrige el reverso de abonos,
--      que invocaba una sobrecarga de 5 argumentos de sp_con_revertir_poliza
--      que no existe.)
--   4. sp_con_procesar_partida_pendiente — reprocesa una pendiente de
--      comprobante (las de LOTE_FACTURACION se reprocesan con el lote de F3).
--
-- Idempotente. Producción: aplicar SOLO en ventana de deploy acordada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. fn_con_resolver_cuenta_modo — restauración (ver encabezado)
-- -----------------------------------------------------------------------------
-- Devuelve la cuenta para un uso aplicando el modo de granularidad de
-- con_integracion_config: GENERAL ignora dimensiones, POR_SERVICIO pasa solo
-- servicio, POR_SERVICIO_CATEGORIA pasa todo. Reusa fn_con_resolver_cuenta (F1).
CREATE OR REPLACE FUNCTION public.fn_con_resolver_cuenta_modo(
    p_company_id bigint,
    p_uso varchar,
    p_modo varchar,
    p_servicio_id bigint,
    p_categoria_servicio_id integer,
    p_con_medicion boolean
) RETURNS bigint
LANGUAGE sql
STABLE
AS $function$
    SELECT CASE p_modo
        WHEN 'POR_SERVICIO' THEN
            public.fn_con_resolver_cuenta(p_company_id, p_uso, p_servicio_id, NULL, NULL)
        WHEN 'POR_SERVICIO_CATEGORIA' THEN
            public.fn_con_resolver_cuenta(p_company_id, p_uso, p_servicio_id, p_categoria_servicio_id, p_con_medicion)
        ELSE
            public.fn_con_resolver_cuenta(p_company_id, p_uso, NULL, NULL, NULL)
    END;
$function$;

COMMENT ON FUNCTION public.fn_con_resolver_cuenta_modo(bigint, varchar, varchar, bigint, integer, boolean) IS
'Resuelve la cuenta de un uso aplicando el modo de granularidad configurado (GENERAL / POR_SERVICIO / POR_SERVICIO_CATEGORIA). Definida en F3 v1; restaurada en el script de F4 porque la v2 de F3 perdió la definición.';

-- -----------------------------------------------------------------------------
-- 2. sp_con_generar_comprobante_config — comprobante por líneas explícitas
-- -----------------------------------------------------------------------------
-- p_lineas: array jsonb [{"account_id":123,"debe":10.00,"haber":0,"descripcion":"..."}].
-- El llamador resuelve las cuentas vía fn_con_resolver_cuenta / _modo (F4):
-- la función NO consulta plantillas ni reglas (D2).
-- Devuelve poliza_id, o NULL si la partida quedó ENCOLADA en con_partida_pendiente.
CREATE OR REPLACE FUNCTION public.sp_con_generar_comprobante_config(
    p_company_id bigint,
    p_module varchar,
    p_document_type varchar,
    p_document_id bigint,
    p_document_number text,
    p_poliza_date date,
    p_description text,
    p_user text,
    p_lineas jsonb
) RETURNS bigint
LANGUAGE plpgsql
AS $function$
DECLARE
    v_module varchar := upper(btrim(p_module));
    v_doc_type varchar := upper(btrim(p_document_type));
    v_poliza_id bigint;
    v_encolar boolean;
    v_asiento record;
    v_period_id bigint;
    v_numero record;
    v_total_debe numeric := 0;
    v_total_haber numeric := 0;
    v_lineas_validas integer := 0;
    v_line smallint := 0;
    v_linea record;
    v_invalidas text;
    v_pend_actualizadas integer;
BEGIN
    IF p_document_id IS NULL OR p_document_id <= 0 THEN
        RAISE EXCEPTION 'document_id inválido (%) para generar comprobante %/%.',
            p_document_id, v_module, v_doc_type;
    END IF;

    -- Idempotencia por documento (misma semántica que sp_con_generar_comprobante).
    SELECT h.poliza_id INTO v_poliza_id
    FROM public.con_partida_hdr h
    WHERE h.company_id = p_company_id
      AND h.module = v_module
      AND h.document_type = v_doc_type
      AND h.document_id = p_document_id
    LIMIT 1;
    IF v_poliza_id IS NOT NULL THEN
        RETURN v_poliza_id;
    END IF;

    SELECT c.encolar_sin_periodo INTO v_encolar
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La empresa % no tiene configuración de integración contable (pantalla Integración Contable / perfil ERSAPS).', p_company_id;
    END IF;

    -- Asiento del módulo (F2): diario + tipo de partida.
    SELECT a.journal_id, a.type_id INTO v_asiento
    FROM public.con_integracion_asiento a
    WHERE a.company_id = p_company_id AND a.module = v_module;
    IF NOT FOUND OR v_asiento.journal_id IS NULL OR v_asiento.type_id IS NULL THEN
        RAISE EXCEPTION 'El módulo % no tiene diario y tipo de partida configurados (pestaña Asientos de Integración Contable).', v_module;
    END IF;

    -- Validación de líneas: estructura, montos y cuentas posteables del tenant.
    IF p_lineas IS NULL OR jsonb_typeof(p_lineas) <> 'array' OR jsonb_array_length(p_lineas) = 0 THEN
        RAISE EXCEPTION 'El comprobante %/%-% no recibió líneas contables.', v_module, v_doc_type, p_document_id;
    END IF;

    DROP TABLE IF EXISTS pg_temp.tmp_comprobante_lineas;
    CREATE TEMP TABLE tmp_comprobante_lineas ON COMMIT DROP AS
    SELECT (e->>'account_id')::bigint AS account_id,
           round(COALESCE((e->>'debe')::numeric, 0), 2) AS debe,
           round(COALESCE((e->>'haber')::numeric, 0), 2) AS haber,
           NULLIF(btrim(e->>'descripcion'), '') AS descripcion
    FROM jsonb_array_elements(p_lineas) AS e;

    DELETE FROM tmp_comprobante_lineas WHERE debe = 0 AND haber = 0;

    IF EXISTS (SELECT 1 FROM tmp_comprobante_lineas WHERE debe < 0 OR haber < 0 OR account_id IS NULL) THEN
        RAISE EXCEPTION 'El comprobante %/%-% tiene líneas con cuenta nula o montos negativos.', v_module, v_doc_type, p_document_id;
    END IF;

    SELECT COUNT(*), COALESCE(SUM(debe), 0), COALESCE(SUM(haber), 0)
    INTO v_lineas_validas, v_total_debe, v_total_haber
    FROM tmp_comprobante_lineas;

    IF v_lineas_validas < 2 THEN
        RAISE EXCEPTION 'El comprobante %/%-% requiere al menos dos líneas con monto.', v_module, v_doc_type, p_document_id;
    END IF;

    IF v_total_debe <> v_total_haber OR v_total_debe <= 0 THEN
        RAISE EXCEPTION 'El comprobante %/%-% no está balanceado (debe=%, haber=%).',
            v_module, v_doc_type, p_document_id, v_total_debe, v_total_haber;
    END IF;

    SELECT string_agg(DISTINCT l.account_id::text, ', ') INTO v_invalidas
    FROM tmp_comprobante_lineas l
    WHERE NOT EXISTS (
        SELECT 1 FROM public.con_plan_cuentas c
        WHERE c.company_id = p_company_id
          AND c.account_id = l.account_id
          AND c.allows_posting);
    IF v_invalidas IS NOT NULL THEN
        RAISE EXCEPTION 'El comprobante %/%-% referencia cuentas inexistentes o no posteables de la empresa %: %.',
            v_module, v_doc_type, p_document_id, p_company_id, v_invalidas;
    END IF;

    -- Período contable abierto por fecha (semántica del motor).
    v_period_id := public.fn_con_periodo_abierto(p_company_id, p_poliza_date);

    IF v_period_id IS NULL THEN
        IF NOT v_encolar THEN
            RAISE EXCEPTION 'No hay período contable abierto para la fecha % y la configuración no permite encolar.', p_poliza_date;
        END IF;

        -- Encolado idempotente por documento: si ya hay pendiente viva, solo
        -- incrementa intentos (no duplica cola).
        UPDATE public.con_partida_pendiente pp
        SET intentos = pp.intentos + 1, updated_at = now(), updated_by = p_user
        WHERE pp.company_id = p_company_id
          AND pp.module = v_module
          AND pp.origen_tipo = v_doc_type
          AND pp.origen_id = p_document_id
          AND pp.status_id = 1;
        GET DIAGNOSTICS v_pend_actualizadas = ROW_COUNT;

        IF v_pend_actualizadas = 0 THEN
            INSERT INTO public.con_partida_pendiente
                (company_id, module, origen_tipo, origen_id, origen_referencia,
                 fecha_documento, descripcion, payload, motivo, intentos, created_by)
            VALUES
                (p_company_id, v_module, v_doc_type, p_document_id, left(p_document_number, 120),
                 p_poliza_date, left(p_description, 500),
                 jsonb_build_object(
                     'module', v_module,
                     'document_type', v_doc_type,
                     'document_id', p_document_id,
                     'document_number', p_document_number,
                     'poliza_date', p_poliza_date,
                     'description', p_description,
                     'lineas', p_lineas),
                 'SIN_PERIODO_ABIERTO', 1, p_user);
        END IF;

        RETURN NULL;
    END IF;

    -- Encabezado con la numeración del motor (lock + correlativo mensual, F3).
    SELECT * INTO v_numero FROM public.fn_con_siguiente_poliza(p_company_id, p_poliza_date);

    INSERT INTO public.con_partida_hdr (
        company_id, journal_id, period_id, module, document_type,
        document_id, document_number, poliza_number, sequence_number,
        poliza_date, description, status, source_reference,
        created_at, created_by, type_id, total_debit, total_credit
    ) VALUES (
        p_company_id, v_asiento.journal_id, v_period_id, v_module, v_doc_type,
        p_document_id, left(p_document_number, 50), v_numero.poliza_number, v_numero.seq,
        p_poliza_date, left(p_description, 500), 0, left(p_document_number, 50),
        now(), COALESCE(p_user, current_user), v_asiento.type_id, 0, 0
    )
    RETURNING poliza_id INTO v_poliza_id;

    FOR v_linea IN
        SELECT l.account_id,
               round(SUM(l.debe), 2) AS debe,
               round(SUM(l.haber), 2) AS haber,
               MIN(l.descripcion) AS descripcion
        FROM tmp_comprobante_lineas l
        GROUP BY l.account_id
        HAVING round(SUM(l.debe), 2) <> 0 OR round(SUM(l.haber), 2) <> 0
        ORDER BY SUM(l.debe) < SUM(l.haber), l.account_id
    LOOP
        v_line := v_line + 1;
        INSERT INTO public.con_partida_dtl (
            company_id, poliza_id, line_number, account_id,
            debit_amount, credit_amount, description, source_document
        ) VALUES (
            p_company_id, v_poliza_id, v_line, v_linea.account_id,
            v_linea.debe, v_linea.haber,
            COALESCE(v_linea.descripcion, p_description),
            left(p_document_number, 120)
        );
    END LOOP;

    UPDATE public.con_partida_hdr h
    SET total_debit = v_total_debe, total_credit = v_total_haber
    WHERE h.poliza_id = v_poliza_id;

    -- Motor único (D1).
    PERFORM public.sp_con_postear_poliza(p_company_id, v_poliza_id, p_user);

    RETURN v_poliza_id;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_generar_comprobante_config(bigint, varchar, varchar, bigint, text, date, text, text, jsonb) IS
'Comprobante automático de la integración contable por configuración (plan F4, D2). El llamador arma las líneas resolviendo cuentas vía fn_con_resolver_cuenta / fn_con_resolver_cuenta_modo; esta función valida (balance, cuentas posteables del tenant), numera con fn_con_siguiente_poliza, postea vía sp_con_postear_poliza (motor único D1) y, si no hay período abierto, encola en con_partida_pendiente según encolar_sin_periodo (devuelve NULL). Idempotente por (module, document_type, document_id).';

-- -----------------------------------------------------------------------------
-- 3. sp_con_revertir_comprobante_config — reverso por documento
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_con_revertir_comprobante_config(
    p_company_id bigint,
    p_module varchar,
    p_document_types varchar[],
    p_document_id bigint,
    p_user text
) RETURNS bigint
LANGUAGE plpgsql
AS $function$
DECLARE
    v_module varchar := upper(btrim(p_module));
    v_doc_types varchar[] := (SELECT array_agg(upper(btrim(t))) FROM unnest(p_document_types) AS t);
    v_poliza_id bigint;
BEGIN
    -- Descartar la pendiente viva del documento (si quedó encolada, no hay
    -- partida que revertir pero la cola no debe regenerarla).
    UPDATE public.con_partida_pendiente pp
    SET status_id = 3, updated_at = now(), updated_by = p_user,
        ultimo_error = 'Descartada por reverso del documento origen'
    WHERE pp.company_id = p_company_id
      AND pp.module = v_module
      AND pp.origen_tipo = ANY (v_doc_types)
      AND pp.origen_id = p_document_id
      AND pp.status_id = 1;

    SELECT h.poliza_id INTO v_poliza_id
    FROM public.con_partida_hdr h
    WHERE h.company_id = p_company_id
      AND h.module = v_module
      AND h.document_type = ANY (v_doc_types)
      AND h.document_id = p_document_id
    ORDER BY CASE WHEN h.status = 1 THEN 0 ELSE 1 END,
             h.poliza_id DESC
    LIMIT 1;

    IF v_poliza_id IS NULL THEN
        RETURN NULL;
    END IF;

    PERFORM public.sp_con_revertir_poliza(p_company_id, v_poliza_id, p_user);
    RETURN v_poliza_id;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_revertir_comprobante_config(bigint, varchar, varchar[], bigint, text) IS
'Reverso del comprobante de un documento comercial (plan F4): descarta la pendiente viva de con_partida_pendiente y revierte la partida del documento vía sp_con_revertir_poliza (motor único). Devuelve el poliza_id revertido o NULL si el documento no tenía partida.';

-- -----------------------------------------------------------------------------
-- 4. sp_con_procesar_partida_pendiente — reprocesa una pendiente de comprobante
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_con_procesar_partida_pendiente(
    p_company_id bigint,
    p_partida_pendiente_id bigint,
    p_user text
) RETURNS bigint
LANGUAGE plpgsql
AS $function$
DECLARE
    v_pendiente record;
    v_poliza_id bigint;
BEGIN
    SELECT * INTO v_pendiente
    FROM public.con_partida_pendiente pp
    WHERE pp.company_id = p_company_id
      AND pp.partida_pendiente_id = p_partida_pendiente_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'La pendiente % no existe para la empresa %.', p_partida_pendiente_id, p_company_id;
    END IF;

    IF v_pendiente.status_id <> 1 THEN
        RAISE EXCEPTION 'La pendiente % no está en estado PENDIENTE (status_id=%).', p_partida_pendiente_id, v_pendiente.status_id;
    END IF;

    IF v_pendiente.origen_tipo = 'LOTE_FACTURACION' THEN
        RAISE EXCEPTION 'Las pendientes del lote de facturación se reprocesan desde la pantalla del lote (sp_con_generar_partidas_facturacion).';
    END IF;

    v_poliza_id := public.sp_con_generar_comprobante_config(
        p_company_id,
        v_pendiente.payload->>'module',
        v_pendiente.payload->>'document_type',
        (v_pendiente.payload->>'document_id')::bigint,
        v_pendiente.payload->>'document_number',
        (v_pendiente.payload->>'poliza_date')::date,
        v_pendiente.payload->>'description',
        p_user,
        v_pendiente.payload->'lineas');

    IF v_poliza_id IS NOT NULL THEN
        UPDATE public.con_partida_pendiente pp
        SET status_id = 2, poliza_id = v_poliza_id,
            procesada_at = now(), procesada_by = p_user,
            updated_at = now(), updated_by = p_user
        WHERE pp.partida_pendiente_id = p_partida_pendiente_id;
    ELSE
        UPDATE public.con_partida_pendiente pp
        SET ultimo_error = 'Sigue sin período contable abierto para la fecha del documento',
            updated_at = now(), updated_by = p_user
        WHERE pp.partida_pendiente_id = p_partida_pendiente_id;
    END IF;

    RETURN v_poliza_id;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_procesar_partida_pendiente(bigint, bigint, text) IS
'Reprocesa una pendiente de comprobante de con_partida_pendiente (plan F4): reinvoca sp_con_generar_comprobante_config con el payload guardado; si postea marca PROCESADA con su poliza_id, si sigue sin período deja la pendiente viva (intentos los incrementa el dedup del encolado). Las de LOTE_FACTURACION se reprocesan con el lote (F3).';
