-- =============================================================================
-- Control presupuestario con COMPROMISO en la O/C — procedimientos de negocio
-- Fecha: 2026-08-27
-- Fase F1 (3 de 4). Requiere: los scripts 01 (estructura) y 02 (funciones)
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ ESTÁ EN LA BASE Y NO EN C#
--   1) Concurrencia. El SELECT ... FOR UPDATE sobre la fila de la partida es la ÚNICA defensa
--      real contra la doble aprobación. El mecanismo actual del compromiso a proveedor
--      (OrdenesPagoDirectoService) no toma ningún lock: se apoya en IsolationLevel.Serializable
--      y su 40001 no se maneja, así que bajo concurrencia devuelve un 500 crudo.
--   2) La regla del repositorio: .github/skills/hodsoft-sin-linq — todo acceso a datos va por SP,
--      función o vista. Código nuevo con cero LINQ.
--   3) Auditoría: el SQL queda versionado y es lo que el contador va a querer leer.
--
-- CONVENCIÓN DE NOMBRE: prefijo sp_ pero implementados como FUNCTION (no PROCEDURE), igual que
-- el sp_pst_aplicar_partida_presupuesto ya existente. Se invocan desde C# con Dapper sobre la
-- conexión y transacción del SiadDbContext, para quedar DENTRO de la transacción del documento.
--
-- LOS SIETE
--   sp_pst_comprometer_documento  -> aprobación de la O/C: valida y compromete
--   sp_pst_liberar_compromiso     -> anulación / cancelación / cierre: libera el SALDO
--   sp_pst_ajustar_compromiso     -> modificación de una O/C aprobada: solo el delta
--   sp_pst_devengar_documento     -> factura: comprometido (-) / ejecutado (+)
--   sp_pst_revertir_devengo       -> anulación de la factura
--   sp_pst_registrar_pago         -> abono: pagado (+), NO altera el disponible
--   sp_pst_revertir_pago          -> anulación del abono
--
-- ERRORES: todos se lanzan con ERRCODE P0001 y un mensaje pensado para el usuario final. El
-- servicio C# los traduce a InvalidOperationException. El código de cuenta va SIN formato: lo
-- formatea IAccountFormatService del lado del portal.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- HELPER — fn_pst_aplicar_movimiento
-- Bloquea la partida, aplica los deltas y escribe el asiento del kardex, todo bajo el mismo lock.
--
-- El INSERT del movimiento va ANTES del UPDATE de la partida a propósito: si el movimiento choca
-- contra el índice de idempotencia (reintento, doble clic, retry de HTTP), la función devuelve
-- NULL y NO aplica el delta. Al revés, un reintento sumaría dos veces.
--
-- Devuelve el id del movimiento, o NULL si era un duplicado.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_aplicar_movimiento(
    p_company_id           BIGINT,
    p_id_presupuesto       VARCHAR,
    p_cuenta               VARCHAR,
    p_centro_costo_id      BIGINT,
    p_tipo_movimiento      SMALLINT,
    p_modulo               VARCHAR,
    p_documento_tipo       VARCHAR,
    p_documento_id         BIGINT,
    p_documento_numero     VARCHAR,
    p_documento_detalle_id BIGINT,
    p_orden_compra_id      BIGINT,
    p_compromiso_id        BIGINT,
    p_fecha                DATE,
    p_monto                NUMERIC,
    p_delta_comprometido   NUMERIC,
    p_delta_real           NUMERIC,
    p_delta_pagado         NUMERIC,
    p_delta_proyeccion     NUMERIC,
    p_excedio              BOOLEAN,
    p_observacion          VARCHAR,
    p_usuario              VARCHAR,
    p_usuario_aprobo       VARCHAR,
    p_ip                   VARCHAR
)
RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    v_proy_ant  NUMERIC(18,4);
    v_comp_ant  NUMERIC(18,4);
    v_real_ant  NUMERIC(18,4);
    v_pag_ant   NUMERIC(18,4);
    v_disp_ant  NUMERIC(18,4);
    v_proy_pos  NUMERIC(18,4);
    v_comp_pos  NUMERIC(18,4);
    v_real_pos  NUMERIC(18,4);
    v_pag_pos   NUMERIC(18,4);
    v_disp_pos  NUMERIC(18,4);
    v_mov_id    BIGINT;
BEGIN
    -- ★ EL LOCK. Aquí se serializan dos aprobaciones simultáneas contra la misma partida.
    SELECT COALESCE(d.valor_proyeccion, 0), COALESCE(d.valor_comprometido, 0),
           COALESCE(d.valor_real, 0),       COALESCE(d.valor_pagado, 0)
      INTO v_proy_ant, v_comp_ant, v_real_ant, v_pag_ant
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto
       AND d.con_cuenta_code = p_cuenta
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No se encontró la partida presupuestaria % del presupuesto %.', p_cuenta, p_id_presupuesto
            USING ERRCODE = 'P0001';
    END IF;

    v_disp_ant := GREATEST(v_proy_ant - v_comp_ant - v_real_ant, 0);

    v_proy_pos := GREATEST(v_proy_ant + COALESCE(p_delta_proyeccion, 0), 0);
    v_comp_pos := GREATEST(v_comp_ant + COALESCE(p_delta_comprometido, 0), 0);
    v_real_pos := GREATEST(v_real_ant + COALESCE(p_delta_real, 0), 0);
    v_pag_pos  := GREATEST(v_pag_ant  + COALESCE(p_delta_pagado, 0), 0);
    v_disp_pos := GREATEST(v_proy_pos - v_comp_pos - v_real_pos, 0);

    INSERT INTO public.pst_movimiento (
        company_id, id_presupuesto, con_cuenta_code, centro_costo_id,
        tipo_movimiento, modulo, documento_tipo, documento_id, documento_numero,
        documento_detalle_id, orden_compra_id, compromiso_id, fecha, monto,
        proyeccion_anterior,  comprometido_anterior,  ejecutado_anterior,  disponible_anterior,
        proyeccion_posterior, comprometido_posterior, ejecutado_posterior, disponible_posterior,
        excedio, observacion, usuario, usuario_aprobo, ip)
    VALUES (
        p_company_id, p_id_presupuesto, p_cuenta, p_centro_costo_id,
        p_tipo_movimiento, p_modulo, p_documento_tipo, p_documento_id, p_documento_numero,
        p_documento_detalle_id, p_orden_compra_id, p_compromiso_id, p_fecha, ABS(COALESCE(p_monto, 0)),
        v_proy_ant, v_comp_ant, v_real_ant, v_disp_ant,
        v_proy_pos, v_comp_pos, v_real_pos, v_disp_pos,
        COALESCE(p_excedio, FALSE), p_observacion, COALESCE(p_usuario, 'sistema'), p_usuario_aprobo, p_ip)
    ON CONFLICT DO NOTHING
    RETURNING id INTO v_mov_id;

    -- Duplicado: el movimiento ya estaba registrado. No se aplica el delta.
    IF v_mov_id IS NULL THEN
        RETURN NULL;
    END IF;

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_proyeccion   = v_proy_pos,
           valor_comprometido = v_comp_pos,
           valor_real         = v_real_pos,
           valor_pagado       = v_pag_pos,
           valor_disponible   = v_disp_pos
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto
       AND d.con_cuenta_code = p_cuenta;

    PERFORM public.fn_pst_recalcular_cabecera(p_company_id, p_id_presupuesto);

    RETURN v_mov_id;
END;
$$;

COMMENT ON FUNCTION public.fn_pst_aplicar_movimiento IS
    'Interno: bloquea la partida (FOR UPDATE), escribe el movimiento del kardex y aplica los deltas. Devuelve NULL si el movimiento era duplicado (y entonces NO aplica nada).';

-- -----------------------------------------------------------------------------
-- 1) sp_pst_comprometer_documento — APROBACIÓN DE LA O/C
--
-- Secuencia:
--   1. Leer el modo. Si es 0 (apagado) -> RETURN sin hacer nada.
--   2. CONSOLIDAR las líneas por cuenta: una O/C puede traer varios renglones contra la misma
--      partida y hay que validarlos JUNTOS, no uno por uno (si no, dos renglones de 60 pasarían
--      contra un disponible de 100).
--   3. Recorrer las cuentas ORDENADAS POR CÓDIGO. El orden determinístico es lo que evita que
--      dos aprobaciones multi-partida simultáneas se abracen en un deadlock.
--   4. Por cuenta: descartar si no es presupuestable, resolver la partida, bloquear, validar,
--      y recorrer sus renglones creando compromiso + movimiento.
-- -----------------------------------------------------------------------------
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

        v_disp := GREATEST(v_proy - v_comp - v_real, 0);
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

-- -----------------------------------------------------------------------------
-- 2) sp_pst_liberar_compromiso — ANULAR / CANCELAR / CERRAR
--
-- Libera el SALDO pendiente, no el total: una O/C de 100,000 con 60,000 ya recibidos libera
-- 40,000. Es exactamente el caso §6 del requerimiento, y sale gratis porque el saldo ya descuenta
-- lo devengado.
--
-- NO exige presupuesto aprobado ni vigente: se puede cancelar una O/C contra un presupuesto ya
-- cerrado. Devolver dinero nunca debe estar bloqueado.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_liberar_compromiso(
    p_company_id     BIGINT,
    p_modulo         VARCHAR,
    p_documento_tipo VARCHAR,
    p_documento_id   BIGINT,
    p_motivo         VARCHAR,
    p_usuario        VARCHAR,
    p_ip             VARCHAR
)
RETURNS NUMERIC
LANGUAGE plpgsql
AS $$
DECLARE
    v_c        RECORD;
    v_saldo    NUMERIC(18,4);
    v_liberado NUMERIC(18,4) := 0;
BEGIN
    IF p_motivo IS NULL OR btrim(p_motivo) = '' THEN
        RAISE EXCEPTION 'El motivo es obligatorio para liberar un compromiso presupuestario.'
            USING ERRCODE = 'P0001';
    END IF;

    FOR v_c IN
        SELECT c.*
          FROM public.pst_compromiso c
         WHERE c.company_id = p_company_id
           AND c.modulo = p_modulo
           AND c.documento_tipo = p_documento_tipo
           AND c.documento_id = p_documento_id
           AND c.estado = 1
         ORDER BY c.con_cuenta_code, c.id     -- orden determinístico
         FOR UPDATE
    LOOP
        v_saldo := v_c.monto_comprometido - v_c.monto_devengado - v_c.monto_liberado;
        IF v_saldo <= 0 THEN
            -- Ya consumido por completo: se cierra sin movimiento.
            UPDATE public.pst_compromiso SET estado = 2 WHERE id = v_c.id;
            CONTINUE;
        END IF;

        UPDATE public.pst_compromiso
           SET monto_liberado = monto_liberado + v_saldo,
               estado = 9,
               usuariomodificacion = p_usuario,
               fechamodificacion = (now() AT TIME ZONE 'utc')
         WHERE id = v_c.id;

        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_c.id_presupuesto, v_c.con_cuenta_code, v_c.centro_costo_id,
            2::SMALLINT, p_modulo, p_documento_tipo, p_documento_id, v_c.documento_numero,
            v_c.documento_detalle_id, v_c.documento_id, v_c.id,
            v_c.fecha,                       -- ★ la fecha ORIGINAL: devuelve al mismo presupuesto
            v_saldo,
            -v_saldo, 0, 0, 0,
            FALSE, p_motivo, p_usuario, NULL, p_ip);

        v_liberado := v_liberado + v_saldo;
    END LOOP;

    RETURN v_liberado;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_liberar_compromiso IS
    'Libera el saldo pendiente de los compromisos vigentes de un documento (anular/cancelar/cerrar). Usa la fecha ORIGINAL del compromiso. Devuelve el total liberado.';

-- -----------------------------------------------------------------------------
-- 3) sp_pst_ajustar_compromiso — MODIFICAR UNA O/C YA APROBADA
--
--   100,000 -> 130,000  compromete solo 30,000 (y valida disponible por esos 30,000)
--   100,000 ->  80,000  libera 20,000
--   100,000 ->  50,000 con 90,000 ya recibidos -> RECHAZA
-- -----------------------------------------------------------------------------
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

            v_disp := GREATEST(v_proy - v_comp - v_real, 0);

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

-- -----------------------------------------------------------------------------
-- 4) sp_pst_devengar_documento — LA FACTURA
--
-- Con O/C  : consume el compromiso existente. Comprometido (-), ejecutado (+), disponible IGUAL.
--            Es la regla de oro del modelo y lo que evita el doble conteo O/C <-> factura.
-- Sin O/C  : compra directa. Consume disponible directamente (tipo 5), según
--            permite_devengo_sin_oc. Cierra el hueco de que orden_compra_id sea nullable.
-- Excedente: si la factura viene por más que el compromiso (variación de precio, flete), el
--            sobrante se valida contra disponible con tolerancia_pct como margen exento.
-- -----------------------------------------------------------------------------
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

        v_disp := GREATEST(v_proy - v_comp - v_real, 0);
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

-- -----------------------------------------------------------------------------
-- 5) sp_pst_revertir_devengo — ANULACIÓN DE LA FACTURA
--
-- Devuelve el ejecutado y, SI LA O/C SIGUE ABIERTA, restituye el compromiso para poder volver a
-- recibir. Si la O/C ya está Cerrada, Cancelada o Anulada, el importe va al disponible.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_revertir_devengo(
    p_company_id     BIGINT,
    p_documento_tipo VARCHAR,
    p_documento_id   BIGINT,
    p_motivo         VARCHAR,
    p_usuario        VARCHAR,
    p_ip             VARCHAR
)
RETURNS NUMERIC
LANGUAGE plpgsql
AS $$
DECLARE
    v_m         RECORD;
    v_a         RECORD;
    v_revertido NUMERIC(18,4) := 0;
    v_oc_abierta BOOLEAN;
    v_tipo      SMALLINT;
BEGIN
    FOR v_m IN
        SELECT m.* FROM public.pst_movimiento m
         WHERE m.company_id = p_company_id
           AND m.documento_tipo = p_documento_tipo
           AND m.documento_id = p_documento_id
           AND m.tipo_movimiento IN (3, 5)
           AND m.estado = 1
         ORDER BY m.con_cuenta_code, m.id
    LOOP
        v_oc_abierta := FALSE;
        IF v_m.orden_compra_id IS NOT NULL THEN
            SELECT (o.estado IN (2, 3)) INTO v_oc_abierta
              FROM public.alm_orden_compra o
             WHERE o.company_id = p_company_id AND o.id = v_m.orden_compra_id;
        END IF;
        v_oc_abierta := COALESCE(v_oc_abierta, FALSE);

        v_tipo := CASE WHEN v_m.tipo_movimiento = 3 THEN 4 ELSE 6 END;

        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_m.id_presupuesto, v_m.con_cuenta_code, v_m.centro_costo_id,
            v_tipo, v_m.modulo, p_documento_tipo, p_documento_id, v_m.documento_numero,
            v_m.documento_detalle_id, v_m.orden_compra_id, NULL,
            v_m.fecha,                       -- ★ la fecha ORIGINAL del devengo
            v_m.monto,
            CASE WHEN v_oc_abierta THEN v_m.monto ELSE 0 END,   -- restituye el compromiso o no
            -v_m.monto, 0, 0,
            FALSE, p_motivo, p_usuario, NULL, p_ip);

        -- Devolver el consumo a los compromisos que lo recibieron.
        IF v_oc_abierta THEN
            FOR v_a IN
                SELECT a.* FROM public.pst_compromiso_aplicacion a
                 WHERE a.company_id = p_company_id
                   AND a.movimiento_id = v_m.id
                   AND a.tipo = 1
                 ORDER BY a.id
            LOOP
                UPDATE public.pst_compromiso
                   SET monto_devengado = GREATEST(monto_devengado - v_a.monto, 0),
                       estado = 1,
                       usuariomodificacion = p_usuario,
                       fechamodificacion = (now() AT TIME ZONE 'utc')
                 WHERE id = v_a.compromiso_id;
            END LOOP;
        END IF;

        UPDATE public.pst_movimiento SET estado = 9 WHERE id = v_m.id;
        v_revertido := v_revertido + v_m.monto;
    END LOOP;

    RETURN v_revertido;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_revertir_devengo IS
    'Anulación de la factura: revierte el devengo con la fecha original. Restituye el compromiso si la O/C sigue abierta (estado 2 o 3); si no, el importe vuelve al disponible.';

-- -----------------------------------------------------------------------------
-- 6) y 7) Pago y su reversa
-- El pago NO altera el disponible: es tesorería, no presupuesto. Se registra para el reporte de
-- ejecución y para conciliar contra bancos.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_registrar_pago(
    p_company_id       BIGINT,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_compra_hdr_id    BIGINT,
    p_fecha            DATE,
    p_monto            NUMERIC,
    p_usuario          VARCHAR,
    p_ip               VARCHAR
)
RETURNS NUMERIC
LANGUAGE plpgsql
AS $$
DECLARE
    v_modo      SMALLINT;
    v_total     NUMERIC(18,4);
    v_m         RECORD;
    v_prorrata  NUMERIC(18,4);
    v_aplicado  NUMERIC(18,4) := 0;
BEGIN
    SELECT c.modo INTO v_modo
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id AND c.modulo = 'COMPRAS_FACTURA';

    IF NOT FOUND OR COALESCE(v_modo, 0) = 0 OR COALESCE(p_monto, 0) <= 0 THEN
        RETURN 0;
    END IF;

    -- El abono se prorratea entre las partidas que devengó la factura, en proporción al devengo.
    SELECT COALESCE(SUM(m.monto), 0) INTO v_total
      FROM public.pst_movimiento m
     WHERE m.company_id = p_company_id
       AND m.documento_tipo = 'FACTURA_COMPRA'
       AND m.documento_id = p_compra_hdr_id
       AND m.tipo_movimiento IN (3, 5)
       AND m.estado = 1;

    IF v_total <= 0 THEN
        RETURN 0;
    END IF;

    FOR v_m IN
        SELECT m.id_presupuesto, m.con_cuenta_code, m.centro_costo_id, m.orden_compra_id,
               SUM(m.monto) AS devengado
          FROM public.pst_movimiento m
         WHERE m.company_id = p_company_id
           AND m.documento_tipo = 'FACTURA_COMPRA'
           AND m.documento_id = p_compra_hdr_id
           AND m.tipo_movimiento IN (3, 5)
           AND m.estado = 1
         GROUP BY m.id_presupuesto, m.con_cuenta_code, m.centro_costo_id, m.orden_compra_id
         ORDER BY m.con_cuenta_code
    LOOP
        v_prorrata := round(p_monto * v_m.devengado / v_total, 4);
        CONTINUE WHEN v_prorrata <= 0;

        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_m.id_presupuesto, v_m.con_cuenta_code, v_m.centro_costo_id,
            7::SMALLINT, 'COMPRAS', 'ABONO_CXP', p_documento_id, p_documento_numero,
            NULL, v_m.orden_compra_id, NULL, p_fecha, v_prorrata,
            0, 0, v_prorrata, 0,
            FALSE, NULL, p_usuario, NULL, p_ip);

        v_aplicado := v_aplicado + v_prorrata;
    END LOOP;

    RETURN v_aplicado;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_registrar_pago IS
    'Abono a la CxP: suma a valor_pagado prorrateando entre las partidas que devengó la factura. NO altera el disponible.';

CREATE OR REPLACE FUNCTION public.sp_pst_revertir_pago(
    p_company_id   BIGINT,
    p_documento_id BIGINT,
    p_motivo       VARCHAR,
    p_usuario      VARCHAR,
    p_ip           VARCHAR
)
RETURNS NUMERIC
LANGUAGE plpgsql
AS $$
DECLARE
    v_m         RECORD;
    v_revertido NUMERIC(18,4) := 0;
BEGIN
    FOR v_m IN
        SELECT m.* FROM public.pst_movimiento m
         WHERE m.company_id = p_company_id
           AND m.documento_tipo = 'ABONO_CXP'
           AND m.documento_id = p_documento_id
           AND m.tipo_movimiento = 7
           AND m.estado = 1
         ORDER BY m.con_cuenta_code, m.id
    LOOP
        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_m.id_presupuesto, v_m.con_cuenta_code, v_m.centro_costo_id,
            8::SMALLINT, v_m.modulo, 'ABONO_CXP', p_documento_id, v_m.documento_numero,
            v_m.documento_detalle_id, v_m.orden_compra_id, NULL, v_m.fecha, v_m.monto,
            0, 0, -v_m.monto, 0,
            FALSE, p_motivo, p_usuario, NULL, p_ip);

        UPDATE public.pst_movimiento SET estado = 9 WHERE id = v_m.id;
        v_revertido := v_revertido + v_m.monto;
    END LOOP;

    RETURN v_revertido;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_revertir_pago IS
    'Anulación de un abono: resta de valor_pagado. No altera el disponible.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
-- =============================================================================
-- a) Las 8 rutinas existen (7 sp_ + 1 helper)
-- SELECT proname FROM pg_proc
--  WHERE proname IN ('fn_pst_aplicar_movimiento', 'sp_pst_comprometer_documento',
--                    'sp_pst_liberar_compromiso', 'sp_pst_ajustar_compromiso',
--                    'sp_pst_devengar_documento', 'sp_pst_revertir_devengo',
--                    'sp_pst_registrar_pago', 'sp_pst_revertir_pago')
--  ORDER BY proname;
-- Esperado: 8 filas.
--
-- b) NO-REGRESIÓN: con el control apagado (modo 0, que es como nace) no hace absolutamente nada.
-- SELECT count(*) AS avisos FROM public.sp_pst_comprometer_documento(
--        2, 'COMPRAS', 'ORDEN_COMPRA', -1, 'PRUEBA', CURRENT_DATE, 'prueba', 'prueba', NULL,
--        ARRAY[ROW('00000000000', NULL, NULL, 100)::public.pst_linea_afectacion]);
-- SELECT count(*) AS movimientos FROM public.pst_movimiento WHERE documento_id = -1;
-- Esperado: 0 avisos y 0 movimientos. Si sale algo, el control NO nació apagado.
--
-- c) Prueba end-to-end en el mirror (NO en producción). Requiere una O/C real y una cuenta
--    presupuestada. Envolver SIEMPRE en BEGIN ... ROLLBACK:
-- BEGIN;
--   UPDATE public.cfg_presupuesto_control SET modo = 2 WHERE company_id = 2 AND modulo = 'COMPRAS_OC';
--   SELECT * FROM public.sp_pst_comprometer_documento(
--          2, 'COMPRAS', 'ORDEN_COMPRA', <id_oc>, '00001', CURRENT_DATE, 'prueba', 'prueba', NULL,
--          ARRAY(SELECT ROW(l.*)::public.pst_linea_afectacion
--                  FROM public.fn_alm_oc_distribucion_partidas(2, <id_oc>) l));
--   SELECT con_cuenta_code, valor_proyeccion, valor_comprometido, valor_real, valor_disponible
--     FROM public.pst_config_presupuesto_dtl WHERE company_id = 2 AND valor_comprometido > 0;
--   SELECT public.sp_pst_liberar_compromiso(2, 'COMPRAS', 'ORDEN_COMPRA', <id_oc>, 'prueba', 'prueba', NULL);
--   -- el comprometido debe volver a 0
-- ROLLBACK;
--
-- d) Conciliación de invariantes (debe salir VACÍA)
-- SELECT d.company_id, d.id_presupuesto, d.con_cuenta_code,
--        d.valor_comprometido AS materializado, COALESCE(c.saldo, 0) AS calculado
--   FROM public.pst_config_presupuesto_dtl d
--   LEFT JOIN (SELECT company_id, id_presupuesto, con_cuenta_code,
--                     SUM(monto_comprometido - monto_devengado - monto_liberado) AS saldo
--                FROM public.pst_compromiso WHERE estado = 1 GROUP BY 1,2,3) c
--          USING (company_id, id_presupuesto, con_cuenta_code)
--  WHERE d.valor_comprometido <> COALESCE(c.saldo, 0);
--
-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- DROP FUNCTION IF EXISTS public.sp_pst_revertir_pago(BIGINT, BIGINT, VARCHAR, VARCHAR, VARCHAR);
-- DROP FUNCTION IF EXISTS public.sp_pst_registrar_pago(BIGINT, BIGINT, VARCHAR, BIGINT, DATE, NUMERIC, VARCHAR, VARCHAR);
-- DROP FUNCTION IF EXISTS public.sp_pst_revertir_devengo(BIGINT, VARCHAR, BIGINT, VARCHAR, VARCHAR, VARCHAR);
-- DROP FUNCTION IF EXISTS public.sp_pst_devengar_documento(BIGINT, VARCHAR, BIGINT, VARCHAR, BIGINT, DATE, VARCHAR, VARCHAR, public.pst_linea_afectacion[]);
-- DROP FUNCTION IF EXISTS public.sp_pst_ajustar_compromiso(BIGINT, VARCHAR, VARCHAR, BIGINT, VARCHAR, VARCHAR, VARCHAR, VARCHAR, public.pst_linea_afectacion[]);
-- DROP FUNCTION IF EXISTS public.sp_pst_liberar_compromiso(BIGINT, VARCHAR, VARCHAR, BIGINT, VARCHAR, VARCHAR, VARCHAR);
-- DROP FUNCTION IF EXISTS public.sp_pst_comprometer_documento(BIGINT, VARCHAR, VARCHAR, BIGINT, VARCHAR, DATE, VARCHAR, VARCHAR, VARCHAR, public.pst_linea_afectacion[]);
-- DROP FUNCTION IF EXISTS public.fn_pst_aplicar_movimiento(BIGINT, VARCHAR, VARCHAR, BIGINT, SMALLINT, VARCHAR, VARCHAR, BIGINT, VARCHAR, BIGINT, BIGINT, BIGINT, DATE, NUMERIC, NUMERIC, NUMERIC, NUMERIC, NUMERIC, BOOLEAN, VARCHAR, VARCHAR, VARCHAR, VARCHAR);
