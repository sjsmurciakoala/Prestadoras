-- =============================================================================
-- Cierre de los periodos contables 2026-07 y 2026-08 (company_id = 2)
-- Fecha: 2026-09-04
--
-- PROBLEMA
--   El portal muestra "No hay un periodo contable abierto" en Contabilidad.
--   La causa NO es que falte un periodo: hay TRES abiertos (70, 84, 85) y
--   PeriodoContableService.ObtenerPeriodoActivoAsync exige exactamente uno.
--   Con mas de uno lanza excepcion, el controlador la devuelve como 500 y la
--   pagina cae al banner generico.
--
-- ALCANCE
--   Correccion de DATOS para el estado concreto de siad_v4 @ 172.16.0.9 al
--   2026-09-04. NO es DDL reutilizable: los ids de poliza y periodo son los de
--   esta base. En el mirror hay que re-verificar el estado antes de aplicar.
--
-- SEGURIDAD
--   Todo en UNA transaccion con gates que abortan por RAISE. Si algo no cuadra
--   no queda nada aplicado.
--     psql -v ON_ERROR_STOP=1 -h 172.16.0.9 -U postgres -d siad_v4 \
--          -f Database/2026-09-04_cierre_periodos_2026-07_2026-08.sql
-- =============================================================================

\set usuario 'correccion-cierre-2026-09'

BEGIN;

-- -----------------------------------------------------------------------------
-- 0. Rango del periodo 84 (idempotente)
--    Venia del 2026-08-01 al 2026-10-01, solapando septiembre completo. La
--    convencion del resto es [dia 1 12:00, dia 1 del mes siguiente 11:59:59].
--    Ya aplicado en siad_v4; se repite por idempotencia para el mirror.
-- -----------------------------------------------------------------------------
UPDATE public.con_periodo_contable
SET end_date = '2026-09-01 11:59:59+00'
WHERE company_id = 2
  AND period_id = 84
  AND end_date <> '2026-09-01 11:59:59+00';

-- -----------------------------------------------------------------------------
-- 1. Polizas de agosto atadas al periodo de julio
--    Seis polizas POSTEADAS con fecha entre 2026-08-02 y 2026-08-23 quedaron
--    con period_id = 70 (julio). Su propio poliza_number dice "2-2026-08-...".
--    Es el mismo defecto de rango del paso 0, un mes antes: mientras el periodo
--    70 tuvo un end_date largo, absorbio documentos de agosto.
--    Esto es lo que fn_con_verificar_saldo_cuenta reporta como
--    FECHA_FUERA_PERIODO (9 de las 11 divergencias de julio) y lo que el
--    rebuild del cache no puede arreglar por si solo.
--    Se reasignan al periodo que les corresponde por fecha.
-- -----------------------------------------------------------------------------
WITH julio AS (
    SELECT start_date::date AS ini, end_date::date AS fin
    FROM public.con_periodo_contable
    WHERE company_id = 2 AND period_id = 70
),
mal_ubicadas AS (
    SELECT h.poliza_id, h.poliza_date::date AS fecha
    FROM public.con_partida_hdr h, julio j
    WHERE h.company_id = 2
      AND h.status = 1
      AND h.period_id = 70
      AND h.poliza_date::date NOT BETWEEN j.ini AND j.fin
)
UPDATE public.con_partida_hdr h
SET period_id = p.period_id,
    updated_at = now(),
    updated_by = :'usuario'
FROM mal_ubicadas m
JOIN public.con_periodo_contable p
  ON p.company_id = 2
 AND m.fecha BETWEEN p.start_date::date AND p.end_date::date
 AND p.period_id <> 70
WHERE h.poliza_id = m.poliza_id;

DO $$
DECLARE v_n integer;
BEGIN
    SELECT count(*) INTO v_n
    FROM public.con_partida_hdr h
    JOIN public.con_periodo_contable p
      ON p.period_id = h.period_id AND p.company_id = h.company_id
    WHERE h.company_id = 2 AND h.status = 1
      AND h.poliza_date::date NOT BETWEEN p.start_date::date AND p.end_date::date;

    IF v_n > 0 THEN
        RAISE EXCEPTION 'GATE 1: quedan % poliza(s) con fecha fuera de su periodo.', v_n;
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2. Par erroneo del documento OPD-333421 (periodo 70)
--
--    El flujo de Ordenes de Pago Directo genero dos partidas con las cuentas
--    contra CRUZADAS:
--      36332 (POSTEADA): D 21101000101 Mexichem   / H 61102050000 Serv.Terc.
--      36333 (BORRADOR): D 11102010101 Davivienda / H 21101000101 Mexichem
--    Lo correcto para ese documento era:
--      Factura: D Serv.Tercerizados / H Mexichem
--      Pago:    D Mexichem          / H Davivienda
--
--    Evidencia dura: postear la 36332 dejo a Servicios Tercerizados, cuenta de
--    COSTO, con saldo ACREEDOR de exactamente 1,500.00.
--
--    DECISION: deshacer el par completo y dejar la orden para recaptura con las
--    cuentas correctas. Se elige sobre postear la 36333 porque esa via congela
--    el error dentro de un periodo CERRADO y revertirlo despues exige reabrir
--    julio. Verificado que ninguna otra tabla las referencia (con_deprecacion,
--    ban_kardex, con_partida_factura, con_partida_pendiente).
-- -----------------------------------------------------------------------------
SELECT public.sp_con_revertir_poliza(2, 36332, :'usuario');

DELETE FROM public.con_partida_dtl WHERE poliza_id IN (36332, 36333);

DELETE FROM public.con_partida_hdr
WHERE company_id = 2
  AND poliza_id IN (36332, 36333)
  AND status = 0;   -- red de seguridad: nunca borra una posteada

DO $$
DECLARE v_n integer;
BEGIN
    SELECT count(*) INTO v_n FROM public.con_partida_hdr
    WHERE company_id = 2 AND poliza_id IN (36332, 36333);
    IF v_n <> 0 THEN
        RAISE EXCEPTION 'GATE 2: quedaron % poliza(s) de OPD-333421 sin borrar.', v_n;
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 3. Reconstruccion del cache de saldos (F6)
--    Respalda, borra y reinserta con_saldo_cuenta desde con_partida_dtl
--    posteado. Es de EMPRESA completa, no de periodo. Corre DESPUES de los
--    pasos 1 y 2 para recalcular sobre datos ya corregidos.
-- -----------------------------------------------------------------------------
SELECT * FROM public.sp_con_reconstruir_saldo_cuenta(2, :'usuario');

-- -----------------------------------------------------------------------------
-- 4. Gate: los dos checklists deben quedar limpios ANTES de cerrar
-- -----------------------------------------------------------------------------
DO $$
DECLARE v_n integer; v_detalle text;
BEGIN
    SELECT count(*), string_agg(format('%s(%s)', c.item, c.cantidad), '; ')
    INTO v_n, v_detalle
    FROM public.fn_con_checklist_cierre_periodo(2, 70) c WHERE NOT c.ok;
    IF v_n > 0 THEN RAISE EXCEPTION 'GATE 4: julio sigue bloqueado -> %', v_detalle; END IF;

    SELECT count(*), string_agg(format('%s(%s)', c.item, c.cantidad), '; ')
    INTO v_n, v_detalle
    FROM public.fn_con_checklist_cierre_periodo(2, 84) c WHERE NOT c.ok;
    IF v_n > 0 THEN RAISE EXCEPTION 'GATE 4: agosto sigue bloqueado -> %', v_detalle; END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 5. Cierre, de mas viejo a mas nuevo. Precierre primero: sp_con_periodo_cerrar
--    solo acepta periodos en estado PRECIERRE.
-- -----------------------------------------------------------------------------
SELECT public.sp_con_periodo_precerrar(2, 70, :'usuario');
SELECT public.sp_con_periodo_cerrar   (2, 70, :'usuario');

SELECT public.sp_con_periodo_precerrar(2, 84, :'usuario');
SELECT public.sp_con_periodo_cerrar   (2, 84, :'usuario');

-- -----------------------------------------------------------------------------
-- 6. Gate final: exactamente UN periodo abierto, y que sea septiembre
-- -----------------------------------------------------------------------------
DO $$
DECLARE v_n integer; v_id bigint;
BEGIN
    SELECT count(*), min(p.period_id) INTO v_n, v_id
    FROM public.con_periodo_contable p
    WHERE p.company_id = 2 AND p.status_id = 0;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'GATE 6: quedaron % periodos abiertos, se esperaba 1.', v_n;
    END IF;
    IF v_id <> 85 THEN
        RAISE EXCEPTION 'GATE 6: el unico abierto es % y se esperaba el 85.', v_id;
    END IF;
    RAISE NOTICE 'OK: unico periodo abierto = % (septiembre 2026).', v_id;
END $$;

COMMIT;

SELECT period_id, start_date::date AS ini, end_date::date AS fin, status_id
FROM public.con_periodo_contable
WHERE company_id = 2 ORDER BY start_date DESC LIMIT 4;
