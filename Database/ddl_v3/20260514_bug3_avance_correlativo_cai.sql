-- =============================================================================
-- BUGFIX #3 — Avance de correlativo_actual en CAI
-- Fecha: 2026-05-14
--
-- Problema:
--   Despues de emitir una factura V3 con sp_lectura_v3, los contadores
--   adm_cai_facturacion.correlativo_actual y adm_cai_bloque_reservado.correlativo_actual
--   NUNCA avanzaban. Esto rompe:
--     1. La validacion futura en sp_adm_obtener_o_reservar_bloque_cai_ruta
--        (`b.correlativo_actual < b.correlativo_hasta`) que decide si reservar
--        un bloque nuevo o reutilizar uno activo.
--     2. El portal en /cai-vencimiento muestra "0 emitidas" para CAIs en uso.
--     3. Borrar un CAI parece seguro aunque tenga facturas (no hay forma de
--        deducirlo solo de las tablas adm_cai_*).
--
-- Fix:
--   Inyectar 2 UPDATE en sp_adm_confirmar_correlativo_cai_sync, justo despues
--   de marcar el correlativo como CONFIRMADO. Usar GREATEST() para que la
--   confirmacion fuera-de-orden (otro dispositivo subio antes con correlativo
--   posterior) no retroceda el contador.
-- =============================================================================

DROP FUNCTION IF EXISTS public.sp_adm_confirmar_correlativo_cai_sync(bigint, bigint, bigint, bigint, varchar, varchar, bigint, varchar);

CREATE OR REPLACE FUNCTION public.sp_adm_confirmar_correlativo_cai_sync(
    p_company_id bigint,
    p_cliente_id bigint,
    p_id_cai bigint,
    p_correlativo bigint,
    p_numero_factura varchar,
    p_lectura_uuid varchar DEFAULT NULL,
    p_factura_id bigint DEFAULT NULL,
    p_usuario varchar DEFAULT current_user
)
RETURNS TABLE (
    success boolean,
    estado_codigo varchar,
    cai_bloque_id bigint,
    factura_id bigint,
    mensaje text
)
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
        UPDATE public.adm_cai_bloque_reservado
        SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo),
            updated_at = now(),
            updated_by = v_usuario_eff
        WHERE company_id = p_company_id
          AND cai_bloque_id = v_bloque.cai_bloque_id
          AND correlativo_actual < p_correlativo;

        UPDATE public.adm_cai_facturacion
        SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo),
            updated_at = now(),
            updated_by = v_usuario_eff
        WHERE company_id = p_company_id
          AND cai_id = p_id_cai
          AND correlativo_actual < p_correlativo;

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
    UPDATE public.adm_cai_bloque_reservado
    SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE company_id = p_company_id
      AND cai_bloque_id = v_bloque.cai_bloque_id
      AND correlativo_actual < p_correlativo;

    UPDATE public.adm_cai_facturacion
    SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE company_id = p_company_id
      AND cai_id = p_id_cai
      AND correlativo_actual < p_correlativo;

    success := true;
    estado_codigo := 'CONFIRMADO';
    cai_bloque_id := v_bloque.cai_bloque_id;
    factura_id := p_factura_id;
    mensaje := 'Correlativo confirmado correctamente.';
    RETURN NEXT;
END;
$function$;

-- =============================================================================
-- Backfill de correlativo_actual usando facturas ya emitidas con CAI
-- =============================================================================

-- Sincroniza adm_cai_bloque_reservado.correlativo_actual con el max correlativo
-- de adm_cai_correlativo_emitido para cada bloque.
UPDATE public.adm_cai_bloque_reservado b
SET correlativo_actual = GREATEST(b.correlativo_actual, sub.max_correlativo),
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        e.company_id,
        e.cai_bloque_id,
        MAX(e.correlativo) AS max_correlativo
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.estado_codigo IN ('CONFIRMADO', 'PENDING_SYNC')
      AND e.factura_id IS NOT NULL
    GROUP BY e.company_id, e.cai_bloque_id
) sub
WHERE b.company_id = sub.company_id
  AND b.cai_bloque_id = sub.cai_bloque_id
  AND b.correlativo_actual < sub.max_correlativo;

-- Sincroniza adm_cai_facturacion.correlativo_actual con el max de sus bloques.
UPDATE public.adm_cai_facturacion c
SET correlativo_actual = GREATEST(c.correlativo_actual, sub.max_correlativo),
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        b.company_id,
        b.cai_id,
        MAX(b.correlativo_actual) AS max_correlativo
    FROM public.adm_cai_bloque_reservado b
    WHERE b.status_id = 1
    GROUP BY b.company_id, b.cai_id
) sub
WHERE c.company_id = sub.company_id
  AND c.cai_id = sub.cai_id
  AND c.correlativo_actual < sub.max_correlativo;
