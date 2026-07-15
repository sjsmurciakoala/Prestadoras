-- =============================================================================
-- 2026-07-15  Apertura única e integral de ciclo (Fase B apertura-ciclo-único)
-- Plan: docs/plans/2026-07-14-plan-apertura-ciclo-unico.md
-- Rama: feat/apertura-ciclo-integral (apilada sobre Fase A / PR #22)
-- -----------------------------------------------------------------------------
-- Reemplaza el DOBLE camino de apertura (pantalla Períodos comerciales sin
-- planilla vs "Generar período" del Auxiliar de Lectura sin validaciones) por
-- UN solo SP que hace todo: valida secuencia SIEMPRE, crea período+ciclo,
-- genera la planilla de lectura y devuelve un resumen con avisos. La lógica
-- de planilla se porta de AuxiliarLecturaService.GenerarPeriodoAsync (C#),
-- que la Fase C retira junto con la pantalla.
--
-- Contenido:
--   1. fn_adm_ciclo_norm — normalización de ciclo ('1'→'01') como función
--      IMMUTABLE única para los objetos nuevos (los SP existentes conservan
--      su patrón inline; unificación total queda para el retiro del legacy).
--   2. fn_adm_periodo_ciclo_sugerido — próximo ciclo según calendariopro.
--   3. fn_adm_periodo_ciclo_info / fn_adm_periodo_ciclo_preview — qué pasaría
--      al abrir (sin escribir): estados, planilla, rutas/lectores, avisos.
--   4. sp_adm_periodo_ciclo_abrir — apertura integral (RETURNS jsonb resumen).
--      * Secuencia obligatoria: mes calendario anterior cerrado (sin bypass).
--      * No reabre períodos/ciclos cerrados (PERIODO_CERRADO / CICLO_CERRADO).
--      * fecha_limite desde calendariopro.fechalec (aviso SIN_CALENDARIO si falta).
--      * Planilla: roll-over del mes anterior o alta desde cliente_maestro;
--        idempotente (PLANILLA_EXISTENTE si ya hay filas del período/ciclo).
--      * NUNCA cierra otros ciclos: solo avisa (OTRO_CICLO_ABIERTO).
--   5. sp_adm_periodo_ciclo_deshacer — borra planilla+ciclo (+período si queda
--      vacío) SOLO si no hay lecturas registradas ni facturas del ciclo.
--   6. DROP del SP viejo sp_adm_periodo_comercial_abrir (tenía el bypass
--      p_validar_secuencia; el servicio C# queda repuntado en esta fase).
--
-- El espejo a historialmes es por TRIGGERS sobre adm_periodo_comercial(_ciclo)
-- (F7): estos SP no lo tocan y el WS legacy sigue viendo el espejo.
-- Idempotente. NO ejecutar en producción fuera de la ventana de deploy.
-- =============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- 1. Normalización de ciclo (única para los objetos de esta fase)
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_ciclo_norm(p_ciclo text)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $function$
    SELECT CASE
        WHEN btrim(COALESCE(p_ciclo, '')) ~ '^[0-9]+$'
            THEN lpad(btrim(p_ciclo)::int::text, 2, '0')
        ELSE btrim(COALESCE(p_ciclo, ''))
    END;
$function$;

COMMENT ON FUNCTION public.fn_adm_ciclo_norm(text) IS
'Fase B apertura-ciclo-único: normaliza el código de ciclo (numérico → 2 dígitos, ''1''→''01''; no numérico → btrim). Misma regla que CalendarioFacturacionReglas.NormalizarCiclo (C#).';

-- Paridad EXACTA con ExtraerRuta (AuxiliarLecturaService C#): separa por '-',
-- descarta segmentos vacíos, recorta; con ≥3 partes devuelve la 3ª, si no el
-- indicativo completo recortado; vacío → NULL.
CREATE OR REPLACE FUNCTION public.fn_adm_ruta_de_indicativo(p_indicativo text)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $function$
    SELECT CASE
        WHEN cardinality(s.partes) >= 3 THEN NULLIF(s.partes[3], '')
        ELSE NULLIF(btrim(COALESCE(p_indicativo, '')), '')
    END
    FROM (
        SELECT ARRAY(
            SELECT btrim(u.x)
            FROM unnest(string_to_array(COALESCE(p_indicativo, ''), '-')) AS u(x)
            WHERE btrim(u.x) <> ''
        ) AS partes
    ) s;
$function$;

-- ----------------------------------------------------------------------------
-- 2. Sugerencia de apertura: el ciclo del calendario más cercano a hoy que
--    aún no tiene registro en adm_periodo_comercial_ciclo. Prefiere el
--    próximo (fechalec >= hoy); si no hay futuro, el pendiente más reciente.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_periodo_ciclo_sugerido(
    p_company_id bigint
) RETURNS TABLE(anio integer, mes integer, ciclo varchar, fecha_lectura date)
LANGUAGE sql
STABLE
AS $function$
    SELECT cp.ano,
           cp.mes,
           public.fn_adm_ciclo_norm(cp.ciclo)::varchar,
           cp.fechalec
    FROM public.calendariopro cp
    WHERE cp.company_id = p_company_id
      AND cp.fechalec IS NOT NULL
      AND NOT EXISTS (
            SELECT 1
            FROM public.adm_periodo_comercial_ciclo pc
            JOIN public.adm_periodo_comercial p
              ON p.company_id = pc.company_id
             AND p.periodo_comercial_id = pc.periodo_comercial_id
            WHERE pc.company_id = p_company_id
              AND p.anio = cp.ano
              AND p.mes = cp.mes
              AND pc.ciclo_codigo = public.fn_adm_ciclo_norm(cp.ciclo)
      )
    ORDER BY (cp.fechalec < current_date),          -- primero los que vienen
             abs(cp.fechalec - current_date)
    LIMIT 1;
$function$;

-- ----------------------------------------------------------------------------
-- 3. Información/preview de la apertura (sin escribir nada)
-- ----------------------------------------------------------------------------

-- Núcleo compartido entre el preview y el SP de apertura: estados actuales,
-- calendario, tamaño potencial de la planilla y rutas con su lector.
-- DROP previo: la firma de OUT params puede cambiar entre versiones.
DROP FUNCTION IF EXISTS public.fn_adm_periodo_ciclo_info(bigint, integer, integer, text);
CREATE OR REPLACE FUNCTION public.fn_adm_periodo_ciclo_info(
    p_company_id bigint,
    p_anio integer,
    p_mes integer,
    p_ciclo text
) RETURNS TABLE(
    ciclo_norm varchar,
    periodo_comercial_id bigint,
    periodo_status smallint,
    periodo_ciclo_id bigint,
    ciclo_status smallint,
    anterior_abierto boolean,
    ciclos_ids integer[],
    fecha_limite_calendario date,
    planilla_existente bigint,
    rollover_disponible bigint,
    clientes_activos bigint,
    rutas jsonb,
    rutas_sin_lector bigint,
    otros_ciclos_abiertos jsonb
)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_ciclo text := public.fn_adm_ciclo_norm(p_ciclo);
    v_anio_prev integer;
    v_mes_prev integer;
BEGIN
    IF p_mes = 1 THEN
        v_anio_prev := p_anio - 1; v_mes_prev := 12;
    ELSE
        v_anio_prev := p_anio; v_mes_prev := p_mes - 1;
    END IF;

    ciclo_norm := v_ciclo;

    SELECT p.periodo_comercial_id, p.status_id
    INTO periodo_comercial_id, periodo_status
    FROM public.adm_periodo_comercial p
    WHERE p.company_id = p_company_id AND p.anio = p_anio AND p.mes = p_mes;

    SELECT pc.periodo_ciclo_id, pc.status_id
    INTO periodo_ciclo_id, ciclo_status
    FROM public.adm_periodo_comercial_ciclo pc
    WHERE pc.company_id = p_company_id
      AND pc.periodo_comercial_id = fn_adm_periodo_ciclo_info.periodo_comercial_id
      AND pc.ciclo_codigo = v_ciclo;

    anterior_abierto := EXISTS (
        SELECT 1 FROM public.adm_periodo_comercial p
        WHERE p.company_id = p_company_id
          AND p.anio = v_anio_prev AND p.mes = v_mes_prev
          AND p.status_id = 1
    );

    -- Catálogo de ciclos: TODOS los ids que matcheen (exacto o numérico),
    -- como el C# original (cicloIds.Contains). Si el catálogo tuviera '1' y
    -- '01' como filas distintas, ambos entran. Fallback al número crudo.
    SELECT COALESCE(array_agg(c.ciclos_id), '{}')
    INTO ciclos_ids
    FROM public.ciclos c
    WHERE btrim(c.ciclos_codigo) = v_ciclo
       OR (c.ciclos_codigo ~ '^[0-9]+$' AND v_ciclo ~ '^[0-9]+$'
           AND c.ciclos_codigo::int = v_ciclo::int);

    IF cardinality(ciclos_ids) = 0 AND v_ciclo ~ '^[0-9]+$' THEN
        ciclos_ids := ARRAY[v_ciclo::int];
    END IF;

    SELECT cp.fechalec
    INTO fecha_limite_calendario
    FROM public.calendariopro cp
    WHERE cp.company_id = p_company_id
      AND cp.ano = p_anio AND cp.mes = p_mes
      AND public.fn_adm_ciclo_norm(cp.ciclo) = v_ciclo
    ORDER BY cp.ide DESC
    LIMIT 1;

    SELECT count(*) INTO planilla_existente
    FROM public.historicomedicion h
    WHERE h.company_id = p_company_id
      AND h.ano = p_anio AND h.mes = p_mes
      AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo;

    SELECT count(*) INTO rollover_disponible
    FROM public.historicomedicion h
    WHERE h.company_id = p_company_id
      AND h.ano = v_anio_prev AND h.mes = v_mes_prev
      AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo;

    SELECT count(*) INTO clientes_activos
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.estado = true
      AND cm.ciclos_id = ANY (fn_adm_periodo_ciclo_info.ciclos_ids);

    -- Rutas del ciclo con su lector asignado (adm_lector_credencial activo).
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
               'ruta', btrim(r.codruta),
               'lector', lc.lector
           ) ORDER BY r.codruta), '[]'::jsonb),
           count(*) FILTER (WHERE lc.lector IS NULL)
    INTO rutas, rutas_sin_lector
    FROM public.rutas r
    LEFT JOIN LATERAL (
        SELECT concat_ws(' - ', btrim(l.codigo), NULLIF(btrim(l.lector_nombre), '')) AS lector
        FROM public.adm_lector_credencial l
        WHERE l.company_id = p_company_id
          AND l.activo
          AND upper(btrim(COALESCE(l.ruta, ''))) = upper(btrim(r.codruta))
        LIMIT 1
    ) lc ON true
    WHERE r.estado = true
      AND r.codciclo = ANY (fn_adm_periodo_ciclo_info.ciclos_ids);

    rutas := COALESCE(rutas, '[]'::jsonb);
    rutas_sin_lector := COALESCE(rutas_sin_lector, 0);

    SELECT COALESCE(jsonb_agg(jsonb_build_object(
               'anio', p.anio, 'mes', p.mes, 'ciclo', pc.ciclo_codigo
           ) ORDER BY p.anio, p.mes, pc.ciclo_codigo), '[]'::jsonb)
    INTO otros_ciclos_abiertos
    FROM public.adm_periodo_comercial_ciclo pc
    JOIN public.adm_periodo_comercial p
      ON p.company_id = pc.company_id
     AND p.periodo_comercial_id = pc.periodo_comercial_id
    WHERE pc.company_id = p_company_id
      AND pc.status_id = 1
      AND NOT (p.anio = p_anio AND p.mes = p_mes AND pc.ciclo_codigo = v_ciclo);

    RETURN NEXT;
END;
$function$;

-- Preview para la pantalla: qué pasaría al abrir, con los mismos avisos que
-- devolverá la apertura real. No escribe nada.
CREATE OR REPLACE FUNCTION public.fn_adm_periodo_ciclo_preview(
    p_company_id bigint,
    p_anio integer,
    p_mes integer,
    p_ciclo text
) RETURNS jsonb
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    i record;
    v_avisos jsonb := '[]'::jsonb;
    v_bloqueo text;
    v_origen text;
    v_clientes bigint;
BEGIN
    IF p_anio IS NULL OR p_mes IS NULL OR p_mes NOT BETWEEN 1 AND 12 THEN
        RAISE EXCEPTION 'Período comercial inválido: anio=%, mes=%.', p_anio, p_mes;
    END IF;

    SELECT * INTO i FROM public.fn_adm_periodo_ciclo_info(p_company_id, p_anio, p_mes, p_ciclo);

    IF i.ciclo_norm = '' OR length(i.ciclo_norm) > 2 THEN
        RAISE EXCEPTION 'Código de ciclo inválido: "%". Se esperan códigos cortos (01, 1).', p_ciclo;
    END IF;

    -- Bloqueos (la apertura real lanza excepción; el preview los reporta)
    v_bloqueo := CASE
        WHEN i.anterior_abierto THEN 'PERIODO_ANTERIOR_ABIERTO'
        WHEN i.periodo_status = 2 THEN 'PERIODO_CERRADO'
        WHEN i.ciclo_status = 2 THEN 'CICLO_CERRADO'
        ELSE NULL
    END;

    IF i.fecha_limite_calendario IS NULL THEN
        v_avisos := v_avisos || jsonb_build_array('SIN_CALENDARIO');
    END IF;

    IF i.planilla_existente > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('PLANILLA_EXISTENTE');
        v_origen := 'EXISTENTE';
        v_clientes := i.planilla_existente;
    ELSIF i.rollover_disponible > 0 THEN
        v_origen := 'ROLL_OVER';
        v_clientes := i.rollover_disponible;
    ELSIF i.clientes_activos > 0 THEN
        v_origen := 'DESDE_CLIENTES';
        v_clientes := i.clientes_activos;
    ELSE
        v_avisos := v_avisos || jsonb_build_array('SIN_CLIENTES');
        v_origen := 'VACIA';
        v_clientes := 0;
    END IF;

    IF i.rutas_sin_lector > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('RUTAS_SIN_LECTOR');
    END IF;

    IF jsonb_array_length(i.otros_ciclos_abiertos) > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('OTRO_CICLO_ABIERTO');
    END IF;

    IF i.ciclo_status = 1 THEN
        v_avisos := v_avisos || jsonb_build_array('CICLO_YA_ABIERTO');
    END IF;

    RETURN jsonb_build_object(
        'anio', p_anio,
        'mes', p_mes,
        'ciclo', i.ciclo_norm,
        'bloqueo', v_bloqueo,
        'fecha_limite', COALESCE(i.fecha_limite_calendario,
                                 (make_date(p_anio, p_mes, 1) + interval '1 month' - interval '1 day')::date),
        'origen_planilla', v_origen,
        'clientes_planilla', v_clientes,
        'rutas', i.rutas,
        'rutas_sin_lector', i.rutas_sin_lector,
        'otros_ciclos_abiertos', i.otros_ciclos_abiertos,
        'avisos', v_avisos
    );
END;
$function$;

-- ----------------------------------------------------------------------------
-- 4. Apertura integral
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_periodo_ciclo_abrir(
    p_company_id bigint,
    p_anio integer,
    p_mes integer,
    p_ciclo text,
    p_usuario text
) RETURNS jsonb
LANGUAGE plpgsql
AS $function$
DECLARE
    i record;
    v_usuario text := left(COALESCE(NULLIF(btrim(p_usuario), ''), 'system'), 100);
    v_periodo_id bigint;
    v_ciclo_id bigint;
    v_fecha_limite date;
    v_avisos jsonb := '[]'::jsonb;
    v_origen text := 'EXISTENTE';
    v_generadas bigint := 0;
BEGIN
    IF p_anio IS NULL OR p_mes IS NULL OR p_mes NOT BETWEEN 1 AND 12 THEN
        RAISE EXCEPTION 'Período comercial inválido: anio=%, mes=%.', p_anio, p_mes;
    END IF;

    -- Serializa aperturas concurrentes del mismo (empresa, período, ciclo):
    -- sin esto, dos clics simultáneos leerían el mismo snapshot (TOCTOU) y
    -- duplicarían la planilla (historicomedicion no tiene clave única) o el
    -- perdedor moriría con un unique_violation crudo en vez del flujo
    -- idempotente. El lock se libera al terminar la transacción.
    PERFORM pg_advisory_xact_lock(hashtextextended(
        format('adm_abrir_ciclo|%s|%s|%s|%s',
               p_company_id, p_anio, p_mes, public.fn_adm_ciclo_norm(p_ciclo)), 0));

    SELECT * INTO i FROM public.fn_adm_periodo_ciclo_info(p_company_id, p_anio, p_mes, p_ciclo);

    IF i.ciclo_norm = '' OR length(i.ciclo_norm) > 2 THEN
        RAISE EXCEPTION 'Código de ciclo inválido: "%". Se esperan códigos cortos (01, 1).', p_ciclo;
    END IF;

    -- Secuencia SIEMPRE: el cierre del mes anterior habilita el siguiente (D7).
    IF i.anterior_abierto THEN
        RAISE EXCEPTION 'PERIODO_ANTERIOR_ABIERTO: cierre el mes comercial anterior antes de abrir %-%.',
            p_anio, lpad(p_mes::text, 2, '0');
    END IF;

    IF i.periodo_status = 2 THEN
        RAISE EXCEPTION 'PERIODO_CERRADO: el período comercial %-% ya fue cerrado; no se reabre.',
            p_anio, lpad(p_mes::text, 2, '0');
    END IF;

    IF i.ciclo_status = 2 THEN
        RAISE EXCEPTION 'CICLO_CERRADO: el ciclo % del período %-% ya fue cerrado; no se reabre.',
            i.ciclo_norm, p_anio, lpad(p_mes::text, 2, '0');
    END IF;

    -- Período del mes: reutiliza el abierto o lo crea.
    IF i.periodo_comercial_id IS NOT NULL THEN
        v_periodo_id := i.periodo_comercial_id;
        PERFORM 1 FROM public.adm_periodo_comercial p
        WHERE p.company_id = p_company_id AND p.periodo_comercial_id = v_periodo_id
        FOR UPDATE;
    ELSE
        INSERT INTO public.adm_periodo_comercial
            (company_id, anio, mes, status_id, fecha_apertura, abierto_por, created_by)
        VALUES
            (p_company_id, p_anio, p_mes, 1, now(), v_usuario, v_usuario)
        RETURNING adm_periodo_comercial.periodo_comercial_id INTO v_periodo_id;
    END IF;

    -- fecha_limite del calendario de facturación (fechalec del ciclo).
    v_fecha_limite := i.fecha_limite_calendario;
    IF v_fecha_limite IS NULL THEN
        v_fecha_limite := (make_date(p_anio, p_mes, 1) + interval '1 month' - interval '1 day')::date;
        v_avisos := v_avisos || jsonb_build_array('SIN_CALENDARIO');
    END IF;

    -- Ciclo: reutiliza el abierto (idempotente) o lo crea.
    IF i.periodo_ciclo_id IS NOT NULL THEN
        v_ciclo_id := i.periodo_ciclo_id;
        v_avisos := v_avisos || jsonb_build_array('CICLO_YA_ABIERTO');
    ELSE
        INSERT INTO public.adm_periodo_comercial_ciclo
            (company_id, periodo_comercial_id, ciclo_codigo, status_id,
             fecha_apertura, abierto_por, fecha_limite, created_by)
        VALUES
            (p_company_id, v_periodo_id, i.ciclo_norm, 1, now(), v_usuario, v_fecha_limite, v_usuario)
        RETURNING adm_periodo_comercial_ciclo.periodo_ciclo_id INTO v_ciclo_id;
    END IF;

    -- Planilla de lectura: idempotente; roll-over del mes anterior o alta
    -- desde cliente_maestro (lógica portada de GenerarPeriodoAsync C#).
    IF i.planilla_existente > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('PLANILLA_EXISTENTE');
        v_generadas := i.planilla_existente;
        v_origen := 'EXISTENTE';
    ELSE
        INSERT INTO public.historicomedicion
            (company_id, ano, mes, contador, ciclo, ruta, secuencia, clave,
             propietario, ubicacion, fecha, usuario, lect_ant, lect_act,
             fecha_lect_ant, fecha_lect_act, consumo, consumoant, condicion, observacion)
        SELECT p_company_id, p_anio, p_mes, h.contador, i.ciclo_norm, h.ruta,
               h.secuencia, h.clave, h.propietario, h.ubicacion, current_date,
               NULL, h.lect_act, NULL, h.fecha_lect_act, NULL, 0, h.consumo,
               h.condicion, NULL
        FROM public.historicomedicion h
        WHERE h.company_id = p_company_id
          AND h.ano = CASE WHEN p_mes = 1 THEN p_anio - 1 ELSE p_anio END
          AND h.mes = CASE WHEN p_mes = 1 THEN 12 ELSE p_mes - 1 END
          AND public.fn_adm_ciclo_norm(h.ciclo) = i.ciclo_norm;

        GET DIAGNOSTICS v_generadas = ROW_COUNT;
        v_origen := 'ROLL_OVER';

        IF v_generadas = 0 THEN
            INSERT INTO public.historicomedicion
                (company_id, ano, mes, contador, ciclo, ruta, secuencia, clave,
                 propietario, ubicacion, fecha, usuario, lect_ant, lect_act,
                 fecha_lect_ant, fecha_lect_act, consumo, consumoant, condicion, observacion)
            SELECT p_company_id, p_anio, p_mes,
                   COALESCE(NULLIF(btrim(md.maestro_medidor_numero), ''), cm.contador),
                   i.ciclo_norm,
                   public.fn_adm_ruta_de_indicativo(cm.maestro_cliente_indicativo_ruta),
                   cm.maestro_cliente_secuencia,
                   cm.maestro_cliente_clave,
                   cm.maestro_cliente_nombre,
                   cd.detalle_cliente_direccion,
                   current_date, NULL, 0, NULL, NULL, NULL, 0, 0, NULL, NULL
            FROM public.cliente_maestro cm
            LEFT JOIN LATERAL (
                SELECT d.detalle_cliente_direccion, d.maestro_medidor_id
                FROM public.cliente_detalle d
                WHERE d.company_id = cm.company_id
                  AND d.maestro_cliente_id = cm.maestro_cliente_id
                ORDER BY COALESCE(d.fechamodificacion, d.fechacreacion) DESC NULLS LAST
                LIMIT 1
            ) cd ON true
            LEFT JOIN public.maestro_medidor md
              ON md.maestro_medidor_id = cd.maestro_medidor_id
            WHERE cm.company_id = p_company_id
              AND cm.estado = true
              AND cm.ciclos_id = ANY (i.ciclos_ids);

            GET DIAGNOSTICS v_generadas = ROW_COUNT;
            v_origen := 'DESDE_CLIENTES';
        END IF;

        IF v_generadas = 0 THEN
            v_avisos := v_avisos || jsonb_build_array('SIN_CLIENTES');
            v_origen := 'VACIA';
        END IF;
    END IF;

    IF i.rutas_sin_lector > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('RUTAS_SIN_LECTOR');
    END IF;

    IF jsonb_array_length(i.otros_ciclos_abiertos) > 0 THEN
        v_avisos := v_avisos || jsonb_build_array('OTRO_CICLO_ABIERTO');
    END IF;

    RETURN jsonb_build_object(
        'periodo_comercial_id', v_periodo_id,
        'periodo_ciclo_id', v_ciclo_id,
        'anio', p_anio,
        'mes', p_mes,
        'ciclo', i.ciclo_norm,
        'fecha_limite', v_fecha_limite,
        'origen_planilla', v_origen,
        'clientes_planilla', v_generadas,
        'rutas', i.rutas,
        'rutas_sin_lector', i.rutas_sin_lector,
        'otros_ciclos_abiertos', i.otros_ciclos_abiertos,
        'avisos', v_avisos
    );
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_periodo_ciclo_abrir(bigint, integer, integer, text, text) IS
'Fase B apertura-ciclo-único (2026-07-15): apertura integral — valida secuencia SIEMPRE, crea período+ciclo (fecha_limite del calendario de facturación), genera la planilla de lectura (roll-over o desde clientes) y devuelve resumen jsonb con avisos. Reemplaza a sp_adm_periodo_comercial_abrir y al flujo "Generar período" del Auxiliar de Lectura.';

-- ----------------------------------------------------------------------------
-- 5. Deshacer apertura: solo si el ciclo está abierto y su planilla no tiene
--    lecturas registradas ni facturas emitidas.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_periodo_ciclo_deshacer(
    p_company_id bigint,
    p_periodo_ciclo_id bigint,
    p_usuario text
) RETURNS jsonb
LANGUAGE plpgsql
AS $function$
DECLARE
    v_ciclo record;
    v_planilla bigint := 0;
    v_periodo_eliminado boolean := false;
BEGIN
    SELECT pc.periodo_ciclo_id, pc.periodo_comercial_id, pc.ciclo_codigo,
           pc.status_id, p.anio, p.mes
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
        RAISE EXCEPTION 'CICLO_CERRADO: el ciclo % de %-% está cerrado; el cierre no se deshace.',
            v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');
    END IF;

    -- Lecturas registradas o facturas emitidas bloquean el deshacer. El motor
    -- V3 estampa usuario y numerofactura en la fila de la planilla al emitir.
    IF EXISTS (
        SELECT 1
        FROM public.historicomedicion h
        WHERE h.company_id = p_company_id
          AND h.ano = v_ciclo.anio AND h.mes = v_ciclo.mes
          AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo.ciclo_codigo
          AND (NULLIF(btrim(COALESCE(h.usuario, '')), '') IS NOT NULL
               OR NULLIF(btrim(COALESCE(h.numerofactura, '')), '') IS NOT NULL)
    ) THEN
        RAISE EXCEPTION 'LECTURAS_REGISTRADAS: el ciclo % de %-% ya tiene lecturas o facturas; no se puede deshacer la apertura.',
            v_ciclo.ciclo_codigo, v_ciclo.anio, lpad(v_ciclo.mes::text, 2, '0');
    END IF;

    DELETE FROM public.historicomedicion h
    WHERE h.company_id = p_company_id
      AND h.ano = v_ciclo.anio AND h.mes = v_ciclo.mes
      AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo.ciclo_codigo;

    GET DIAGNOSTICS v_planilla = ROW_COUNT;

    -- El trigger espejo de F7 borra la fila correspondiente de historialmes.
    DELETE FROM public.adm_periodo_comercial_ciclo pc
    WHERE pc.company_id = p_company_id
      AND pc.periodo_ciclo_id = p_periodo_ciclo_id;

    IF NOT EXISTS (
        SELECT 1 FROM public.adm_periodo_comercial_ciclo pc
        WHERE pc.company_id = p_company_id
          AND pc.periodo_comercial_id = v_ciclo.periodo_comercial_id
    ) THEN
        DELETE FROM public.adm_periodo_comercial p
        WHERE p.company_id = p_company_id
          AND p.periodo_comercial_id = v_ciclo.periodo_comercial_id;
        v_periodo_eliminado := true;
    END IF;

    RETURN jsonb_build_object(
        'planilla_eliminada', v_planilla,
        'ciclo_eliminado', true,
        'periodo_eliminado', v_periodo_eliminado
    );
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_periodo_ciclo_deshacer(bigint, bigint, text) IS
'Fase B apertura-ciclo-único: deshace una apertura de ciclo (borra planilla + ciclo + período si queda vacío) SOLO si no hay lecturas registradas ni facturas. Reemplaza a EliminarPeriodoAsync del Auxiliar de Lectura.';

-- ----------------------------------------------------------------------------
-- 6. Retiro del SP viejo de apertura (tenía bypass p_validar_secuencia y no
--    generaba planilla). El servicio C# queda repuntado en esta misma fase.
-- ----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.sp_adm_periodo_comercial_abrir(bigint, integer, integer, character varying, text, boolean);

COMMIT;
