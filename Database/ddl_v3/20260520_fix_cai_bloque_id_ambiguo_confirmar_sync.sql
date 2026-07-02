-- ============================================================
-- FIX 2026-05-20 — error 42702 «cai_bloque_id» ambigua
-- ============================================================
-- sp_adm_confirmar_correlativo_cai_sync declara RETURNS TABLE(... cai_bloque_id ...)
-- por lo que 'cai_bloque_id' es una variable de salida de la funcion.
-- Los dos UPDATE a public.adm_cai_bloque_reservado referencian la columna
-- 'cai_bloque_id' sin alias de tabla en el WHERE -> PostgreSQL no puede
-- distinguir entre la columna de la tabla y la variable OUT -> 42702.
--
-- Sintoma: al sincronizar una factura del app lector el WS responde
-- OK_WITH_SYNC_CONFLICT y se registra un conflicto SYNC_CONFIRM_ERROR con
-- detalle "42702: la referencia a la columna «cai_bloque_id» es ambigua".
-- La factura se crea pero el correlativo CAI no se confirma.
--
-- Fix: calificar los UPDATE a adm_cai_bloque_reservado con alias de tabla 'b'.
-- (sp_adm_prepare_correlativo_cai_sync NO requiere fix: su unico acceso a
--  cai_bloque_id ya esta calificado con alias en el SELECT.)
-- ============================================================

CREATE OR REPLACE FUNCTION public.sp_adm_confirmar_correlativo_cai_sync(p_company_id bigint, p_cliente_id bigint, p_id_cai bigint, p_correlativo bigint, p_numero_factura character varying, p_lectura_uuid character varying DEFAULT NULL::character varying, p_factura_id bigint DEFAULT NULL::bigint, p_usuario character varying DEFAULT CURRENT_USER)
 RETURNS TABLE(success boolean, estado_codigo character varying, cai_bloque_id bigint, factura_id bigint, mensaje text)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_uuid varchar := NULLIF(BTRIM(COALESCE(p_lectura_uuid, '')), '');
    v_numero varchar := NULLIF(BTRIM(COALESCE(p_numero_factura, '')), '');
    v_bloque record;
    v_existente public.adm_cai_correlativo_emitido%ROWTYPE;
    v_usuario_eff varchar := COALESCE(NULLIF(BTRIM(p_usuario), ''), current_user);
BEGIN
    IF p_id_cai IS NULL OR p_id_cai <= 0 OR p_correlativo IS NULL OR p_correlativo <= 0 OR v_numero IS NULL THEN
        RAISE EXCEPTION 'CAI_DATOS_REQUERIDOS: id_cai, correlativo y numero_factura son requeridos.';
    END IF;

    SELECT
        b.cai_bloque_id
    INTO v_bloque
    FROM public.adm_cai_bloque_reservado b
    WHERE b.company_id = p_company_id
      AND b.cai_id = p_id_cai
      AND p_correlativo BETWEEN b.correlativo_desde AND b.correlativo_hasta
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'BLOQUE_INVALIDO: no se encontro bloque reservado para confirmar el correlativo %.', p_correlativo;
    END IF;

    SELECT *
    INTO v_existente
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.company_id = p_company_id
      AND (
            (v_uuid IS NOT NULL AND e.lectura_uuid = v_uuid)
            OR e.numero_factura = v_numero
            OR (e.cai_id = p_id_cai AND e.correlativo = p_correlativo)
          )
    ORDER BY e.cai_correlativo_emitido_id DESC
    LIMIT 1;

    IF FOUND THEN
        IF COALESCE(v_existente.cai_id, 0) <> p_id_cai
           OR COALESCE(v_existente.correlativo, 0) <> p_correlativo
           OR v_existente.numero_factura <> v_numero THEN
            RAISE EXCEPTION 'FACTURA_YA_CONFIRMADA: existe un registro previo incompatible para este correlativo.';
        END IF;

        UPDATE public.adm_cai_correlativo_emitido AS e
        SET cliente_id = COALESCE(p_cliente_id, e.cliente_id),
            factura_id = COALESCE(p_factura_id, e.factura_id),
            estado_codigo = 'CONFIRMADO',
            fecha_confirmacion = now(),
            detalle_conflicto = NULL,
            updated_at = now(),
            updated_by = v_usuario_eff
        WHERE e.cai_correlativo_emitido_id = v_existente.cai_correlativo_emitido_id;

        -- BUGFIX 2026-05-14 #3: avanzar correlativo_actual en bloque y CAI.
        -- GREATEST evita retroceso ante confirmaciones fuera de orden.
        -- FIX 2026-05-20: alias 'b' para evitar 42702 (cai_bloque_id ambigua con variable OUT).
        UPDATE public.adm_cai_bloque_reservado AS b
        SET correlativo_actual = GREATEST(b.correlativo_actual, p_correlativo),
            updated_at = now(),
            updated_by = v_usuario_eff
        WHERE b.company_id = p_company_id
          AND b.cai_bloque_id = v_bloque.cai_bloque_id
          AND b.correlativo_actual < p_correlativo;

        UPDATE public.adm_cai_facturacion AS c
        SET correlativo_actual = GREATEST(c.correlativo_actual, p_correlativo),
            updated_at = now(),
            updated_by = v_usuario_eff
        WHERE c.company_id = p_company_id
          AND c.cai_id = p_id_cai
          AND c.correlativo_actual < p_correlativo;

        success := true;
        estado_codigo := 'CONFIRMADO';
        cai_bloque_id := v_existente.cai_bloque_id;
        factura_id := COALESCE(p_factura_id, v_existente.factura_id);
        mensaje := 'Correlativo confirmado correctamente.';
        RETURN NEXT;
        RETURN;
    END IF;

    INSERT INTO public.adm_cai_correlativo_emitido (
        company_id,
        cai_bloque_id,
        cai_id,
        correlativo,
        numero_factura,
        cliente_id,
        lectura_uuid,
        factura_id,
        estado_codigo,
        fecha_confirmacion,
        created_by
    )
    VALUES (
        p_company_id,
        v_bloque.cai_bloque_id,
        p_id_cai,
        p_correlativo,
        v_numero,
        p_cliente_id,
        v_uuid,
        p_factura_id,
        'CONFIRMADO',
        now(),
        v_usuario_eff
    );

    -- BUGFIX 2026-05-14 #3: tambien al confirmar sin row previo de prepare.
    -- FIX 2026-05-20: alias 'b' para evitar 42702 (cai_bloque_id ambigua con variable OUT).
    UPDATE public.adm_cai_bloque_reservado AS b
    SET correlativo_actual = GREATEST(b.correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE b.company_id = p_company_id
      AND b.cai_bloque_id = v_bloque.cai_bloque_id
      AND b.correlativo_actual < p_correlativo;

    UPDATE public.adm_cai_facturacion AS c
    SET correlativo_actual = GREATEST(c.correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE c.company_id = p_company_id
      AND c.cai_id = p_id_cai
      AND c.correlativo_actual < p_correlativo;

    success := true;
    estado_codigo := 'CONFIRMADO';
    cai_bloque_id := v_bloque.cai_bloque_id;
    factura_id := p_factura_id;
    mensaje := 'Correlativo confirmado correctamente.';
    RETURN NEXT;
END;
$function$;
