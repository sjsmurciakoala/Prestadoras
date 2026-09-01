-- =============================================================================
-- Control presupuestario — unificación de proveedores (OPD) y bancos (F8)
-- Fecha: 2026-08-27
-- Fase F8 (5 de 5). Requiere: los scripts 01, 02 y 03 de esta misma tanda
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ ESTO DEJÓ DE SER OPCIONAL
-- El script 01 cambió la fórmula del disponible a
--     valor_disponible = proyeccion − comprometido − real
-- pero hay DOS sitios más que recalculan la cabecera del presupuesto con la fórmula vieja
-- (valor_global − Σ valor_real), ignorando el comprometido, y la PISAN:
--
--   1) fn_pst_afectar_saldo_real_credito       -> cada partida bancaria con crédito
--   2) OrdenesPagoDirectoService.RecalculateBudgetHeadersAsync -> cada compromiso a proveedor
--
-- No es un riesgo teórico: ocurre en cuanto convivan un compromiso de compras y un movimiento
-- bancario. Este script cierra (1); (2) se elimina del C# en la misma entrega.
--
-- QUÉ MÁS APORTA
--   - Los compromisos a proveedor pasan a tomar LOCK sobre la partida. Hoy no toman ninguno: se
--     apoyan en IsolationLevel.Serializable y su 40001 no se maneja, así que bajo concurrencia
--     devuelven un 500 crudo.
--   - Los compromisos a proveedor pasan a dejar rastro en el kardex (pst_movimiento).
--
-- QUÉ SE CREA / SE CAMBIA
--   1) pst_movimiento          -> CHECK e índice ampliados con los tipos 14 y 15.
--   2) sp_pst_afectar_valor_real -> motor compartido (lock + validación + kardex) para los
--                                   movimientos que consumen EJECUTADO directamente.
--   3) fn_pst_afectar_saldo_real_credito -> MISMA firma y MISMOS códigos de retorno; ahora
--                                   recalcula bien la cabecera y respeta el modo del módulo.
--
-- ⚠️ CAMBIO DE COMPORTAMIENTO, BAJO INTERRUPTOR
-- La validación sigue midiendo contra (proyeccion − real), igual que hoy, MIENTRAS el módulo esté
-- en modo 0 (Apagado), que es como nacen PROVEEDORES y BANCOS. Solo al encenderlos pasa a
-- descontar también el comprometido. Encenderlos puede rechazar operaciones que hoy pasan: es lo
-- correcto (los dos consumen el mismo presupuesto), pero es una decisión deliberada.
--
-- Cambio ADITIVO salvo el reemplazo de una función existente, cuya definición anterior queda
-- copiada al pie para poder revertirla.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Dos tipos de movimiento nuevos para el compromiso a proveedor
--
--    Van FUERA del índice de idempotencia, igual que los ajustes (12/13), porque el flujo de
--    OPD es REPETIBLE por naturaleza: al editar una orden revierte el importe anterior y aplica
--    el nuevo, sobre el MISMO documento. Si estuvieran dentro del índice, la segunda aplicación
--    chocaría, fn_pst_aplicar_movimiento devolvería NULL y —esto es lo grave— el delta NO se
--    aplicaría. El presupuesto quedaría desincronizado en silencio.
-- -----------------------------------------------------------------------------
ALTER TABLE public.pst_movimiento DROP CONSTRAINT IF EXISTS ck_pst_movimiento_tipo;
ALTER TABLE public.pst_movimiento
    ADD CONSTRAINT ck_pst_movimiento_tipo
    CHECK (tipo_movimiento IN (1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15));

DROP INDEX IF EXISTS public.uq_pst_movimiento_idempotencia;
CREATE UNIQUE INDEX uq_pst_movimiento_idempotencia
    ON public.pst_movimiento (company_id, tipo_movimiento, documento_tipo, documento_id,
                              con_cuenta_code, COALESCE(documento_detalle_id, 0))
    WHERE estado = 1 AND tipo_movimiento NOT IN (10, 11, 12, 13, 14, 15);

COMMENT ON COLUMN public.pst_movimiento.tipo_movimiento IS '1 Compromiso inicial (aprobación de la O/C) · 2 Liberación · 3 Devengo · 4 Reversa de devengo · 5 Devengo directo (sin O/C) · 6 Reversa de devengo directo · 7 Pago · 8 Reversa de pago · 10 Ampliación de presupuesto · 11 Reducción de presupuesto · 12 Ajuste de compromiso (aumento) · 13 Ajuste de compromiso (disminución) · 14 Ejecución de compromiso a proveedor · 15 Reversa de ejecución de compromiso a proveedor. Los tipos 10-15 son repetibles y quedan fuera del índice de idempotencia.';

-- -----------------------------------------------------------------------------
-- 2) sp_pst_afectar_valor_real — motor compartido del EJECUTADO
--
--    Para los módulos que consumen valor_real directamente, sin pasar por un compromiso previo:
--    los compromisos a proveedor (OPD) y los créditos bancarios.
--
--    Diferencia importante con sp_pst_comprometer_documento: aquí la validación se hace SIEMPRE,
--    incluso con el módulo apagado. No es una contradicción: estos dos módulos YA validaban
--    presupuesto antes de que existiera este control, y apagarlo no debe desactivar una regla que
--    lleva años en producción. El modo solo decide si el comprometido entra en la base.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_pst_afectar_valor_real(
    p_company_id       BIGINT,
    p_modulo           VARCHAR,
    p_documento_tipo   VARCHAR,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_fecha            DATE,
    p_usuario          VARCHAR,
    p_ip               VARCHAR,
    p_direccion        SMALLINT,          -- +1 consume · -1 devuelve
    p_exige_aprobado   BOOLEAN,
    p_lineas           public.pst_linea_afectacion[]
)
RETURNS TABLE (con_cuenta_code VARCHAR, disponible NUMERIC, requerido NUMERIC, exceso NUMERIC, excedio BOOLEAN)
LANGUAGE plpgsql
AS $$
DECLARE
    v_modo      SMALLINT;
    v_cuenta    VARCHAR(20);
    v_monto     NUMERIC(18,4);
    v_partida   VARCHAR(10);
    v_proy      NUMERIC(18,4);
    v_comp      NUMERIC(18,4);
    v_real      NUMERIC(18,4);
    v_base      NUMERIC(18,4);
    v_disp      NUMERIC(18,4);
    v_tipo      SMALLINT;
BEGIN
    SELECT c.modo INTO v_modo
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id AND c.modulo = p_modulo;
    v_modo := COALESCE(v_modo, 0);

    v_tipo := CASE WHEN p_direccion >= 0 THEN 14 ELSE 15 END;

    FOR v_cuenta, v_monto IN
        SELECT upper(btrim(l.con_cuenta_code)), SUM(l.monto)
          FROM unnest(p_lineas) l
         WHERE COALESCE(l.monto, 0) > 0
           AND l.con_cuenta_code IS NOT NULL
           AND btrim(l.con_cuenta_code) <> ''
         GROUP BY upper(btrim(l.con_cuenta_code))
         ORDER BY 1                       -- ★ orden determinístico: anti-deadlock
    LOOP
        -- Cuenta no presupuestable: se ignora. Es el criterio que ya usaba OPD.
        IF NOT EXISTS (
            SELECT 1 FROM public.con_plan_cuentas p
             WHERE p.company_id = p_company_id
               AND upper(btrim(p.code)) = v_cuenta
               AND p.allows_budget
        ) THEN
            CONTINUE;
        END IF;

        -- Al devolver no se exige presupuesto aprobado: como en el resto del motor, devolver
        -- presupuesto nunca debe estar bloqueado.
        SELECT r.id_presupuesto INTO v_partida
          FROM public.fn_pst_resolver_partida(
                   p_company_id, v_cuenta, p_fecha,
                   COALESCE(p_exige_aprobado, TRUE) AND p_direccion >= 0) r;

        IF v_partida IS NULL THEN
            RAISE EXCEPTION 'No existe un presupuesto% y vigente para la cuenta % en la fecha %.',
                CASE WHEN COALESCE(p_exige_aprobado, TRUE) AND p_direccion >= 0 THEN ' aprobado' ELSE '' END,
                v_cuenta, to_char(p_fecha, 'YYYY-MM-DD')
                USING ERRCODE = 'P0001';
        END IF;

        SELECT COALESCE(d.valor_proyeccion, 0), COALESCE(d.valor_comprometido, 0), COALESCE(d.valor_real, 0)
          INTO v_proy, v_comp, v_real
          FROM public.pst_config_presupuesto_dtl d
         WHERE d.company_id = p_company_id
           AND d.id_presupuesto = v_partida
           AND d.con_cuenta_code = v_cuenta
         FOR UPDATE;                      -- ★ EL LOCK que hoy no existe en OPD

        -- Con el módulo apagado se conserva la base histórica (proyección − ejecutado). Al
        -- encenderlo, el comprometido de compras también cuenta.
        v_base := CASE WHEN v_modo = 0 THEN v_proy - v_real
                       ELSE v_proy - v_real - v_comp END;
        v_disp := GREATEST(v_base, 0);

        IF p_direccion >= 0 AND v_monto > v_disp THEN
            RAISE EXCEPTION 'La operación excede el presupuesto disponible para la cuenta %. Disponible: %. Requerido: %. Faltan: %.',
                v_cuenta,
                to_char(v_disp, 'FM999999999990.00'),
                to_char(v_monto, 'FM999999999990.00'),
                to_char(v_monto - v_disp, 'FM999999999990.00')
                USING ERRCODE = 'P0001';
        END IF;

        PERFORM public.fn_pst_aplicar_movimiento(
            p_company_id, v_partida, v_cuenta, NULL,
            v_tipo, p_modulo, p_documento_tipo, p_documento_id, p_documento_numero,
            NULL, NULL, NULL, p_fecha, v_monto,
            0, CASE WHEN p_direccion >= 0 THEN v_monto ELSE -v_monto END, 0, 0,
            FALSE, NULL, p_usuario, NULL, p_ip);
    END LOOP;

    RETURN;
END;
$$;

COMMENT ON FUNCTION public.sp_pst_afectar_valor_real IS
    'Motor compartido de los módulos que consumen valor_real directamente (compromisos a proveedor, créditos bancarios): bloquea la partida, valida y escribe el kardex. Valida SIEMPRE; el modo solo decide si el comprometido entra en la base.';

-- -----------------------------------------------------------------------------
-- 3) fn_pst_afectar_saldo_real_credito — MISMA firma, MISMOS códigos de retorno
--
--    BanTransaccionesService la llama con (company, account_id, fecha, credito) y distingue
--    0 = excede · 1 = ok · 2 = presupuesto sin aprobar. Ese contrato NO cambia, así que el C# de
--    bancos queda intacto.
--
--    Qué cambia por dentro:
--      a) la cabecera se recalcula con fn_pst_recalcular_cabecera (que sí descuenta el
--         comprometido) en vez de con la fórmula vieja que la inflaba;
--      b) la base de validación respeta el modo del módulo BANCOS.
--
--    ⚠️ Lo que NO se hace: escribir el kardex. Bancos llama a esta función ANTES de crear la
--    partida, así que en ese punto no existe ningún id de documento con el cual identificar el
--    movimiento. Inventarle uno ensuciaría un libro de auditoría, y usar un id fijo rompería la
--    idempotencia. Cerrarlo requiere mover la llamada a después de crear la partida —
--    reestructurar la transacción de bancos—, que es un cambio aparte y con su propio riesgo.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_afectar_saldo_real_credito(
    p_company_id BIGINT,
    p_account_id BIGINT,
    p_poliza_date DATE,
    p_credito NUMERIC
)
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_account_code     VARCHAR(30);
    v_allows_budget    BOOLEAN := FALSE;
    v_id_presupuesto   VARCHAR(10);
    v_saldo_real       NUMERIC(18,4);
    v_saldo_proyectado NUMERIC(18,4);
    v_comprometido     NUMERIC(18,4);
    v_estado_aprobado  BOOLEAN := FALSE;
    v_nuevo_saldo_real NUMERIC(18,4);
    v_modo             SMALLINT;
    v_base             NUMERIC(18,4);
BEGIN
    IF p_company_id IS NULL
       OR p_account_id IS NULL
       OR p_poliza_date IS NULL
       OR COALESCE(p_credito, 0) = 0 THEN
        RETURN 1;
    END IF;

    -- Se conserva la doble búsqueda del original (con_plan_cuenta / con_plan_cuentas): hay bases
    -- donde la tabla tiene el nombre en singular.
    IF to_regclass('public.con_plan_cuenta') IS NOT NULL THEN
        SELECT c.code, COALESCE(c.allows_budget, FALSE)
          INTO v_account_code, v_allows_budget
          FROM public.con_plan_cuenta c
         WHERE c.account_id = p_account_id AND c.company_id = p_company_id
         LIMIT 1;
    ELSIF to_regclass('public.con_plan_cuentas') IS NOT NULL THEN
        SELECT c.code, COALESCE(c.allows_budget, FALSE)
          INTO v_account_code, v_allows_budget
          FROM public.con_plan_cuentas c
         WHERE c.account_id = p_account_id AND c.company_id = p_company_id
         LIMIT 1;
    ELSE
        RETURN 1;
    END IF;

    IF v_account_code IS NULL OR NOT v_allows_budget THEN
        RETURN 1;
    END IF;

    SELECT c.modo INTO v_modo
      FROM public.cfg_presupuesto_control c
     WHERE c.company_id = p_company_id AND c.modulo = 'BANCOS';
    v_modo := COALESCE(v_modo, 0);

    SELECT d.id_presupuesto,
           COALESCE(d.valor_real, 0),
           COALESCE(d.valor_proyeccion, 0),
           COALESCE(d.valor_comprometido, 0),
           COALESCE(h.estado_aprobado, FALSE)
      INTO v_id_presupuesto, v_saldo_real, v_saldo_proyectado, v_comprometido, v_estado_aprobado
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.company_id = d.company_id
       AND h.id_presupuesto = d.id_presupuesto
     WHERE d.company_id = p_company_id
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code))
       AND p_poliza_date BETWEEN h.fecha_inicia AND h.fecha_finaliza
     ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC
     LIMIT 1
     FOR UPDATE OF d;

    IF NOT FOUND THEN
        RETURN 1;
    END IF;

    IF p_credito > 0 AND NOT v_estado_aprobado THEN
        RETURN 2;
    END IF;

    -- Apagado = base histórica (proyección − ejecutado), idéntica a la de siempre.
    -- Encendido = también descuenta lo comprometido por las órdenes de compra aprobadas.
    v_base := CASE WHEN v_modo = 0 THEN v_saldo_proyectado - v_saldo_real
                   ELSE v_saldo_proyectado - v_saldo_real - v_comprometido END;

    IF p_credito > 0 AND p_credito > v_base THEN
        RETURN 0;
    END IF;

    v_nuevo_saldo_real := GREATEST(COALESCE(v_saldo_real, 0) + p_credito, 0);

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_real = v_nuevo_saldo_real,
           valor_disponible = GREATEST(
               COALESCE(d.valor_proyeccion, 0) - COALESCE(d.valor_comprometido, 0) - v_nuevo_saldo_real, 0)
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = v_id_presupuesto
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code));

    -- ★ La corrección: antes recalculaba la cabecera con (valor_global − Σ valor_real), ignorando
    -- el comprometido, y la dejaba inflada.
    PERFORM public.fn_pst_recalcular_cabecera(p_company_id, v_id_presupuesto);

    RETURN 1;
END;
$$;

COMMENT ON FUNCTION public.fn_pst_afectar_saldo_real_credito(BIGINT, BIGINT, DATE, NUMERIC) IS
    'Crédito bancario contra presupuesto. Códigos: 0 excede · 1 ok · 2 sin aprobar. Desde 2026-08-27 recalcula la cabecera con fn_pst_recalcular_cabecera y respeta el modo del módulo BANCOS. No escribe kardex: el llamador todavía no tiene id de documento en ese punto.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT; empresa 2)
-- =============================================================================
-- a) Las rutinas existen y el CHECK admite los tipos nuevos
-- SELECT proname FROM pg_proc WHERE proname IN ('sp_pst_afectar_valor_real', 'fn_pst_afectar_saldo_real_credito');
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_pst_movimiento_tipo';
-- SELECT pg_get_indexdef(indexrelid) FROM pg_index WHERE indexrelid = 'uq_pst_movimiento_idempotencia'::regclass;
--
-- b) NO-REGRESIÓN de bancos: la firma y los códigos de retorno son los de siempre
-- SELECT pg_get_function_identity_arguments(oid) AS args, pg_get_function_result(oid) AS res
--   FROM pg_proc WHERE proname = 'fn_pst_afectar_saldo_real_credito';
-- Esperado: 'p_company_id bigint, p_account_id bigint, p_poliza_date date, p_credito numeric' e 'integer'.
--
-- c) Una cuenta sin presupuesto sigue devolviendo 1 (no bloquea nada)
-- SELECT public.fn_pst_afectar_saldo_real_credito(2, -1, CURRENT_DATE, 100);   -- espera 1
--
-- d) La cabecera ya no queda inflada. Con una partida comprometida, el disponible de la cabecera
--    debe descontar el comprometido (diferencia = 0):
-- SELECT h.id_presupuesto, h.valor_global, h.valor_comprometido, h.valor_disponible,
--        h.valor_global
--          - COALESCE((SELECT SUM(d.valor_comprometido + d.valor_real)
--                        FROM public.pst_config_presupuesto_dtl d
--                       WHERE d.company_id = h.company_id AND d.id_presupuesto = h.id_presupuesto), 0)
--          - h.valor_disponible AS diferencia
--   FROM public.pst_config_presupuesto_hdr h WHERE h.company_id = 2;
--
-- =============================================================================
-- ROLLBACK — definición ANTERIOR de fn_pst_afectar_saldo_real_credito
-- =============================================================================
-- Para revertir: restaurar la versión previa (recalcula la cabecera con valor_global − SUM(valor_real)
-- y valida contra proyeccion − real, sin considerar el comprometido). Está en
-- Database/ddl_v3/20260331_presupuesto_credito_valor_disponible.sql, que es el script que la dejó
-- como estaba antes de esta tanda.
--
-- Y para los tipos de movimiento:
-- ALTER TABLE public.pst_movimiento DROP CONSTRAINT IF EXISTS ck_pst_movimiento_tipo;
-- ALTER TABLE public.pst_movimiento ADD CONSTRAINT ck_pst_movimiento_tipo
--     CHECK (tipo_movimiento IN (1,2,3,4,5,6,7,8,10,11,12,13));
-- DROP INDEX IF EXISTS public.uq_pst_movimiento_idempotencia;
-- CREATE UNIQUE INDEX uq_pst_movimiento_idempotencia
--     ON public.pst_movimiento (company_id, tipo_movimiento, documento_tipo, documento_id,
--                               con_cuenta_code, COALESCE(documento_detalle_id, 0))
--     WHERE estado = 1 AND tipo_movimiento NOT IN (10, 11, 12, 13);
-- DROP FUNCTION IF EXISTS public.sp_pst_afectar_valor_real(BIGINT, VARCHAR, VARCHAR, BIGINT, VARCHAR, DATE, VARCHAR, VARCHAR, SMALLINT, BOOLEAN, public.pst_linea_afectacion[]);
