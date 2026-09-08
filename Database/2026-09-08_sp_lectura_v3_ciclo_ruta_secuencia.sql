-- =============================================================================
-- 2026-09-08 — La emision de lectura dejaba el historico sin ciclo, sin ruta
--              y sin secuencia
--
-- EL SINTOMA
--   Emitir una lectura para un cliente que NO tiene fila en historicomedicion
--   de ese periodo funciona la primera vez. La segunda vez, para el mismo
--   cliente y periodo, revienta con:
--
--     P0001: No hay periodo abierto para anio=2026 mes=7 ciclo=(sin ciclo).
--
--   El mensaje es enganoso: el periodo comercial SI esta abierto. Lo que pasa
--   es que la primera emision dejo el historico sin ciclo, y la validacion
--   posterior no sabe con que ciclo comparar.
--
-- LA CAUSA (medida, no supuesta)
--   `sp_adm_calcular_factura_lectura` devuelve ciclo, ruta y secuencia con
--   `COALESCE(v_historico.X, '')`: cuando el cliente no tiene historico del
--   periodo, los tres salen como CADENA VACIA, no como NULL.
--
--   `sp_lectura_v3` los resolvia asi:
--
--     v_ciclo := NULLIF(COALESCE(v_calc.ciclo, p_ciclo), '');
--
--   El COALESCE se evalua primero y la cadena vacia LE GANA a p_ciclo, porque
--   '' no es NULL. Solo despues el NULLIF la convierte en NULL. Resultado: el
--   valor de reserva **nunca** se usaba. Lo mismo en las dos lineas de al lado,
--   con la ruta y la secuencia del maestro del cliente.
--
--   Comprobacion directa del orden de evaluacion:
--     SELECT NULLIF(COALESCE('', '01'), '');                  -- NULL   (antes)
--     SELECT COALESCE(NULLIF('', ''), NULLIF('01', ''));      -- '01'   (ahora)
--
--   Y el parametro p_ciclo tampoco ayudaba: `EmisionLecturaService.cs` linea
--   460 llama a sp_lectura_v3 con `p_ciclo => NULL`, igual que la prueba.
--
-- POR QUE NO SE VE EN PRODUCCION
--   El INSERT de historicomedicion dentro de sp_lectura_v3 es la rama
--   excepcional: solo corre si el UPDATE previo no encontro fila. En la
--   operacion normal la APERTURA DEL CICLO (`sp_adm_periodo_ciclo_abrir`) ya
--   creo el historico con su ciclo, asi que la lectura solo actualiza.
--   Por eso `siad_v4` tiene 2.320 filas en historicomedicion y NINGUNA sin
--   ciclo. El defecto solo se dispara al emitir para un cliente que no entro
--   en la apertura del ciclo — y ahi lo deja bloqueado para ese periodo.
--
-- QUE CAMBIA
--   Solo tres asignaciones de `sp_lectura_v3`. Cada candidato se anula por
--   separado (`NULLIF(btrim(...), '')`) antes de elegir, y el ciclo gana un
--   tercer recurso: el codigo de ciclo del maestro del cliente, via
--   `ciclos.ciclos_id = cliente_maestro.ciclos_id`.
--
--   **El resultado solo cambia donde antes salia NULL.** Si el calculo trae
--   ciclo, ruta o secuencia, la funcion devuelve exactamente lo mismo que
--   antes. No se toca ninguna otra linea del procedimiento.
--
-- DONDE SE APLICA
--   ⚠️ **A PRODUCCION NO SE LE TOCA NADA.** El flujo de este proyecto va en un
--   solo sentido: `siad_v4` se replica hacia local, nunca al reves.
--   * `siad_v3_restore` (mirror) — aplicado el 2026-09-08.
--   * `siad_v3_desarrollo` — se puede aplicar.
--   * `siad_v4` (produccion) — **NO**.
--
--   Dicho eso, el defecto **existe igual arriba**: antes de este cambio la
--   funcion tenia la MISMA huella md5 en las dos bases. Queda anotado como
--   hallazgo, no como pendiente de despliegue. Si algun dia se decide
--   corregirlo arriba, sera una decision aparte y por la via que el proyecto
--   use para eso.
--
--   ⚠️ **OJO con el restore:** mientras esta correccion viva solo en el mirror,
--   es una DIVERGENCIA respecto de produccion. Al replicar `siad_v4` sobre el
--   mirror se pierde, y hay que volver a aplicarla si se quiere conservar.
--
-- IMPACTO DE DATOS
--   Ninguno directo: es un CREATE OR REPLACE, no toca filas. **No repara los
--   historicos ya escritos sin ciclo.** En el mirror y en produccion hoy no
--   hay ninguno, asi que no hace falta backfill; si algun dia aparecen, se
--   corrigen aparte.
--
-- IDEMPOTENTE
--   Es un CREATE OR REPLACE. Se puede reejecutar.
--
-- REVERSA
--   Volver a poner las tres lineas originales:
--     v_ciclo := NULLIF(COALESCE(v_calc.ciclo, p_ciclo), '');
--     v_ruta := NULLIF(COALESCE(v_calc.ruta, v_cliente.maestro_cliente_indicativo_ruta), '');
--     v_secuencia := NULLIF(COALESCE(v_calc.secuencia, v_cliente.maestro_cliente_secuencia), '');
-- =============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

CREATE OR REPLACE FUNCTION public.sp_lectura_v3(p_company_id bigint, p_anio integer, p_mes integer, p_ciclo character varying DEFAULT NULL::character varying, p_clave character varying DEFAULT NULL::character varying, p_contador character varying DEFAULT NULL::character varying, p_fecha_lectura date DEFAULT CURRENT_DATE, p_usuario character varying DEFAULT NULL::character varying, p_lectura_actual numeric DEFAULT NULL::numeric, p_ser3 character DEFAULT NULL::bpchar, p_ser4 character DEFAULT NULL::bpchar, p_observacion character varying DEFAULT NULL::character varying, p_condicion_lectura character varying DEFAULT 'N'::character varying, p_lectura_promedio numeric DEFAULT NULL::numeric, p_numero_factura character varying DEFAULT NULL::character varying, p_correlativo_cai integer DEFAULT NULL::integer, p_id_cai integer DEFAULT NULL::integer, p_tienemedidor character DEFAULT NULL::bpchar, p_informativo character varying DEFAULT NULL::character varying, p_imagen bytea DEFAULT NULL::bytea, p_categoria character DEFAULT NULL::bpchar, p_lectura_uuid character varying DEFAULT NULL::character varying)
 RETURNS TABLE(success boolean, codigo text, mensaje text, factura_id integer, numrecibo integer, numero_factura text, cliente_id bigint, cliente_clave text, cliente_nombre text, consumo numeric, subtotal numeric, subtotal_ajustes numeric, saldos_anteriores numeric, recargos numeric, total numeric, taservi1 numeric, taservi2 numeric, taservi3 numeric, taservi4 numeric, detalle_servicios_json jsonb, warnings_json jsonb)
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_cliente public.cliente_maestro%ROWTYPE;
    v_calc record;
    v_factura_id integer;
    v_numrecibo integer;
    v_fechavence date;
    v_plazo integer := 0;
    v_numdei text := '';
    v_prefijo_documento text := '';
    v_ciclo text;
    v_ruta text;
    v_secuencia text;
    v_tiene_medidor_char text;
    v_saldo_total numeric := 0;
    v_saldo_detalle numeric := 0;
    v_saldo_servicio_actual numeric := 0;
    -- F4: saldos por servicio de los documentos pendientes, capturados ANTES
    -- de compensar la factura anterior (la fuente por documentos devolvería 0
    -- después del UPDATE de compensación).
    v_saldos_previos jsonb := '{}'::jsonb;
    v_uuid text := NULLIF(BTRIM(COALESCE(p_lectura_uuid, '')), '');
    v_factura_existente record;
    v_factura_periodo record;
    v_detalle record;
BEGIN
    SELECT *
    INTO v_cliente
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.maestro_cliente_clave = p_clave
      AND cm.estado = true
    LIMIT 1;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe cliente activo con clave=% para company_id=%.',
            p_clave, p_company_id;
    END IF;

    IF p_id_cai IS NOT NULL THEN
        SELECT c.prefijo_documento
        INTO v_prefijo_documento
        FROM public.adm_cai_facturacion c
        WHERE c.company_id = p_company_id
          AND c.cai_id = p_id_cai
          AND c.status_id = 1
        LIMIT 1;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'No existe CAI V3 activo con id=% para company_id=%.', p_id_cai, p_company_id;
        END IF;
    END IF;

    IF p_id_cai IS NOT NULL AND (p_correlativo_cai IS NULL OR p_correlativo_cai <= 0) THEN
        RAISE EXCEPTION 'Correlativo CAI requerido para registrar lectura V3.';
    END IF;

    IF p_numero_factura IS NULL AND p_id_cai IS NOT NULL THEN
        p_numero_factura := concat(COALESCE(v_prefijo_documento, ''), lpad(COALESCE(p_correlativo_cai, 0)::text, 8, '0'));
    END IF;

    IF p_numero_factura IS NULL OR btrim(p_numero_factura) = '' THEN
        RAISE EXCEPTION 'Numero de factura requerido para registrar lectura.';
    END IF;

    IF v_uuid IS NOT NULL THEN
        SELECT
            e.factura_id,
            e.numero_factura
        INTO v_factura_existente
        FROM public.adm_cai_correlativo_emitido e
        WHERE e.company_id = p_company_id
          AND e.lectura_uuid = v_uuid
          AND e.factura_id IS NOT NULL
        ORDER BY e.cai_correlativo_emitido_id DESC
        LIMIT 1;

        IF FOUND THEN
            RETURN QUERY
            WITH factura_row AS (
                SELECT
                    f.id,
                    f.numrecibo,
                    f.numfactura,
                    COALESCE(f.saldototal, 0)::numeric(18, 4) AS total
                FROM public.factura f
                WHERE f.id = v_factura_existente.factura_id
                LIMIT 1
            ),
            historico_row AS (
                SELECT
                    COALESCE(hm.consumo, 0)::numeric(18, 4) AS consumo,
                    COALESCE(hm.descuentoapp, 0)::numeric(18, 4) AS subtotal_ajustes,
                    COALESCE(hm.taservi1, 0)::numeric(18, 4) AS taservi1,
                    COALESCE(hm.taservi2, 0)::numeric(18, 4) AS taservi2,
                    COALESCE(hm.taservi3, 0)::numeric(18, 4) AS taservi3,
                    COALESCE(hm.taservi4, 0)::numeric(18, 4) AS taservi4
                FROM public.historicomedicion hm
                JOIN factura_row fr
                  ON fr.numfactura = hm.numerofactura
                WHERE hm.clave = v_cliente.maestro_cliente_clave
                ORDER BY hm.ide DESC
                LIMIT 1
            ),
            detalle_json AS (
                SELECT
                    COALESCE(
                        jsonb_agg(
                            jsonb_build_object(
                                'servicio_codigo', fd.tiposervicio,
                                'servicio_nombre', fd.descripcion,
                                'monto_final', COALESCE(fd.montovalor, 0)
                            )
                            ORDER BY fd.tiposervicio, fd.descripcion
                        ),
                        '[]'::jsonb
                    ) AS detalle_servicios_json,
                    COALESCE(SUM(COALESCE(fd.montovalor, 0)), 0)::numeric(18, 4) AS subtotal
                FROM public.factura_detalle fd
                JOIN factura_row fr
                  ON fr.id = fd.factura_id
            )
            SELECT
                true,
                'IDEMPOTENTE'::text,
                'La lectura ya habia sido registrada anteriormente.'::text,
                fr.id,
                fr.numrecibo,
                fr.numfactura::text,
                v_cliente.maestro_cliente_id::bigint,
                v_cliente.maestro_cliente_clave::text,
                v_cliente.maestro_cliente_nombre::text,
                COALESCE(hr.consumo, 0),
                COALESCE(dj.subtotal, 0),
                COALESCE(hr.subtotal_ajustes, 0),
                GREATEST(fr.total - COALESCE(dj.subtotal, 0), 0)::numeric(18, 4),
                0::numeric,
                fr.total,
                COALESCE(hr.taservi1, 0),
                COALESCE(hr.taservi2, 0),
                COALESCE(hr.taservi3, 0),
                COALESCE(hr.taservi4, 0),
                dj.detalle_servicios_json,
                jsonb_build_array('LECTURA_IDEMPOTENTE')
            FROM factura_row fr
            CROSS JOIN detalle_json dj
            LEFT JOIN historico_row hr ON true;

            RETURN;
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.factura f
        WHERE f.clientecodigo = v_cliente.maestro_cliente_clave
          AND f.numfactura = p_numero_factura
    ) THEN
        RAISE EXCEPTION 'Ya existe factura con numero=% para cliente=%.', p_numero_factura, v_cliente.maestro_cliente_clave;
    END IF;

    SELECT f.id, f.numfactura, f.estado
    INTO v_factura_periodo
    FROM public.factura f
    WHERE f.clientecodigo = v_cliente.maestro_cliente_clave
      AND f.ano = p_anio::text
      AND f.mes = p_mes::text
      AND COALESCE(f.estado, '') <> 'N'
    ORDER BY f.id DESC
    LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION 'FACTURA_YA_EMITIDA: ya existe factura % (estado=%) para cliente=% en periodo %/%. Anule la factura previa antes de re-emitir.',
            v_factura_periodo.numfactura, v_factura_periodo.estado,
            v_cliente.maestro_cliente_clave, p_anio, p_mes;
    END IF;

    SELECT *
    INTO v_calc
    FROM public.sp_adm_calcular_factura_lectura(
        p_company_id,
        p_anio,
        p_mes,
        v_cliente.maestro_cliente_id,
        p_contador,
        COALESCE(p_fecha_lectura, current_date),
        p_lectura_actual,
        p_condicion_lectura,
        p_lectura_promedio,
        p_usuario,
        p_observacion,
        p_id_cai,
        p_correlativo_cai,
        p_numero_factura,
        p_informativo
    );

    IF NOT FOUND THEN
        RAISE EXCEPTION 'sp_adm_calcular_factura_lectura no devolvio resultado para cliente=%.', v_cliente.maestro_cliente_clave;
    END IF;

    -- 2026-09-08: el orden de NULLIF y COALESCE estaba invertido.
    -- sp_adm_calcular_factura_lectura devuelve ciclo, ruta y secuencia como
    -- CADENA VACIA cuando el cliente no tiene historico del periodo (usa
    -- COALESCE(v_historico.X, '')). Con la forma anterior esa cadena vacia
    -- ganaba el COALESCE y solo despues el NULLIF la volvia NULL, asi que el
    -- valor de reserva NUNCA se usaba: ni p_ciclo ni los datos del maestro del
    -- cliente. El historico nacia sin ciclo, sin ruta y sin secuencia, y desde
    -- ahi toda emision posterior de ese cliente en ese periodo moria con
    -- 'No hay periodo abierto ... ciclo=(sin ciclo)', mensaje enganoso porque
    -- el periodo si estaba abierto.
    -- Ahora cada candidato se anula por separado antes de elegir, y el ciclo
    -- tiene un tercer recurso: el del maestro del cliente.
    v_ciclo := COALESCE(
        NULLIF(btrim(v_calc.ciclo), ''),
        NULLIF(btrim(p_ciclo), ''),
        NULLIF(btrim((SELECT c.ciclos_codigo
                        FROM public.ciclos c
                       WHERE c.ciclos_id = v_cliente.ciclos_id)), ''));
    v_ruta := COALESCE(
        NULLIF(btrim(v_calc.ruta), ''),
        NULLIF(btrim(v_cliente.maestro_cliente_indicativo_ruta), ''));
    v_secuencia := COALESCE(
        NULLIF(btrim(v_calc.secuencia), ''),
        NULLIF(btrim(v_cliente.maestro_cliente_secuencia), ''));
    v_tiene_medidor_char := CASE
        WHEN COALESCE(v_calc.tiene_medidor, v_cliente.maestro_cliente_tiene_medidor, false) THEN 'S'
        ELSE 'N'
    END;

    IF v_ciclo IS NOT NULL THEN
        -- Fase A (2026-07-14): calendariopro es multitenant — filtro por
        -- company_id. El match de ciclo tolera '1' vs '01' (SIMAFI sin cero a
        -- la izquierda vs planilla V3 normalizada a 2 dígitos).
        SELECT cp.fechavence, cp.diasvence
        INTO v_fechavence, v_plazo
        FROM public.calendariopro cp
        WHERE cp.company_id = p_company_id
          AND cp.ano = p_anio
          AND cp.mes = p_mes
          AND (
                btrim(cp.ciclo) = btrim(v_ciclo)
                OR (cp.ciclo ~ '^[0-9]+$' AND v_ciclo ~ '^[0-9]+$'
                    AND cp.ciclo::int = v_ciclo::int)
              )
        ORDER BY cp.ide DESC
        LIMIT 1;
    END IF;

    IF v_tiene_medidor_char = 'N' THEN
        UPDATE public.historicosinmedidor
        SET numerofactura = p_numero_factura,
            correlativocai = p_correlativo_cai,
            idcai = p_id_cai,
            fecha = now(),
            usuario = p_usuario
        WHERE cuenta = v_cliente.maestro_cliente_clave
          AND ano = p_anio
          AND mes = p_mes;

        IF NOT FOUND THEN
            INSERT INTO public.historicosinmedidor(
                cuenta, ano, mes, numerofactura, correlativocai, idcai, fecha, usuario
            )
            VALUES (
                v_cliente.maestro_cliente_clave, p_anio, p_mes, p_numero_factura, p_correlativo_cai, p_id_cai, now(), p_usuario
            );
        END IF;
    ELSE
        UPDATE public.historicomedicion
        SET fecha_lect_act   = COALESCE(p_fecha_lectura, current_date),
            usuario          = p_usuario,
            lect_act         = v_calc.lectura_actual_efectiva,
            consumo          = v_calc.consumo_facturable,
            taservi1         = COALESCE(v_calc.taservi1, 0),
            taservi2         = COALESCE(v_calc.taservi2, 0),
            taservi3         = COALESCE(v_calc.taservi3, 0),
            taservi4         = COALESCE(v_calc.taservi4, 0),
            ser3             = p_ser3,
            ser4             = p_ser4,
            observacion      = p_observacion,
            condicion        = v_calc.condicion_lectura_aplicada,
            lec_prom         = p_lectura_promedio,
            numerofactura    = p_numero_factura,
            correlativocai   = p_correlativo_cai,
            idcai            = p_id_cai,
            codinfo          = left(COALESCE(p_informativo, ''), 1),
            imagenmedidor    = p_imagen,
            descuentoapp     = COALESCE(v_calc.subtotal_ajustes, 0),
            categoriacliente = p_categoria
        WHERE contador = COALESCE(p_contador, v_calc.contador)
          AND ano = p_anio
          AND mes = p_mes;

        IF NOT FOUND THEN
            INSERT INTO public.historicomedicion(
                company_id,
                ano, mes, contador, ciclo, ruta, secuencia, clave, fecha,
                usuario, lect_act, lect_ant, fecha_lect_act, consumo,
                taservi1, taservi2, taservi3, taservi4,
                ser3, ser4, observacion, condicion, lec_prom,
                numerofactura, correlativocai, idcai, codinfo, imagenmedidor,
                descuentoapp, categoriacliente
            )
            VALUES (
                p_company_id,
                p_anio,
                p_mes,
                COALESCE(p_contador, v_calc.contador),
                v_ciclo,
                v_ruta,
                v_secuencia,
                v_cliente.maestro_cliente_clave,
                COALESCE(p_fecha_lectura, current_date),
                p_usuario,
                v_calc.lectura_actual_efectiva,
                v_calc.lectura_anterior,
                COALESCE(p_fecha_lectura, current_date),
                v_calc.consumo_facturable,
                COALESCE(v_calc.taservi1, 0),
                COALESCE(v_calc.taservi2, 0),
                COALESCE(v_calc.taservi3, 0),
                COALESCE(v_calc.taservi4, 0),
                p_ser3,
                p_ser4,
                p_observacion,
                v_calc.condicion_lectura_aplicada,
                p_lectura_promedio,
                p_numero_factura,
                p_correlativo_cai,
                p_id_cai,
                left(COALESCE(p_informativo, ''), 1),
                p_imagen,
                COALESCE(v_calc.subtotal_ajustes, 0),
                p_categoria
            );
        END IF;
    END IF;

    -- F4: capturar el saldo pendiente por servicio (líneas de facturas A/B)
    -- ANTES de la compensación — es el arrastre que llevarán las líneas de la
    -- factura nueva (montovalor_saldo = saldo previo del servicio + mes).
    SELECT COALESCE(jsonb_object_agg(t.tiposervicio, t.saldo), '{}'::jsonb)
      INTO v_saldos_previos
      FROM (
          SELECT d.tiposervicio, SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)) AS saldo
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = v_cliente.maestro_cliente_clave
            AND f.estado IN ('A','B')
          GROUP BY d.tiposervicio
      ) t;

    UPDATE public.factura
       SET estado = 'C',
           estado_id = 2  -- Cobrada/Compensada (cfg_estado_documento_comercial)
     WHERE company_id = p_company_id  -- F4: faltaba el filtro de empresa
       AND clientecodigo = v_cliente.maestro_cliente_clave
       AND tipofacturacion = 'S'
       AND estado_id = 1;  -- Activa

    v_numdei := CASE
        WHEN p_id_cai IS NOT NULL THEN COALESCE(p_numero_factura, '')
        ELSE ''
    END;

    INSERT INTO public.factura AS f(
        company_id,
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
        estado_id,
        tipofacturacion
    )
    VALUES (
        p_company_id,
        p_numero_factura,
        v_cliente.maestro_cliente_clave,
        'F',
        p_anio::text,
        p_mes::text,
        COALESCE(p_fecha_lectura, current_date),
        v_fechavence,
        COALESCE(v_cliente.maestro_cliente_rtn, ''),
        concat_ws('/', p_anio::text, p_mes::text),
        v_numdei,
        COALESCE(v_calc.total_factura, 0),
        p_usuario,
        COALESCE(v_cliente.maestro_cliente_identidad, ''),
        'A',
        1,  -- estado_id = Activa (cfg_estado_documento_comercial)
        'S'
    )
    RETURNING f.id, f.numrecibo INTO v_factura_id, v_numrecibo;

    v_saldo_total := COALESCE(v_calc.saldos_anteriores, 0);

    FOR v_detalle IN
        SELECT *
        FROM jsonb_to_recordset(COALESCE(v_calc.detalle_servicios_json, '[]'::jsonb)) AS d(
            servicio_codigo text,
            servicio_nombre text,
            monto_final numeric
        )
        WHERE COALESCE(d.monto_final, 0) <> 0
        ORDER BY servicio_codigo
    LOOP
        v_saldo_total := v_saldo_total + COALESCE(v_detalle.monto_final, 0);
        -- F4: arrastre desde la captura previa a la compensación (documentos
        -- pendientes de ESTA empresa; antes: corrida legacy cross-company que
        -- quedaba desactualizada con los pagos del motor).
        v_saldo_servicio_actual := COALESCE((v_saldos_previos ->> v_detalle.servicio_codigo)::numeric, 0);
        v_saldo_detalle := v_saldo_servicio_actual + COALESCE(v_detalle.monto_final, 0);

        INSERT INTO public.factura_detalle(
            company_id,
            numrecibo,
            codigo,
            tiposervicio,
            descripcion,
            montovalor,
            factura_id,
            montovalor_saldo
        )
        VALUES (
            p_company_id,
            v_numrecibo,
            '',
            v_detalle.servicio_codigo,
            v_detalle.servicio_nombre,
            COALESCE(v_detalle.monto_final, 0),
            v_factura_id,
            v_saldo_detalle
        );

        -- F7 H2c: sin espejo legacy — la línea de factura ES el documento.

    END LOOP;

    RETURN QUERY
    SELECT
        true,
        'OK'::text,
        'Lectura registrada correctamente'::text,
        v_factura_id,
        v_numrecibo,
        p_numero_factura::text,
        v_cliente.maestro_cliente_id::bigint,
        v_cliente.maestro_cliente_clave::text,
        v_cliente.maestro_cliente_nombre::text,
        COALESCE(v_calc.consumo_facturable, 0),
        COALESCE(v_calc.subtotal_servicios, 0),
        COALESCE(v_calc.subtotal_ajustes, 0),
        COALESCE(v_calc.saldos_anteriores, 0),
        COALESCE(v_calc.recargos, 0),
        COALESCE(v_calc.total_factura, 0),
        COALESCE(v_calc.taservi1, 0),
        COALESCE(v_calc.taservi2, 0),
        COALESCE(v_calc.taservi3, 0),
        COALESCE(v_calc.taservi4, 0),
        COALESCE(v_calc.detalle_servicios_json, '[]'::jsonb),
        COALESCE(v_calc.warnings_json, '[]'::jsonb);
END;
$function$
;

COMMIT;

-- =============================================================================
-- VERIFICACION (ejecutar aparte, despues del COMMIT)
-- =============================================================================
--
-- Las tres asignaciones deben tener ya la forma nueva:
--   SELECT count(*) AS lineas_corregidas
--     FROM regexp_split_to_table(
--            (SELECT prosrc FROM pg_proc p
--               JOIN pg_namespace n ON n.oid = p.pronamespace
--              WHERE n.nspname = 'public' AND p.proname = 'sp_lectura_v3'),
--            E'\n') AS l
--    WHERE l LIKE '%NULLIF(btrim(v_calc.%';
--   -- esperado: 3
--
-- No debe quedar ninguna de la forma vieja:
--   SELECT count(*) AS lineas_viejas
--     FROM regexp_split_to_table(
--            (SELECT prosrc FROM pg_proc p
--               JOIN pg_namespace n ON n.oid = p.pronamespace
--              WHERE n.nspname = 'public' AND p.proname = 'sp_lectura_v3'),
--            E'\n') AS l
--    WHERE l LIKE '%NULLIF(COALESCE(v_calc.%';
--   -- esperado: 0
--
-- Y no debe haber historicos sin ciclo (hoy son 0 en las dos bases):
--   SELECT count(*) FROM public.historicomedicion
--    WHERE COALESCE(btrim(ciclo), '') = '';
--   -- esperado: 0
--
-- Prueba funcional: la que cubre el caso es
--   dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~EmisionLecturaPortalTests"
-- Emite, anula y vuelve a emitir sobre el mismo cliente y periodo. Antes de
-- este cambio, la tercera emision moria con "No hay periodo abierto".
