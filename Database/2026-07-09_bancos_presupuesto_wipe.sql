-- =============================================================================
-- Limpieza previa a la re-migración de Bancos y Presupuesto desde SIMAFI
-- (origen nuevo: MySQL bdsimafi @ 172.16.0.3, alcance 2025 → fecha actual).
--
-- DECISIÓN DEL USUARIO (2026-07-09): "se limpian completamente las tablas y se
-- quitan las partidas contables". Tabula rasa del módulo Bancos y del módulo
-- Presupuesto, INCLUYENDO las cuentas/bancos que se habían capturado en el
-- portal (códigos 000001–000007 y los 6 bancos sin prefijo SIM).
--
-- NO SE TOCA:
--   · con_plan_cuentas          (instrucción explícita del usuario)
--   · ban_moneda               (catálogo; FK requerida por ban_kardex)
--   · ban_tipos_transacciones  (catálogo; el script 04_kardex lo completa)
--   · stg_simafi_cuenta_map    (cross-walk contable; insumo del transform)
--   · las 12,056 pólizas module='SIMAFI' de la migración contable previa
--   · ws_banco_credencial / ws_banco_transaccion (banco_cuenta_id IS NULL,
--     no referencian ninguna cuenta, no pertenecen al módulo ban_*)
--
-- SÍ SE BORRA:
--   · ban_banco, ban_cuenta, ban_kardex, ban_movimiento, ban_movimiento_detalle,
--     ban_movimiento_transito
--   · ban_ws_credencial, ban_ws_pago  (FK NO ACTION sobre ban_cuenta 000003:
--     bloquean el DELETE si no se quitan antes)
--   · las pólizas de con_partida_hdr referenciadas por ban_kardex.partida_cuenta_id
--     (+ su con_partida_dtl por CASCADE). Al 2026-07-09 son 3 y ninguna está
--     posteada: 36232/36234 (module PROV, pruebas "rwegtergt"/"acascasc") y
--     36448 (module BANCOS, smoke test de web service; solo en el mirror).
--   · pst_config_presupuesto_hdr (+ _dtl por CASCADE)
--
-- Idempotente: re-ejecutar sobre una base ya limpia no hace nada y no falla.
-- Transaccional: o pasa todo, o no pasa nada.
--
-- ⚠️ ADVERTENCIA OPERATIVA (integración 2026-07-11, ver
-- docs/INFORME_RAMA_MODULO_CAJA_EMILIO_2026-07-09.md §4):
--   1. Este script BORRA ban_ws_credencial y ban_ws_pago (WS bancario F8).
--      Tras ejecutarlo es OBLIGATORIO re-seedear las credenciales del WS
--      (apc.BancosWs deja de autenticar) y se pierde la bitácora de
--      idempotencia de pagos del banco. Ejecutar solo dentro del runbook
--      de deploy, nunca suelto.
--   2. En bases con F6 (con_saldo_cuenta oficial), correr después el
--      rebuild/verificador de saldos (fn_con_saldo_libro).
-- =============================================================================
BEGIN;

DROP TABLE IF EXISTS tmp_wipe_params;
CREATE TEMP TABLE tmp_wipe_params AS SELECT 2::bigint AS company_id;

-- ---------------------------------------------------------------------------
-- 0) Capturar las pólizas contables colgadas del kardex ANTES de borrarlo
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS tmp_polizas_kardex;
CREATE TEMP TABLE tmp_polizas_kardex AS
SELECT DISTINCT k.partida_cuenta_id AS poliza_id
FROM public.ban_kardex k, tmp_wipe_params p
WHERE k.company_id = p.company_id
  AND k.partida_cuenta_id IS NOT NULL;

-- Guard: abortar si alguna póliza tiene documentos de negocio colgando —
-- facturas conciliadas, depreciación o partidas pendientes. Ésas no son
-- pruebas y borrarlas rompería otros módulos.
--
-- Las pólizas POSTEADAS sí se borran (decisión del usuario 2026-07-09): en el
-- SRV la 36249 "Prueba de deposito 001" quedó posteada durante una prueba de
-- UI. Se verificó que postear NO alimenta con_saldo_cuenta ni
-- con_balance_mensual para esas cuentas, así que no descuadra saldos.
DO $$
DECLARE n_fac int; n_dep int; n_pen int; n_post int;
BEGIN
    SELECT count(*) INTO n_fac FROM public.con_partida_factura   f JOIN tmp_polizas_kardex t ON t.poliza_id = f.poliza_id;
    SELECT count(*) INTO n_dep FROM public.con_deprecacion       d JOIN tmp_polizas_kardex t ON t.poliza_id = d.poliza_id;
    SELECT count(*) INTO n_pen FROM public.con_partida_pendiente p JOIN tmp_polizas_kardex t ON t.poliza_id = p.poliza_id;

    IF n_fac > 0 OR n_dep > 0 OR n_pen > 0 THEN
        RAISE EXCEPTION
            'ABORTADO: pólizas con documentos de negocio (facturas=%, depreciación=%, pendientes=%). Revise antes de borrar.',
            n_fac, n_dep, n_pen;
    END IF;

    SELECT count(*) INTO n_post
    FROM public.con_partida_hdr h JOIN tmp_polizas_kardex t ON t.poliza_id = h.poliza_id
    WHERE h.posted_at IS NOT NULL;

    RAISE NOTICE 'Pólizas a eliminar junto con el kardex: % (de las cuales % posteadas)',
        (SELECT count(*) FROM tmp_polizas_kardex), n_post;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Integraciones de web service que bloquean el DELETE de ban_cuenta
--    (FK con ON DELETE NO ACTION). ban_ws_pago además apunta a con_partida_hdr.
-- ---------------------------------------------------------------------------
DELETE FROM public.ban_ws_pago       WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);
DELETE FROM public.ban_ws_credencial WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);

-- ---------------------------------------------------------------------------
-- 2) Módulo Bancos: movimientos primero, catálogos después
--    ban_movimiento_detalle y ban_movimiento_transito caen por CASCADE desde
--    ban_movimiento, pero se borran explícitamente por si quedaran huérfanos.
-- ---------------------------------------------------------------------------
DELETE FROM public.ban_movimiento_transito
WHERE movimiento_id IN (SELECT movimiento_id FROM public.ban_movimiento
                        WHERE company_id IN (SELECT company_id FROM tmp_wipe_params))
   OR banco_cuenta_id IN (SELECT banco_cuenta_id FROM public.ban_cuenta
                          WHERE company_id IN (SELECT company_id FROM tmp_wipe_params));

DELETE FROM public.ban_kardex WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);

DELETE FROM public.ban_movimiento_detalle
WHERE movimiento_id IN (SELECT movimiento_id FROM public.ban_movimiento
                        WHERE company_id IN (SELECT company_id FROM tmp_wipe_params));

DELETE FROM public.ban_movimiento WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);

-- ---------------------------------------------------------------------------
-- 3) Pólizas contables del kardex borrado (con_partida_dtl cae por CASCADE).
--    Se eliminan SOLO las capturadas en el paso 0 — el resto de contabilidad,
--    incluidas las 12k pólizas SIMAFI, queda intacto.
-- ---------------------------------------------------------------------------
DELETE FROM public.con_partida_hdr
WHERE poliza_id IN (SELECT poliza_id FROM tmp_polizas_kardex);

-- ---------------------------------------------------------------------------
-- 4) Catálogos de Bancos (ya sin hijos)
-- ---------------------------------------------------------------------------
DELETE FROM public.ban_cuenta WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);
DELETE FROM public.ban_banco  WHERE company_id IN (SELECT company_id FROM tmp_wipe_params);

-- ---------------------------------------------------------------------------
-- 5) Módulo Presupuesto
--    pst_config_presupuesto_dtl cae por CASCADE desde _hdr.
--    pst_actividad_presupuesto y pst_solicitud_actividad_presupuesto tienen FK
--    RESTRICT: si tuvieran filas, el DELETE fallaría. Se avisa explícitamente.
-- ---------------------------------------------------------------------------
DO $$
DECLARE n_act int; n_sol int;
BEGIN
    SELECT count(*) INTO n_act FROM public.pst_actividad_presupuesto;
    SELECT count(*) INTO n_sol FROM public.pst_solicitud_actividad_presupuesto;

    IF n_act > 0 OR n_sol > 0 THEN
        RAISE EXCEPTION
            'ABORTADO: el presupuesto tiene hijos vivos (actividades=%, solicitudes=%). FK RESTRICT.', n_act, n_sol;
    END IF;
END $$;

DELETE FROM public.pst_config_presupuesto_hdr;

-- ---------------------------------------------------------------------------
-- 6) Verificación dentro de la misma transacción
-- ---------------------------------------------------------------------------
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT 'ban_banco' t, count(*) n FROM public.ban_banco
        UNION ALL SELECT 'ban_cuenta', count(*) FROM public.ban_cuenta
        UNION ALL SELECT 'ban_kardex', count(*) FROM public.ban_kardex
        UNION ALL SELECT 'ban_movimiento', count(*) FROM public.ban_movimiento
        UNION ALL SELECT 'ban_movimiento_detalle', count(*) FROM public.ban_movimiento_detalle
        UNION ALL SELECT 'ban_ws_credencial', count(*) FROM public.ban_ws_credencial
        UNION ALL SELECT 'ban_ws_pago', count(*) FROM public.ban_ws_pago
        UNION ALL SELECT 'pst_config_presupuesto_hdr', count(*) FROM public.pst_config_presupuesto_hdr
        UNION ALL SELECT 'pst_config_presupuesto_dtl', count(*) FROM public.pst_config_presupuesto_dtl
    LOOP
        IF r.n <> 0 THEN
            RAISE EXCEPTION 'ABORTADO: % quedó con % fila(s) tras la limpieza.', r.t, r.n;
        END IF;
    END LOOP;

    RAISE NOTICE 'Limpieza OK: Bancos y Presupuesto vacíos.';
    RAISE NOTICE 'Preservados: con_plan_cuentas=%, ban_moneda=%, ban_tipos_transacciones=%, pólizas restantes=%',
        (SELECT count(*) FROM public.con_plan_cuentas),
        (SELECT count(*) FROM public.ban_moneda),
        (SELECT count(*) FROM public.ban_tipos_transacciones),
        (SELECT count(*) FROM public.con_partida_hdr);
END $$;

COMMIT;
