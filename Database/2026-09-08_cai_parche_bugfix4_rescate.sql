-- =============================================================================
-- RESCATE 2026-09-08 — Parche del ciclo CAI que solo vivia en produccion
--   (BUGFIX #3 del 2026-05-14, BUGFIX #4 del 2026-08-22 y el guard de cierre
--    de ciclo del 2026-08-23)
--
-- POR QUE EXISTE ESTE ARCHIVO
--   El 2026-09-08, al comparar el mirror `siad_v3_restore` contra la base
--   activa de produccion `siad_v4`, aparecieron cinco objetos del ciclo CAI
--   que SOLO existen arriba y que NINGUN script de Database/ reconstruye.
--   Se aplicaron directo al servidor y nunca bajaron al repositorio.
--
--   Consecuencia: un restore del mirror sobre produccion los borraria, y el
--   repositorio no podria volver a crearlos. Este script cierra ese hueco.
--
--   El contenido NO se escribio a mano: se extrajo de `siad_v4` con
--   pg_get_functiondef / pg_get_indexdef, en una sesion abierta con
--   default_transaction_read_only=on.
--
-- DONDE SE APLICA
--   * `siad_v4` (produccion, 172.16.0.9) — YA LO TIENE. El script es
--     idempotente y reejecutarlo arriba no cambia nada, pero no hace falta.
--   * `siad_v3_restore` (mirror local) — LE FALTA. Aqui es donde importa.
--   * `siad_v3_desarrollo` (3.208.232.209) — le falta.
--
-- QUE ARREGLA CADA PIEZA
--
--   1. sp_adm_avanzar_correlativo_actual_cai  (FUNCION NUEVA)
--      Helper unico que adelanta `correlativo_actual` en
--      adm_cai_bloque_reservado y en adm_cai_facturacion, con GREATEST para
--      que nunca retroceda ante confirmaciones fuera de orden. Antes esa
--      logica estaba copiada dentro de confirmar_correlativo_cai_sync.
--
--   2. sp_adm_obtener_o_reservar_bloque_cai_ruta
--      BUGFIX #4: el correlativo disponible ya no sale del contador del
--      bloque, sino del MAYOR entre el contador y el maximo correlativo
--      realmente emitido (excluyendo SYNC_CONFLICT y las reservas anuladas
--      con status_id = 0). El contador podia quedar atras en las reservas
--      creadas antes del fix, y el snapshot de la ruta repartia dos veces el
--      mismo folio.
--
--   3. sp_adm_prepare_correlativo_cai_sync
--      BUGFIX #4, dos cambios:
--        a. La busqueda de duplicado filtra por status_id = 1, para que una
--           reserva ANULADA deje de bloquear su numero para siempre.
--        b. Reservar TAMBIEN consume el correlativo (llama al helper). Sin
--           esto el proximo snapshot de la ruta lo volvia a repartir.
--
--   4. sp_adm_confirmar_correlativo_cai_sync
--      BUGFIX #4: mismo filtro status_id = 1 en la busqueda de duplicado, y
--      el avance del contador pasa a delegarse en el helper (antes eran dos
--      UPDATE copiados dentro de esta funcion).
--
--   5. sp_adm_periodo_ciclo_cerrar
--      Guard nuevo del 2026-08-23: el cierre de un ciclo se rechaza con
--      CICLO_FOLIOS_SIN_CONFIRMAR si quedan folios CAI reservados en estado
--      PENDING_SYNC y sin factura. Son lecturas ya emitidas que siguen en un
--      telefono; si se cierra el ciclo no pueden subir. La comparacion de
--      codigo de ciclo replica la de fn_adm_periodo_comercial_ciclo_abierto:
--      se normaliza a dos digitos solo cuando el codigo es numerico.
--      El guard NO corre cuando se llama con p_forzar = true.
--
--   6. Los tres indices unicos de adm_cai_correlativo_emitido pasan a ser
--      PARCIALES (WHERE status_id = 1). Con los indices totales, una reserva
--      anulada seguia ocupando su (company_id, cai_id, correlativo), su
--      numero_factura y su lectura_uuid, y el folio quedaba quemado. Es la
--      contraparte en el indice del filtro status_id = 1 de los puntos 3 y 4.
--
-- ORDEN DE DEPENDENCIAS
--   El helper del punto 1 va primero porque lo invocan los puntos 3 y 4.
--   plpgsql resuelve las llamadas en ejecucion, asi que el orden no es
--   obligatorio, pero se respeta por claridad.
--
-- DEPENDENCIAS VERIFICADAS EN EL MIRROR (2026-09-08)
--   Existen: sp_adm_actualizar_estado_cai(bigint),
--   sp_adm_reservar_bloque_cai(8 args), fn_adm_periodo_ciclo_rutas_pendientes,
--   y todas las columnas que el parche usa (adm_cai_correlativo_emitido.
--   status_id / estado_codigo / cai_bloque_id, adm_cai_bloque_reservado.
--   ruta_codigo / fecha_reserva, adm_cai_facturacion.tipo_documento_fiscal_id,
--   adm_periodo_comercial_ciclo.ciclo_codigo, ciclos.ciclos_codigo).
--
-- IMPACTO DE DATOS
--   Ninguno. No inserta, no borra y no actualiza ninguna fila. Solo reemplaza
--   cuerpos de funcion y redefine tres indices.
--   En el mirror la tabla tiene 228 filas y NINGUNA con status_id <> 1, asi
--   que los indices parciales se crean sin posibilidad de colision.
--
-- IDEMPOTENTE
--   Las funciones son CREATE OR REPLACE. Los indices se tocan dentro de un
--   bloque DO que primero comprueba si ya son parciales: si lo son, no hace
--   nada. Reejecutar el script completo es seguro.
--
-- REVERSA
--   Para volver atras, los indices se recrean sin el WHERE (ver el bloque
--   comentado al final) y las funciones se restauran desde el respaldo previo.
--   Sacar respaldo antes con:
--     pg_dump -s -t adm_cai_correlativo_emitido <bd> > antes.sql
-- =============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

-- -----------------------------------------------------------------------------
-- 1 a 5. Las cinco funciones, tal como estan hoy en siad_v4.
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.sp_adm_avanzar_correlativo_actual_cai(p_company_id bigint, p_cai_bloque_id bigint, p_id_cai bigint, p_correlativo bigint, p_usuario character varying DEFAULT CURRENT_USER)
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

$function$
;

CREATE OR REPLACE FUNCTION public.sp_adm_obtener_o_reservar_bloque_cai_ruta(p_company_id bigint, p_ruta_codigo character varying, p_cantidad integer DEFAULT 250, p_usuario character varying DEFAULT CURRENT_USER, p_tipo_documento_fiscal_id smallint DEFAULT 1)
 RETURNS TABLE(cai_bloque_id bigint, cai_id bigint, codigo_cai character varying, prefijo_documento character varying, correlativo_desde bigint, correlativo_hasta bigint, correlativo_actual bigint, correlativo_siguiente bigint, fecha_expiracion date, estado_codigo character varying)
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

$function$
;

CREATE OR REPLACE FUNCTION public.sp_adm_prepare_correlativo_cai_sync(p_company_id bigint, p_cliente_id bigint, p_id_cai bigint, p_correlativo bigint, p_numero_factura character varying, p_lectura_uuid character varying DEFAULT NULL::character varying, p_usuario character varying DEFAULT CURRENT_USER)
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

$function$
;

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

$function$
;

CREATE OR REPLACE FUNCTION public.sp_adm_periodo_ciclo_cerrar(p_company_id bigint, p_periodo_ciclo_id bigint, p_usuario text, p_forzar boolean DEFAULT false)
 RETURNS void
 LANGUAGE plpgsql
AS $function$

DECLARE

    v_ciclo record;

    v_pendientes bigint;

    v_folios bigint;

BEGIN

    SELECT pc.*, p.anio, p.mes

    INTO v_ciclo

    FROM public.adm_periodo_comercial_ciclo pc

    JOIN public.adm_periodo_comercial p

      ON p.company_id = pc.company_id

     AND p.periodo_comercial_id = pc.periodo_comercial_id

    WHERE pc.company_id = p_company_id

      AND pc.periodo_ciclo_id = p_periodo_ciclo_id

    FOR UPDATE OF pc;



    IF NOT FOUND THEN

        RAISE EXCEPTION 'No existe el ciclo de período comercial % para company_id=%.',

            p_periodo_ciclo_id, p_company_id;

    END IF;



    IF v_ciclo.status_id <> 1 THEN

        RAISE EXCEPTION 'CICLO_YA_CERRADO: el ciclo % del período %-% ya está cerrado.',

            v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');

    END IF;



    IF NOT p_forzar THEN

        SELECT count(*)

        INTO v_pendientes

        FROM public.fn_adm_periodo_ciclo_rutas_pendientes(p_company_id, p_periodo_ciclo_id) rp

        WHERE rp.pendiente;



        IF v_pendientes > 0 THEN

            RAISE EXCEPTION 'CICLO_RUTAS_PENDIENTES: % ruta(s) del ciclo % sin facturas emitidas en %-%.',

                v_pendientes, v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');

        END IF;



        -- 2026-08-23: folios entregados que todavía no subieron. La comparación

        -- de ciclo replica la de fn_adm_periodo_comercial_ciclo_abierto: se

        -- normaliza a dos dígitos sólo cuando el código es numérico.

        SELECT count(*)

        INTO v_folios

        FROM public.adm_cai_correlativo_emitido e

        JOIN public.cliente_maestro cm

          ON cm.company_id = e.company_id

         AND cm.maestro_cliente_id = e.cliente_id

        LEFT JOIN public.ciclos c

          ON c.ciclos_id = cm.ciclos_id

        WHERE e.company_id = p_company_id

          AND e.status_id = 1

          AND e.estado_codigo = 'PENDING_SYNC'

          AND e.factura_id IS NULL

          AND (

                CASE

                    WHEN btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),

                                        lpad(cm.ciclos_id::text, 2, '0'))) ~ '^[0-9]+$'

                    THEN lpad(btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),

                                             lpad(cm.ciclos_id::text, 2, '0'))), 2, '0')

                    ELSE btrim(coalesce(nullif(btrim(c.ciclos_codigo), ''),

                                        lpad(cm.ciclos_id::text, 2, '0')))

                END

              ) = (

                CASE

                    WHEN btrim(v_ciclo.ciclo_codigo) ~ '^[0-9]+$'

                    THEN lpad(btrim(v_ciclo.ciclo_codigo), 2, '0')

                    ELSE btrim(v_ciclo.ciclo_codigo)

                END

              );



        IF v_folios > 0 THEN

            RAISE EXCEPTION 'CICLO_FOLIOS_SIN_CONFIRMAR: % folio(s) CAI del ciclo % reservados y sin confirmar. Son lecturas ya emitidas que siguen en un teléfono: si se cierra %-%, no van a poder subir.',

                v_folios, v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');

        END IF;

    END IF;



    UPDATE public.adm_periodo_comercial_ciclo pc

    SET status_id = 2,

        fecha_cierre = now(),

        cerrado_por = left(p_usuario, 100),

        updated_at = now(),

        updated_by = left(p_usuario, 100)

    WHERE pc.company_id = p_company_id

      AND pc.periodo_ciclo_id = p_periodo_ciclo_id;

END;

$function$
;

-- -----------------------------------------------------------------------------
-- 6. Los tres indices unicos pasan a parciales (WHERE status_id = 1).
--    El bloque revisa antes si ya lo son, para no tocar nada en una base que
--    ya tiene el parche (produccion).
-- -----------------------------------------------------------------------------

DO $rescate_cai$
DECLARE
    r record;
    v_def text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('uq_adm_cai_correlativo_emitido_company_cai_corr',
             'CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_cai_corr'
             ' ON public.adm_cai_correlativo_emitido USING btree'
             ' (company_id, cai_id, correlativo) WHERE (status_id = 1)'),
            ('uq_adm_cai_correlativo_emitido_company_numero',
             'CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_numero'
             ' ON public.adm_cai_correlativo_emitido USING btree'
             ' (company_id, numero_factura) WHERE (status_id = 1)'),
            ('uq_adm_cai_correlativo_emitido_company_lectura',
             'CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_lectura'
             ' ON public.adm_cai_correlativo_emitido USING btree'
             ' (company_id, lectura_uuid)'
             ' WHERE ((lectura_uuid IS NOT NULL) AND (status_id = 1))')
        ) AS t(nombre, definicion)
    LOOP
        SELECT pg_get_indexdef(i.oid) INTO v_def
        FROM pg_class i
        JOIN pg_namespace n ON n.oid = i.relnamespace
        WHERE n.nspname = 'public' AND i.relname = r.nombre AND i.relkind = 'i';

        IF v_def IS NULL THEN
            RAISE NOTICE 'Indice % no existe; se crea.', r.nombre;
            EXECUTE r.definicion;
        ELSIF v_def LIKE '%status_id = 1%' THEN
            RAISE NOTICE 'Indice % ya es parcial; no se toca.', r.nombre;
        ELSE
            RAISE NOTICE 'Indice % es total; se redefine como parcial.', r.nombre;
            EXECUTE format('DROP INDEX public.%I', r.nombre);
            EXECUTE r.definicion;
        END IF;
    END LOOP;
END
$rescate_cai$;

COMMIT;

-- =============================================================================
-- VERIFICACION (ejecutar aparte, despues del COMMIT)
-- =============================================================================
--
-- Las cinco funciones deben aparecer, y el helper debe existir:
--   SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS args
--     FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--    WHERE n.nspname = 'public'
--      AND p.proname IN ('sp_adm_avanzar_correlativo_actual_cai',
--                        'sp_adm_obtener_o_reservar_bloque_cai_ruta',
--                        'sp_adm_prepare_correlativo_cai_sync',
--                        'sp_adm_confirmar_correlativo_cai_sync',
--                        'sp_adm_periodo_ciclo_cerrar')
--    ORDER BY 1;
--   -- esperado: 5 filas
--
-- Los tres indices deben salir con su WHERE:
--   SELECT indexname, indexdef
--     FROM pg_indexes
--    WHERE schemaname = 'public'
--      AND tablename  = 'adm_cai_correlativo_emitido'
--      AND indexname LIKE 'uq_%'
--    ORDER BY 1;
--   -- esperado: los 3 con "WHERE (status_id = 1)"
--
-- Contraste contra produccion (opcional): las huellas deben coincidir con las
-- de siad_v4.
--   SELECT p.proname,
--          md5(regexp_replace(p.prosrc, '\s+', '', 'g')) AS huella
--     FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--    WHERE n.nspname = 'public'
--      AND p.proname LIKE 'sp_adm_%cai%'
--    ORDER BY 1;
--
-- =============================================================================
-- REVERSA (solo si hay que volver atras; NO ejecutar en condiciones normales)
-- =============================================================================
--
--   BEGIN;
--   DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_cai_corr;
--   CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_cai_corr
--       ON public.adm_cai_correlativo_emitido USING btree (company_id, cai_id, correlativo);
--   DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_numero;
--   CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_numero
--       ON public.adm_cai_correlativo_emitido USING btree (company_id, numero_factura);
--   DROP INDEX IF EXISTS public.uq_adm_cai_correlativo_emitido_company_lectura;
--   CREATE UNIQUE INDEX uq_adm_cai_correlativo_emitido_company_lectura
--       ON public.adm_cai_correlativo_emitido USING btree (company_id, lectura_uuid)
--       WHERE (lectura_uuid IS NOT NULL);
--   COMMIT;
--   -- Ojo: la reversa de los indices puede fallar si para entonces ya existen
--   -- filas anuladas (status_id = 0) que colisionen con una vigente.
--   -- Las funciones se restauran desde el respaldo previo, no desde aqui.
