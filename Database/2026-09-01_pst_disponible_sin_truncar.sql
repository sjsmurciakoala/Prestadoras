-- =============================================================================
-- 2026-09-01 · El disponible presupuestario deja de ocultar los sobregiros
-- =============================================================================
-- HALLAZGO (ronda de QA del modulo de Compras, 2026-09-01 · H4)
--
--   El disponible se calculaba con GREATEST(proyeccion - comprometido - real, 0). Cuando una
--   partida esta sobregirada, ese truncado la reporta en 0.00 y deja de distinguirse una
--   partida justo agotada de una excedida en miles.
--
--   Caso real en el espejo: la cuenta 11401010101 tiene proyeccion 10,000.00 y comprometido
--   15,805.92. El disponible real es -5,805.92 y el sistema mostraba 0.00. El mensaje de
--   bloqueo decia "Faltan: 13,125.00" cuando en realidad faltaban 18,930.92, y un reporte que
--   sumara la columna "disponible" daba un total inflado porque los sobregiros no restaban.
--
--   El COMMENT de la propia vista ya declaraba la formula sin truncar
--   ("Disponible = proyeccion - comprometido - real"): el codigo era el que no la cumplia.
--
-- QUE CAMBIA
--
--   El disponible pasa a ser el valor real, negativo cuando hay sobregiro, en:
--     1. fn_pst_disponible               -> el panel previo de la orden de compra
--     2. vw_pst_ejecucion_presupuestaria -> el reporte de ejecucion presupuestaria
--     3. sp_pst_comprometer_documento    -> validacion y texto "Disponible / Faltan"
--     4. sp_pst_ajustar_compromiso       -> idem, al aumentar una orden
--     5. sp_pst_devengar_documento       -> idem, al recibir una factura
--
-- QUE **NO** CAMBIA (deliberado)
--
--   · La REGLA de bloqueo es identica. Se valida "requerido > disponible": cuando no hay
--     sobregiro el GREATEST era transparente (el valor ya era positivo) y cuando lo hay, tanto
--     0 como el negativo hacen fallar la comparacion. Nada que antes pasara empieza a fallar
--     ahora; solo mejora la CIFRA que se informa.
--   · fn_pst_aplicar_movimiento y la columna cacheada pst_config_presupuesto_dtl.valor_disponible
--     se dejan como estan. Esa columna la consume ademas la pantalla de configuracion
--     presupuestaria, que usa OTRA formula (valor_global - real, sin comprometido) y no entro en
--     esta ronda de pruebas. Cambiarla exige probar ese modulo aparte.
--   · Los GREATEST sobre valor_proyeccion / comprometido / real / pagado NO se tocan: esos
--     evitan acumuladores negativos y son una salvaguarda distinta.
--
-- REVERSIBLE: si hiciera falta volver atras, basta reaplicar los scripts
--             2026-08-27_pst_compromiso_02_funciones.sql, _03_procedimientos.sql y _04_vistas.sql.
--
-- Solo CREATE OR REPLACE: no altera tablas, no borra datos, no cambia firmas.
-- =============================================================================

BEGIN;

-- 1) fn_pst_disponible --------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_disponible(
    p_company_id BIGINT,
    p_cuenta     VARCHAR,
    p_fecha      DATE
)
RETURNS NUMERIC
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(d.valor_proyeccion, 0)
         - COALESCE(d.valor_comprometido, 0)
         - COALESCE(d.valor_real, 0)
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.company_id = d.company_id
       AND h.id_presupuesto = d.id_presupuesto
     WHERE d.company_id = p_company_id
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(p_cuenta))
       AND p_fecha BETWEEN h.fecha_inicia AND h.fecha_finaliza
     ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC
     LIMIT 1;
$$;

COMMENT ON FUNCTION public.fn_pst_disponible(BIGINT, VARCHAR, DATE) IS
    'Disponible = proyeccion - comprometido - real. NEGATIVO si la partida esta sobregirada: el truncado a 0 ocultaba la magnitud del exceso. Lectura sin lock, para el panel previo de la UI. NULL = la cuenta no tiene partida vigente.';

-- 2) vw_pst_ejecucion_presupuestaria ------------------------------------------
CREATE OR REPLACE VIEW public.vw_pst_ejecucion_presupuestaria AS
SELECT d.company_id,
       d.id_presupuesto,
       h.fecha_inicia,
       h.fecha_finaliza,
       h.estado_aprobado,
       d.con_cuenta_code,
       pc.name                                    AS cuenta_nombre,
       pc.account_type                            AS cuenta_tipo,
       pc.allows_budget                           AS cuenta_presupuestable,
       d.valor_proyeccion                         AS presupuesto,
       d.valor_comprometido                       AS comprometido,
       d.valor_real                               AS ejecutado,
       d.valor_pagado                             AS pagado,
       (d.valor_proyeccion - d.valor_comprometido - d.valor_real) AS disponible,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * d.valor_real / d.valor_proyeccion, 2) ELSE NULL END       AS pct_ejecucion,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * d.valor_comprometido / d.valor_proyeccion, 2) ELSE NULL END AS pct_compromiso,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * (d.valor_comprometido + d.valor_real) / d.valor_proyeccion, 2)
            ELSE NULL END                                                                  AS pct_utilizado
  FROM public.pst_config_presupuesto_dtl d
  JOIN public.pst_config_presupuesto_hdr h
    ON h.company_id = d.company_id
   AND h.id_presupuesto = d.id_presupuesto
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = d.company_id
        AND upper(btrim(pc.code)) = upper(btrim(d.con_cuenta_code));

COMMENT ON VIEW public.vw_pst_ejecucion_presupuestaria IS
    'Ejecucion presupuestaria por partida: presupuesto, comprometido, ejecutado, pagado y disponible. Disponible = proyeccion - comprometido - real, NEGATIVO cuando la partida esta sobregirada.';

-- 3) sp_pst_comprometer_documento ---------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_comprometer_documento(
    p_company_id       BIGINT,
    p_modulo           VARCHAR,
    p_documento_tipo   VARCHAR,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_fecha            DATE,
    p_usuario          VARCHAR,
    p_usuario_aprobo   VARCHAR,
    p_ip               VARCHAR,
    p_lineas           public.pst_linea_afectacion[]
)
RETURNS TABLE (con_cuenta_code VARCHAR, disponible NUMERIC, requerido NUMERIC, exceso NUMERIC, excedio BOOLEAN)
LANGUAGE plpgsql
AS $$
DECLARE
    v_modo           SMALLINT;
    v_exige_aprobado BOOLEAN;
    v_sin_cuenta     INTEGER;
    v_cuenta         VARCHAR(20);
    v_requerido      NUMERIC(18,4);
    v_partida        VARCHAR(10);
    v_proy           NUMERIC(18,4);
    v_comp           NUMERIC(18,4);
    v_real           NUMERIC(18,4);
    v_disp           NUMERIC(18,4);
    v_excedio        BOOLEAN;
    v_linea          public.pst_linea_afectacion;
    v_comp_id        BIGINT;
BEGIN
    SELECT c.modo, c.exige_presupuesto_aprobado
      INTO v_modo, v_exige_aprobado
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id
       AND c.modulo = 'COMPRAS_OC';

    -- Sin configuración o apagado: comportamiento idéntico al de hoy.
    IF NOT FOUND OR COALESCE(v_modo, 0) = 0 THEN
        RETURN;
    END IF;

    -- ★ IDEMPOTENCIA A NIVEL DE DOCUMENTO. Va ANTES de validar, y no después.
    -- Si el documento ya tiene compromisos vigentes, esta llamada es un reintento (doble clic,
    -- retry de HTTP, reenvío del cliente) y debe ser un no-op silencioso.
    -- Sin esta guarda, el reintento vuelve a validar el importe completo contra un disponible que
    -- YA descuenta ese mismo compromiso, y falla con un engañoso "excede el presupuesto".
    -- El ON CONFLICT del INSERT no alcanza: la validación ocurre antes de llegar a él.
    -- (Modificar una O/C ya aprobada NO pasa por aquí: eso es sp_pst_ajustar_compromiso.)
    IF EXISTS (
        SELECT 1 FROM public.pst_compromiso c
         WHERE c.company_id = p_company_id
           AND c.modulo = p_modulo
           AND c.documento_tipo = p_documento_tipo
           AND c.documento_id = p_documento_id
           AND c.estado IN (1, 2)
    ) THEN
        RETURN;
    END IF;

    -- Renglones sin cuenta resoluble. No se ignoran en silencio: comprometerían de menos.
    SELECT count(*) INTO v_sin_cuenta
      FROM unnest(p_lineas) l
     WHERE COALESCE(l.monto, 0) > 0
       AND (l.con_cuenta_code IS NULL OR btrim(l.con_cuenta_code) = '');

    IF v_sin_cuenta > 0 THEN
        IF v_modo = 2 THEN
            RAISE EXCEPTION 'Hay % renglón(es) sin cuenta presupuestaria. Configure la cuenta del tipo de artículo o capture la cuenta en el renglón antes de aprobar.', v_sin_cuenta
                USING ERRCODE = 'P0001';
        END IF;
        con_cuenta_code := NULL; disponible := NULL; requerido := NULL; exceso := NULL; excedio := TRUE;
        RETURN NEXT;
    END IF;

    FOR v_cuenta, v_requerido IN
        SELECT upper(btrim(l.con_cuenta_code)), SUM(l.monto)
          FROM unnest(p_lineas) l
         WHERE COALESCE(l.monto, 0) > 0
           AND l.con_cuenta_code IS NOT NULL
           AND btrim(l.con_cuenta_code) <> ''
         GROUP BY upper(btrim(l.con_cuenta_code))
         ORDER BY 1                       -- ★ orden determinístico: anti-deadlock
    LOOP
        -- Cuenta no presupuestable: se ignora en silencio (mismo criterio que el compromiso a
        -- proveedor). Es lo que permite al contador definir el alcance sin tocar código.
        IF NOT EXISTS (
            SELECT 1 FROM public.con_plan_cuentas p
             WHERE p.company_id = p_company_id
               AND upper(btrim(p.code)) = v_cuenta
               AND p.allows_budget
        ) THEN
            CONTINUE;
        END IF;

        SELECT r.id_presupuesto INTO v_partida
          FROM public.fn_pst_resolver_partida(p_company_id, v_cuenta, p_fecha, COALESCE(v_exige_aprobado, TRUE)) r;

        IF v_partida IS NULL THEN
            IF v_modo = 2 THEN
                RAISE EXCEPTION 'La cuenta % está marcada como presupuestable pero no tiene un presupuesto vigente% a la fecha %. Presupueste la cuenta o quítele la marca.',
                    v_cuenta,
                    CASE WHEN COALESCE(v_exige_aprobado, TRUE) THEN ' y aprobado' ELSE '' END,
                    to_char(p_fecha, 'YYYY-MM-DD')
                    USING ERRCODE = 'P0001';
            END IF;
            con_cuenta_code := v_cuenta; disponible := NULL; requerido := v_requerido;
            exceso := NULL; excedio := TRUE;
            RETURN NEXT;
            CONTINUE;
        END IF;

        -- Lectura BAJO LOCK para validar. El lock se mantiene hasta el COMMIT de la transacción
        -- del documento, así que la validación y la aplicación ven el mismo saldo.
        SELECT COALESCE(d.valor_proyeccion, 0), COALESCE(d.valor_comprometido, 0), COALESCE(d.valor_real, 0)
          INTO v_proy, v_comp, v_real
          FROM public.pst_config_presupuesto_dtl d
         WHERE d.company_id = p_company_id
           AND d.id_presupuesto = v_partida
           AND d.con_cuenta_code = v_cuenta
         FOR UPDATE;

        v_disp := v_proy - v_comp - v_real;
        v_excedio := v_requerido > v_disp;

        IF v_excedio THEN
            IF v_modo = 2 THEN
                RAISE EXCEPTION 'La orden excede el presupuesto disponible para la cuenta %. Disponible: %. Requerido: %. Faltan: %.',
                    v_cuenta,
                    to_char(v_disp, 'FM999999999990.00'),
                    to_char(v_requerido, 'FM999999999990.00'),
                    to_char(v_requerido - v_disp, 'FM999999999990.00')
                    USING ERRCODE = 'P0001';
            END IF;
            con_cuenta_code := v_cuenta; disponible := v_disp; requerido := v_requerido;
            exceso := v_requerido - v_disp; excedio := TRUE;
            RETURN NEXT;
        END IF;

        -- Aplicación, renglón por renglón (el compromiso vive al nivel del renglón para poder
        -- liberar con exactitud lo pendiente de cada uno).
        FOR v_linea IN
            SELECT l.* FROM unnest(p_lineas) l
             WHERE COALESCE(l.monto, 0) > 0
               AND upper(btrim(l.con_cuenta_code)) = v_cuenta
             ORDER BY l.documento_detalle_id
        LOOP
            INSERT INTO public.pst_compromiso (
                company_id, id_presupuesto, con_cuenta_code, centro_costo_id,
                modulo, documento_tipo, documento_id, documento_numero, documento_detalle_id,
                fecha, monto_comprometido, estado, usuariocreacion)
            VALUES (
                p_company_id, v_partida, v_cuenta, v_linea.centro_costo_id,
                p_modulo, p_documento_tipo, p_documento_id, p_documento_numero, v_linea.documento_detalle_id,
                p_fecha, v_linea.monto, 1, p_usuario)
            ON CONFLICT ON CONSTRAINT uq_pst_compromiso_documento DO NOTHING
            RETURNING id INTO v_comp_id;

            -- Duplicado: este renglón ya estaba comprometido (reintento). No se vuelve a aplicar.
            IF v_comp_id IS NULL THEN
                CONTINUE;
            END IF;

            PERFORM public.fn_pst_aplicar_movimiento(
                p_company_id, v_partida, v_cuenta, v_linea.centro_costo_id,
                1::SMALLINT, p_modulo, p_documento_tipo, p_documento_id, p_documento_numero,
                v_linea.documento_detalle_id, p_documento_id, v_comp_id,
                p_fecha, v_linea.monto,
                v_linea.monto, 0, 0, 0,
                v_excedio, NULL, p_usuario, p_usuario_aprobo, p_ip);
        END LOOP;
    END LOOP;

    RETURN;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_comprometer_documento IS
    'Aprobación de la O/C: valida disponible POR PARTIDA (consolidando renglones) y compromete. Modo 0 no hace nada, 1 avisa, 2 rechaza. Devuelve los avisos.';

-- 4) sp_pst_ajustar_compromiso ------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_ajustar_compromiso(
    p_company_id       BIGINT,
    p_modulo           VARCHAR,
    p_documento_tipo   VARCHAR,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_motivo           VARCHAR,
    p_usuario          VARCHAR,
    p_ip               VARCHAR,
    p_lineas           public.pst_linea_afectacion[]
)
RETURNS TABLE (con_cuenta_code VARCHAR, disponible NUMERIC, requerido NUMERIC, exceso NUMERIC, excedio BOOLEAN)
LANGUAGE plpgsql
AS $$
DECLARE
    v_modo      SMALLINT;
    v_cuenta    VARCHAR(20);
    v_nuevo     NUMERIC(18,4);
    v_efectivo  NUMERIC(18,4);
    v_devengado NUMERIC(18,4);
    v_delta     NUMERIC(18,4);
    v_partida   VARCHAR(10);
    v_fecha     DATE;
    v_centro    BIGINT;
    v_comp_id   BIGINT;
    v_disp      NUMERIC(18,4);
    v_proy      NUMERIC(18,4);
    v_comp      NUMERIC(18,4);
    v_real      NUMERIC(18,4);
BEGIN
    SELECT c.modo INTO v_modo
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id AND c.modulo = 'COMPRAS_OC';

    IF NOT FOUND OR COALESCE(v_modo, 0) = 0 THEN
        RETURN;
    END IF;

    -- Recorre la UNIÓN de cuentas: las de la nueva distribución y las que ya estaban
    -- comprometidas (para poder liberar una partida que desaparece del documento).
    FOR v_cuenta IN
        SELECT cuenta FROM (
            SELECT upper(btrim(l.con_cuenta_code)) AS cuenta
              FROM unnest(p_lineas) l
             WHERE COALESCE(l.monto, 0) > 0 AND l.con_cuenta_code IS NOT NULL
            UNION
            SELECT c.con_cuenta_code
              FROM public.pst_compromiso c
             WHERE c.company_id = p_company_id AND c.modulo = p_modulo
               AND c.documento_tipo = p_documento_tipo AND c.documento_id = p_documento_id
               AND c.estado = 1
        ) u
        ORDER BY cuenta                       -- ★ anti-deadlock
    LOOP
        SELECT COALESCE(SUM(l.monto), 0) INTO v_nuevo
          FROM unnest(p_lineas) l
         WHERE COALESCE(l.monto, 0) > 0
           AND upper(btrim(l.con_cuenta_code)) = v_cuenta;

        SELECT COALESCE(SUM(c.monto_comprometido - c.monto_liberado), 0),
               COALESCE(SUM(c.monto_devengado), 0),
               MIN(c.id_presupuesto), MIN(c.fecha), MIN(c.centro_costo_id), MIN(c.id)
          INTO v_efectivo, v_devengado, v_partida, v_fecha, v_centro, v_comp_id
          FROM public.pst_compromiso c
         WHERE c.company_id = p_company_id AND c.modulo = p_modulo
           AND c.documento_tipo = p_documento_tipo AND c.documento_id = p_documento_id
           AND c.con_cuenta_code = v_cuenta AND c.estado = 1;

        IF v_partida IS NULL THEN
            -- Partida nueva en la modificación: es un compromiso nuevo, no un ajuste.
            -- Se delega en sp_pst_comprometer_documento desde el servicio.
            CONTINUE;
        END IF;

        IF v_nuevo < v_devengado THEN
            RAISE EXCEPTION 'No se puede reducir el compromiso de la cuenta % a % : ya se recibieron %. Anule primero las facturas correspondientes.',
                v_cuenta,
                to_char(v_nuevo, 'FM999999999990.00'),
                to_char(v_devengado, 'FM999999999990.00')
                USING ERRCODE = 'P0001';
        END IF;

        v_delta := v_nuevo - v_efectivo;
        CONTINUE WHEN v_delta = 0;

        IF v_delta > 0 THEN
            SELECT COALESCE(d.valor_proyeccion, 0), COALESCE(d.valor_comprometido, 0), COALESCE(d.valor_real, 0)
              INTO v_proy, v_comp, v_real
              FROM public.pst_config_presupuesto_dtl d
             WHERE d.company_id = p_company_id AND d.id_presupuesto = v_partida
               AND d.con_cuenta_code = v_cuenta
             FOR UPDATE;

            v_disp := v_proy - v_comp - v_real;

            IF v_delta > v_disp THEN
                IF v_modo = 2 THEN
                    RAISE EXCEPTION 'El aumento de la orden excede el presupuesto disponible para la cuenta %. Disponible: %. Aumento requerido: %. Faltan: %.',
                        v_cuenta,
                        to_char(v_disp, 'FM999999999990.00'),
                        to_char(v_delta, 'FM999999999990.00'),
                        to_char(v_delta - v_disp, 'FM999999999990.00')
                        USING ERRCODE = 'P0001';
                END IF;
                con_cuenta_code := v_cuenta; disponible := v_disp; requerido := v_delta;
                exceso := v_delta - v_disp; excedio := TRUE;
                RETURN NEXT;
            END IF;

            UPDATE public.pst_compromiso
               SET monto_comprometido = monto_comprometido + v_delta,
                   usuariomodificacion = p_usuario,
                   fechamodificacion = (now() AT TIME ZONE 'utc')
             WHERE id = v_comp_id;

            PERFORM public.fn_pst_aplicar_movimiento(
                p_company_id, v_partida, v_cuenta, v_centro,
                12::SMALLINT, p_modulo, p_documento_tipo, p_documento_id, p_documento_numero,
                NULL, p_documento_id, v_comp_id, v_fecha, v_delta,
                v_delta, 0, 0, 0,
                FALSE, p_motivo, p_usuario, NULL, p_ip);
        ELSE
            UPDATE public.pst_compromiso
               SET monto_liberado = monto_liberado + ABS(v_delta),
                   usuariomodificacion = p_usuario,
                   fechamodificacion = (now() AT TIME ZONE 'utc')
             WHERE id = v_comp_id;

            PERFORM public.fn_pst_aplicar_movimiento(
                p_company_id, v_partida, v_cuenta, v_centro,
                13::SMALLINT, p_modulo, p_documento_tipo, p_documento_id, p_documento_numero,
                NULL, p_documento_id, v_comp_id, v_fecha, ABS(v_delta),
                v_delta, 0, 0, 0,
                FALSE, p_motivo, p_usuario, NULL, p_ip);
        END IF;
    END LOOP;

    RETURN;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_ajustar_compromiso IS
    'Modificación de una O/C aprobada: compromete o libera SOLO el delta por partida. Rechaza reducir por debajo de lo ya devengado.';

-- 5) sp_pst_devengar_documento ------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_devengar_documento(
    p_company_id       BIGINT,
    p_documento_tipo   VARCHAR,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_orden_compra_id  BIGINT,
    p_fecha            DATE,
    p_usuario          VARCHAR,
    p_ip               VARCHAR,
    p_lineas           public.pst_linea_afectacion[]
)
RETURNS TABLE (con_cuenta_code VARCHAR, disponible NUMERIC, requerido NUMERIC, exceso NUMERIC, excedio BOOLEAN)
LANGUAGE plpgsql
AS $$
DECLARE
    v_modo       SMALLINT;
    v_tolerancia NUMERIC(5,2);
    v_sin_oc     SMALLINT;
    v_cuenta     VARCHAR(20);
    v_monto      NUMERIC(18,4);
    v_detalle    BIGINT;
    v_centro     BIGINT;
    v_restante   NUMERIC(18,4);
    v_consumido  NUMERIC(18,4);
    v_c          RECORD;
    v_saldo      NUMERIC(18,4);
    v_aplica     NUMERIC(18,4);
    v_ids        BIGINT[];
    v_montos     NUMERIC[];
    v_partida    VARCHAR(10);
    v_mov        BIGINT;
    v_i          INTEGER;
    v_proy       NUMERIC(18,4);
    v_comp       NUMERIC(18,4);
    v_real       NUMERIC(18,4);
    v_disp       NUMERIC(18,4);
    v_margen     NUMERIC(18,4);
BEGIN
    SELECT c.modo, c.tolerancia_pct, c.permite_devengo_sin_oc
      INTO v_modo, v_tolerancia, v_sin_oc
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id AND c.modulo = 'COMPRAS_FACTURA';

    IF NOT FOUND OR COALESCE(v_modo, 0) = 0 THEN
        RETURN;
    END IF;

    IF p_orden_compra_id IS NULL AND COALESCE(v_sin_oc, 1) = 0 THEN
        RAISE EXCEPTION 'La configuración del presupuesto no permite registrar compras sin orden de compra. Genere y apruebe la O/C antes de facturar.'
            USING ERRCODE = 'P0001';
    END IF;

    FOR v_cuenta, v_monto, v_detalle, v_centro IN
        SELECT upper(btrim(l.con_cuenta_code)), SUM(l.monto),
               MIN(l.documento_detalle_id), MIN(l.centro_costo_id)
          FROM unnest(p_lineas) l
         WHERE COALESCE(l.monto, 0) > 0
           AND l.con_cuenta_code IS NOT NULL
           AND btrim(l.con_cuenta_code) <> ''
         GROUP BY upper(btrim(l.con_cuenta_code))
         ORDER BY 1                        -- ★ anti-deadlock
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM public.con_plan_cuentas p
             WHERE p.company_id = p_company_id
               AND upper(btrim(p.code)) = v_cuenta
               AND p.allows_budget
        ) THEN
            CONTINUE;
        END IF;

        v_restante  := v_monto;
        v_consumido := 0;
        v_ids       := ARRAY[]::BIGINT[];
        v_montos    := ARRAY[]::NUMERIC[];
        v_partida   := NULL;

        -- ---- a) Consumir el compromiso de la O/C (si la hay) ----
        IF p_orden_compra_id IS NOT NULL THEN
            FOR v_c IN
                SELECT c.* FROM public.pst_compromiso c
                 WHERE c.company_id = p_company_id
                   AND c.documento_tipo = 'ORDEN_COMPRA'
                   AND c.documento_id = p_orden_compra_id
                   AND c.con_cuenta_code = v_cuenta
                   AND c.estado = 1
                 ORDER BY c.id                       -- FIFO por renglón
                 FOR UPDATE
            LOOP
                EXIT WHEN v_restante <= 0;
                v_saldo := v_c.monto_comprometido - v_c.monto_devengado - v_c.monto_liberado;
                CONTINUE WHEN v_saldo <= 0;

                v_aplica := LEAST(v_saldo, v_restante);

                UPDATE public.pst_compromiso
                   SET monto_devengado = monto_devengado + v_aplica,
                       estado = CASE WHEN monto_devengado + v_aplica + monto_liberado >= monto_comprometido
                                     THEN 2 ELSE 1 END,
                       usuariomodificacion = p_usuario,
                       fechamodificacion = (now() AT TIME ZONE 'utc')
                 WHERE id = v_c.id;

                v_ids     := v_ids     || v_c.id;
                v_montos  := v_montos  || v_aplica;
                v_partida := v_c.id_presupuesto;
                v_consumido := v_consumido + v_aplica;
                v_restante  := v_restante  - v_aplica;
            END LOOP;

            -- UN SOLO movimiento por cuenta (no uno por compromiso): el índice de idempotencia
            -- lleva la cuenta, no el compromiso. Las aplicaciones individuales van aparte.
            IF v_consumido > 0 THEN
                v_mov := public.fn_pst_aplicar_movimiento(
                    p_company_id, v_partida, v_cuenta, v_centro,
                    3::SMALLINT, 'COMPRAS', p_documento_tipo, p_documento_id, p_documento_numero,
                    v_detalle, p_orden_compra_id, NULL, p_fecha, v_consumido,
                    -v_consumido, v_consumido, 0, 0,
                    FALSE, NULL, p_usuario, NULL, p_ip);

                IF v_mov IS NOT NULL THEN
                    FOR v_i IN 1 .. array_length(v_ids, 1) LOOP
                        INSERT INTO public.pst_compromiso_aplicacion (
                            company_id, compromiso_id, movimiento_id, tipo,
                            documento_tipo, documento_id, documento_numero, monto, usuario)
                        VALUES (p_company_id, v_ids[v_i], v_mov, 1,
                                p_documento_tipo, p_documento_id, p_documento_numero, v_montos[v_i], p_usuario);
                    END LOOP;
                END IF;
            END IF;
        END IF;

        -- ---- b) Lo que no cubrió el compromiso: devengo DIRECTO contra el disponible ----
        CONTINUE WHEN v_restante <= 0;

        SELECT r.id_presupuesto INTO v_partida
          FROM public.fn_pst_resolver_partida(p_company_id, v_cuenta, p_fecha, TRUE) r;

        IF v_partida IS NULL THEN
            IF v_modo = 2 THEN
                RAISE EXCEPTION 'La cuenta % está marcada como presupuestable pero no tiene presupuesto vigente y aprobado a la fecha %.',
                    v_cuenta, to_char(p_fecha, 'YYYY-MM-DD') USING ERRCODE = 'P0001';
            END IF;
            con_cuenta_code := v_cuenta; disponible := NULL; requerido := v_restante;
            exceso := NULL; excedio := TRUE;
            RETURN NEXT;
            CONTINUE;
        END IF;

        SELECT COALESCE(d.valor_proyeccion, 0), COALESCE(d.valor_comprometido, 0), COALESCE(d.valor_real, 0)
          INTO v_proy, v_comp, v_real
          FROM public.pst_config_presupuesto_dtl d
         WHERE d.company_id = p_company_id AND d.id_presupuesto = v_partida
           AND d.con_cuenta_code = v_cuenta
         FOR UPDATE;

        v_disp := v_proy - v_comp - v_real;
        -- La tolerancia solo exime el exceso sobre un compromiso existente, no la compra directa.
        v_margen := CASE WHEN v_consumido > 0
                         THEN round(v_consumido * COALESCE(v_tolerancia, 0) / 100.0, 4)
                         ELSE 0 END;

        IF v_restante > v_disp + v_margen THEN
            IF v_modo = 2 THEN
                RAISE EXCEPTION 'La factura excede el presupuesto disponible para la cuenta %. Disponible: %. Requerido: %. Faltan: %.',
                    v_cuenta,
                    to_char(v_disp, 'FM999999999990.00'),
                    to_char(v_restante, 'FM999999999990.00'),
                    to_char(v_restante - v_disp, 'FM999999999990.00')
                    USING ERRCODE = 'P0001';
            END IF;
            con_cuenta_code := v_cuenta; disponible := v_disp; requerido := v_restante;
            exceso := v_restante - v_disp; excedio := TRUE;
            RETURN NEXT;
        END IF;

        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_partida, v_cuenta, v_centro,
            5::SMALLINT, 'COMPRAS', p_documento_tipo, p_documento_id, p_documento_numero,
            v_detalle, p_orden_compra_id, NULL, p_fecha, v_restante,
            0, v_restante, 0, 0,
            v_restante > v_disp + v_margen, NULL, p_usuario, NULL, p_ip);
    END LOOP;

    RETURN;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_devengar_documento IS
    'Factura de compra: consume el compromiso de la O/C (disponible sin cambio) y lo que sobre lo devenga directo contra el disponible. Sin O/C, según permite_devengo_sin_oc.';

COMMIT;

-- =============================================================================
-- VERIFICACION (ejecutar despues de aplicar)
-- =============================================================================
-- a) Las partidas sobregiradas ya reportan el negativo real, no 0:
-- SELECT con_cuenta_code, presupuesto, comprometido, ejecutado, disponible, pct_utilizado
--   FROM public.vw_pst_ejecucion_presupuestaria
--  WHERE company_id = 2 AND disponible < 0
--  ORDER BY disponible;
--
-- b) La funcion coincide con la vista para la misma cuenta y fecha:
-- SELECT public.fn_pst_disponible(2, '11401010101', CURRENT_DATE) AS por_funcion,
--        (SELECT disponible FROM public.vw_pst_ejecucion_presupuestaria
--          WHERE company_id = 2 AND con_cuenta_code = '11401010101'
--            AND CURRENT_DATE BETWEEN fecha_inicia AND fecha_finaliza) AS por_vista;
--
-- c) Ningun objeto quedo con el truncado (debe devolver 0 filas):
-- SELECT p.proname
--   FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--  WHERE n.nspname = 'public'
--    AND p.proname IN ('fn_pst_disponible','sp_pst_comprometer_documento',
--                      'sp_pst_ajustar_compromiso','sp_pst_devengar_documento')
--    AND pg_get_functiondef(p.oid) LIKE '%GREATEST(v_proy - v_comp - v_real, 0)%';
-- =============================================================================
