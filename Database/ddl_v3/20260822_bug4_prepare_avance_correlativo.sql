-- =============================================================================
-- BUGFIX #4 — el prepare no cuenta lo preparado (correlativo repartido dos veces)
-- Fecha: 2026-08-22
--
-- Problema:
--   sp_adm_prepare_correlativo_cai_sync INSERTA la fila en
--   adm_cai_correlativo_emitido con estado PENDING_SYNC pero NO toca
--   correlativo_actual. El BUGFIX #3 (20260514) agrego ese avance solo en
--   sp_adm_confirmar_correlativo_cai_sync.
--
--   Consecuencia: toda subida que muera entre el prepare y el confirm deja un
--   correlativo ocupado que el contador no refleja. Como
--   sp_adm_obtener_o_reservar_bloque_cai_ruta deriva correlativo_siguiente de
--   b.correlativo_actual + 1, el proximo snapshot reparte ESE MISMO correlativo
--   a otro abonado/dispositivo, y el choque es permanente: el prepare siguiente
--   encuentra (cai_id, correlativo) tomado y responde CORRELATIVO_DUPLICADO.
--
--   Caso de campo (piloto app_lectores, ruta 00L1, cai_id 7):
--     bloque 18, rango 251-500, correlativo_actual = 254
--     correlativo 255 -> reservado el 18-jul por 090806378 (PENDING_SYNC, sin factura)
--     correlativo 256 -> reservado el 18-jul por 090806218 (PENDING_SYNC, sin factura)
--     agosto: la ruta vuelve a repartir 255 y 256 -> dos lecturas trabadas.
--   Es el mismo bug del #3, en la otra mitad del flujo.
--
-- Fix (cuatro partes):
--   1. Los indices unicos de adm_cai_correlativo_emitido pasan a ser PARCIALES
--      (WHERE status_id = 1). Sin esto, anular una reserva no libera el
--      correlativo y hay que borrar la fila para reemitir.
--   2. El prepare avanza correlativo_actual (bloque y CAI) con GREATEST, igual
--      que ya hace el confirm, y busca el correlativo existente filtrando
--      status_id = 1 para que anular una reserva sea suficiente.
--   3. sp_adm_obtener_o_reservar_bloque_cai_ruta deriva el siguiente correlativo
--      de max(correlativo) REALMENTE emitido en el bloque, no del contador:
--      deriva de la verdad en vez de confiar en un valor que puede quedar atras.
--      Tambien devuelve ese maximo como correlativo_actual, porque el snapshot
--      offline siembra con el el contador local del dispositivo.
--   4. Backfill de los bloques y CAI que hoy estan desfasados.
--
-- El confirm se recrea sin cambios de logica, solo para alinear el filtro
-- status_id = 1 en la busqueda del correlativo existente (si no, una reserva
-- anulada seguiria bloqueando la confirmacion).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Paso 0: helper de avance de contadores (evita repetir los dos UPDATE)
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.sp_adm_avanzar_correlativo_actual_cai(
    p_company_id bigint,
    p_cai_bloque_id bigint,
    p_id_cai bigint,
    p_correlativo bigint,
    p_usuario varchar DEFAULT current_user
)
RETURNS void
LANGUAGE plpgsql
AS $function$
DECLARE
    v_usuario_eff varchar := COALESCE(NULLIF(BTRIM(p_usuario), ''), current_user);
BEGIN
    -- GREATEST evita retroceso ante confirmaciones/reservas fuera de orden.
    UPDATE public.adm_cai_bloque_reservado AS b
    SET correlativo_actual = GREATEST(b.correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE b.company_id = p_company_id
      AND b.cai_bloque_id = p_cai_bloque_id
      AND b.correlativo_actual < p_correlativo;

    UPDATE public.adm_cai_facturacion AS c
    SET correlativo_actual = GREATEST(c.correlativo_actual, p_correlativo),
        updated_at = now(),
        updated_by = v_usuario_eff
    WHERE c.company_id = p_company_id
      AND c.cai_id = p_id_cai
      AND c.correlativo_actual < p_correlativo;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_avanzar_correlativo_actual_cai(bigint, bigint, bigint, bigint, varchar) IS
'BUGFIX #4 (2026-08-22): avanza correlativo_actual de bloque y CAI hasta el
correlativo dado, sin retroceder. Lo usan el prepare y el confirm.';

-- -----------------------------------------------------------------------------
-- Paso 1: indices unicos parciales (una reserva anulada libera el correlativo)
-- -----------------------------------------------------------------------------

DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_cai_corr;
CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_cai_correlativo_emitido_company_cai_corr
    ON public.adm_cai_correlativo_emitido (company_id, cai_id, correlativo)
    WHERE status_id = 1;

DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_numero;
CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_cai_correlativo_emitido_company_numero
    ON public.adm_cai_correlativo_emitido (company_id, numero_factura)
    WHERE status_id = 1;

DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_lectura;
CREATE UNIQUE INDEX IF NOT EXISTS uq_adm_cai_correlativo_emitido_company_lectura
    ON public.adm_cai_correlativo_emitido (company_id, lectura_uuid)
    WHERE lectura_uuid IS NOT NULL AND status_id = 1;

-- -----------------------------------------------------------------------------
-- Paso 2: el prepare avanza los contadores
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.sp_adm_prepare_correlativo_cai_sync(
    p_company_id bigint,
    p_cliente_id bigint,
    p_id_cai bigint,
    p_correlativo bigint,
    p_numero_factura varchar,
    p_lectura_uuid varchar DEFAULT NULL,
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
        b.cai_bloque_id,
        b.fecha_expiracion
    INTO v_bloque
    FROM public.adm_cai_bloque_reservado b
    JOIN public.adm_cai_facturacion c
      ON c.company_id = b.company_id
     AND c.cai_id = b.cai_id
    WHERE b.company_id = p_company_id
      AND b.cai_id = p_id_cai
      AND b.status_id = 1
      AND c.status_id = 1
      AND current_date >= c.vigencia_desde
      AND (c.vigencia_hasta IS NULL OR current_date <= c.vigencia_hasta)
      AND p_correlativo BETWEEN b.correlativo_desde AND b.correlativo_hasta
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'BLOQUE_INVALIDO: el correlativo % no pertenece a un bloque CAI reservado/activo.', p_correlativo;
    END IF;

    IF v_bloque.fecha_expiracion IS NOT NULL AND current_date > v_bloque.fecha_expiracion THEN
        RAISE EXCEPTION 'CAI_VENCIDO: el bloque reservado para el correlativo % ya expiro.', p_correlativo;
    END IF;

    SELECT *
    INTO v_existente
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.company_id = p_company_id
      AND e.status_id = 1  -- BUGFIX #4: una reserva anulada no bloquea
      AND (
            (v_uuid IS NOT NULL AND e.lectura_uuid = v_uuid)
            OR e.numero_factura = v_numero
            OR (e.cai_id = p_id_cai AND e.correlativo = p_correlativo)
          )
    ORDER BY e.cai_correlativo_emitido_id DESC
    LIMIT 1;

    IF FOUND THEN
        IF COALESCE(v_existente.cai_id, 0) = p_id_cai
           AND COALESCE(v_existente.correlativo, 0) = p_correlativo
           AND v_existente.numero_factura = v_numero THEN

            -- BUGFIX #4: sanea el contador de reservas que nacieron antes de
            -- este fix (idempotente y barato).
            PERFORM public.sp_adm_avanzar_correlativo_actual_cai(
                p_company_id, v_bloque.cai_bloque_id, p_id_cai, p_correlativo, v_usuario_eff);

            success := true;
            estado_codigo := CASE
                WHEN v_existente.factura_id IS NOT NULL THEN 'IDEMPOTENTE'
                ELSE COALESCE(v_existente.estado_codigo, 'PENDING_SYNC')
            END;
            cai_bloque_id := v_existente.cai_bloque_id;
            factura_id := v_existente.factura_id;
            mensaje := CASE
                WHEN v_existente.factura_id IS NOT NULL THEN 'El correlativo ya fue confirmado para esta misma lectura.'
                ELSE 'El correlativo ya estaba preparado para sincronizacion.'
            END;
            RETURN NEXT;
            RETURN;
        END IF;

        RAISE EXCEPTION 'CORRELATIVO_DUPLICADO: el correlativo/numero ya fue utilizado por otra lectura.';
    END IF;

    INSERT INTO public.adm_cai_correlativo_emitido (
        company_id,
        cai_bloque_id,
        cai_id,
        correlativo,
        numero_factura,
        cliente_id,
        lectura_uuid,
        estado_codigo,
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
        'PENDING_SYNC',
        v_usuario_eff
    );

    -- BUGFIX #4: reservar TAMBIEN consume el correlativo. Sin esto, el proximo
    -- snapshot de la ruta lo reparte de nuevo.
    PERFORM public.sp_adm_avanzar_correlativo_actual_cai(
        p_company_id, v_bloque.cai_bloque_id, p_id_cai, p_correlativo, v_usuario_eff);

    success := true;
    estado_codigo := 'PENDING_SYNC';
    cai_bloque_id := v_bloque.cai_bloque_id;
    factura_id := NULL;
    mensaje := 'Correlativo preparado para sincronizacion.';
    RETURN NEXT;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_prepare_correlativo_cai_sync(bigint, bigint, bigint, bigint, varchar, varchar, varchar) IS
'Reserva (prepare) un correlativo CAI para una lectura offline.
BUGFIX #4 (2026-08-22): avanza correlativo_actual de bloque y CAI, porque
reservar tambien consume el numero; y busca el correlativo existente filtrando
status_id = 1, para que anular una reserva libere el numero sin borrar filas.';

-- -----------------------------------------------------------------------------
-- Paso 3: el confirm ignora reservas anuladas (misma logica del 20260520)
-- -----------------------------------------------------------------------------

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
      AND e.status_id = 1  -- BUGFIX #4: una reserva anulada no bloquea
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

        -- BUGFIX #3 (2026-05-14) + #4 (2026-08-22): el avance vive en el helper.
        PERFORM public.sp_adm_avanzar_correlativo_actual_cai(
            p_company_id, v_bloque.cai_bloque_id, p_id_cai, p_correlativo, v_usuario_eff);

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

    PERFORM public.sp_adm_avanzar_correlativo_actual_cai(
        p_company_id, v_bloque.cai_bloque_id, p_id_cai, p_correlativo, v_usuario_eff);

    success := true;
    estado_codigo := 'CONFIRMADO';
    cai_bloque_id := v_bloque.cai_bloque_id;
    factura_id := p_factura_id;
    mensaje := 'Correlativo confirmado correctamente.';
    RETURN NEXT;
END;
$function$;

-- -----------------------------------------------------------------------------
-- Paso 4: el bloque se reparte desde lo REALMENTE emitido, no desde el contador
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.sp_adm_obtener_o_reservar_bloque_cai_ruta(
    p_company_id bigint,
    p_ruta_codigo varchar,
    p_cantidad integer DEFAULT 250,
    p_usuario varchar DEFAULT current_user,
    p_tipo_documento_fiscal_id smallint DEFAULT 1  -- 1 = Factura
)
RETURNS TABLE (
    cai_bloque_id bigint,
    cai_id bigint,
    codigo_cai varchar,
    prefijo_documento varchar,
    correlativo_desde bigint,
    correlativo_hasta bigint,
    correlativo_actual bigint,
    correlativo_siguiente bigint,
    fecha_expiracion date,
    estado_codigo varchar
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_ruta_codigo varchar := NULLIF(BTRIM(COALESCE(p_ruta_codigo, '')), '');
    v_cai_id bigint;
BEGIN
    IF v_ruta_codigo IS NULL THEN
        RAISE EXCEPTION 'RUTA_REQUERIDA: se requiere ruta para resolver bloque CAI offline.';
    END IF;

    IF p_tipo_documento_fiscal_id IS NULL OR p_tipo_documento_fiscal_id <= 0 THEN
        RAISE EXCEPTION 'TIPO_DOCUMENTO_REQUERIDO: tipo_documento_fiscal_id no valido.';
    END IF;

    -- Refresca estado del CAI antes de seleccionar (idempotente, barato).
    PERFORM public.sp_adm_actualizar_estado_cai(p_company_id);

    -- Branch 1: bloque ya reservado para esta ruta + CAI vigente del tipo correcto.
    -- BUGFIX #4: `usado` = el mayor entre el contador y el maximo correlativo
    -- realmente emitido en el bloque. El contador puede quedar atras (reservas
    -- previas al fix); lo emitido es la verdad. Se excluye SYNC_CONFLICT porque
    -- no es una emision valida, y status_id = 0 porque es una reserva anulada.
    RETURN QUERY
    SELECT
        b.cai_bloque_id,
        b.cai_id,
        c.codigo_cai,
        c.prefijo_documento,
        b.correlativo_desde,
        b.correlativo_hasta,
        GREATEST(b.correlativo_actual, COALESCE(em.max_emitido, 0)) AS correlativo_actual,
        LEAST(GREATEST(b.correlativo_actual, COALESCE(em.max_emitido, 0)) + 1, b.correlativo_hasta) AS correlativo_siguiente,
        b.fecha_expiracion,
        b.estado_codigo
    FROM public.adm_cai_bloque_reservado b
    JOIN public.adm_cai_facturacion c
      ON c.company_id = b.company_id
     AND c.cai_id = b.cai_id
    LEFT JOIN LATERAL (
        SELECT MAX(e.correlativo) AS max_emitido
        FROM public.adm_cai_correlativo_emitido e
        WHERE e.company_id = b.company_id
          AND e.cai_bloque_id = b.cai_bloque_id
          AND e.status_id = 1
          AND e.estado_codigo <> 'SYNC_CONFLICT'
    ) em ON true
    WHERE b.company_id = p_company_id
      AND b.ruta_codigo = v_ruta_codigo
      AND b.status_id = 1
      AND c.status_id = 1
      AND c.tipo_documento_fiscal_id = p_tipo_documento_fiscal_id
      AND c.estado_id = 1  -- VIGENTE
      AND current_date >= c.vigencia_desde
      AND (c.vigencia_hasta IS NULL OR current_date <= c.vigencia_hasta)
      AND c.fecha_limite_emision >= current_date
      AND c.correlativo_actual < c.rango_hasta
      AND (b.fecha_expiracion IS NULL OR current_date <= b.fecha_expiracion)
      AND GREATEST(b.correlativo_actual, COALESCE(em.max_emitido, 0)) < b.correlativo_hasta
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;

    IF FOUND THEN
        RETURN;
    END IF;

    -- Branch 2: no hay bloque vigente — busca CAI vigente del tipo correcto.
    SELECT c.cai_id
    INTO v_cai_id
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id
      AND c.status_id = 1
      AND c.tipo_documento_fiscal_id = p_tipo_documento_fiscal_id
      AND c.estado_id = 1  -- VIGENTE
      AND current_date >= c.vigencia_desde
      AND (c.vigencia_hasta IS NULL OR current_date <= c.vigencia_hasta)
      AND c.fecha_limite_emision >= current_date
      AND c.correlativo_actual < c.rango_hasta
    ORDER BY c.vigencia_desde DESC, c.cai_id DESC
    LIMIT 1;

    IF v_cai_id IS NULL THEN
        RAISE EXCEPTION 'CAI_VIGENTE_NO_DISPONIBLE: no existe CAI vigente del tipo % para la empresa %. Revise vigencia, fecha limite y agotamiento de rango.',
            p_tipo_documento_fiscal_id, p_company_id;
    END IF;

    PERFORM 1
    FROM public.sp_adm_reservar_bloque_cai(
        p_company_id,
        v_cai_id,
        NULL,
        NULL,
        v_ruta_codigo,
        COALESCE(NULLIF(p_cantidad, 0), 250),
        NULL,
        p_usuario
    );

    RETURN QUERY
    SELECT
        b.cai_bloque_id,
        b.cai_id,
        c.codigo_cai,
        c.prefijo_documento,
        b.correlativo_desde,
        b.correlativo_hasta,
        GREATEST(b.correlativo_actual, COALESCE(em.max_emitido, 0)) AS correlativo_actual,
        LEAST(GREATEST(b.correlativo_actual, COALESCE(em.max_emitido, 0)) + 1, b.correlativo_hasta) AS correlativo_siguiente,
        b.fecha_expiracion,
        b.estado_codigo
    FROM public.adm_cai_bloque_reservado b
    JOIN public.adm_cai_facturacion c
      ON c.company_id = b.company_id
     AND c.cai_id = b.cai_id
    LEFT JOIN LATERAL (
        SELECT MAX(e.correlativo) AS max_emitido
        FROM public.adm_cai_correlativo_emitido e
        WHERE e.company_id = b.company_id
          AND e.cai_bloque_id = b.cai_bloque_id
          AND e.status_id = 1
          AND e.estado_codigo <> 'SYNC_CONFLICT'
    ) em ON true
    WHERE b.company_id = p_company_id
      AND b.ruta_codigo = v_ruta_codigo
      AND b.status_id = 1
      AND c.cai_id = v_cai_id
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_obtener_o_reservar_bloque_cai_ruta(bigint, varchar, integer, varchar, smallint) IS
'Resuelve o reserva bloque CAI para una ruta. Filtros V3 (2026-05-14):
  - tipo_documento_fiscal_id (default 1=FAC)
  - estado_id = 1 VIGENTE (cfg_estado_cai)
  - fecha_limite_emision >= current_date
  - correlativo_actual < rango_hasta (no agotado)
Llama sp_adm_actualizar_estado_cai antes de seleccionar para refrescar VENCIDO/AGOTADO automaticamente.
BUGFIX #4 (2026-08-22): correlativo_actual y correlativo_siguiente se derivan de
GREATEST(contador, max correlativo emitido en el bloque), asi un contador
desfasado no vuelve a repartir un correlativo ya tomado.';

-- -----------------------------------------------------------------------------
-- Paso 5: backfill de los contadores desfasados
-- -----------------------------------------------------------------------------
-- A diferencia del backfill del BUGFIX #3, aqui SI cuentan las reservas
-- PENDING_SYNC sin factura: ocupan el correlativo aunque nunca se hayan
-- confirmado. Se excluye SYNC_CONFLICT (no es emision valida) y status_id = 0
-- (reserva anulada). Solo sube, nunca retrocede.

UPDATE public.adm_cai_bloque_reservado b
SET correlativo_actual = sub.max_correlativo,
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        e.company_id,
        e.cai_bloque_id,
        MAX(e.correlativo) AS max_correlativo
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.status_id = 1
      AND e.estado_codigo <> 'SYNC_CONFLICT'
    GROUP BY e.company_id, e.cai_bloque_id
) sub
WHERE b.company_id = sub.company_id
  AND b.cai_bloque_id = sub.cai_bloque_id
  AND b.correlativo_actual < sub.max_correlativo;

UPDATE public.adm_cai_facturacion c
SET correlativo_actual = sub.max_correlativo,
    updated_at = now(),
    updated_by = current_user
FROM (
    SELECT
        e.company_id,
        e.cai_id,
        MAX(e.correlativo) AS max_correlativo
    FROM public.adm_cai_correlativo_emitido e
    WHERE e.status_id = 1
      AND e.estado_codigo <> 'SYNC_CONFLICT'
    GROUP BY e.company_id, e.cai_id
) sub
WHERE c.company_id = sub.company_id
  AND c.cai_id = sub.cai_id
  AND c.correlativo_actual < sub.max_correlativo;

-- -----------------------------------------------------------------------------
-- Validacion: no debe quedar ningun bloque por debajo de lo emitido
-- -----------------------------------------------------------------------------

DO $$
DECLARE
    v_msg text;
BEGIN
    SELECT string_agg(
        format('bloque %s (ruta %s, cai %s): correlativo_actual=%s, max_emitido=%s',
            b.cai_bloque_id, b.ruta_codigo, b.cai_id, b.correlativo_actual, sub.max_correlativo),
        E'\n'
    )
    INTO v_msg
    FROM public.adm_cai_bloque_reservado b
    JOIN (
        SELECT e.company_id, e.cai_bloque_id, MAX(e.correlativo) AS max_correlativo
        FROM public.adm_cai_correlativo_emitido e
        WHERE e.status_id = 1
          AND e.estado_codigo <> 'SYNC_CONFLICT'
        GROUP BY e.company_id, e.cai_bloque_id
    ) sub
      ON sub.company_id = b.company_id
     AND sub.cai_bloque_id = b.cai_bloque_id
    WHERE b.correlativo_actual < sub.max_correlativo;

    IF v_msg IS NULL THEN
        RAISE NOTICE 'BUGFIX #4: todos los bloques quedaron al dia con lo emitido.';
    ELSE
        RAISE EXCEPTION E'BUGFIX #4: quedaron bloques desfasados:\n%', v_msg;
    END IF;
END $$;
