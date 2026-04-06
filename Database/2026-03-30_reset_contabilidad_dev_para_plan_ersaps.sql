-- ============================================================
-- 2026-03-30_reset_contabilidad_dev_para_plan_ersaps.sql
-- Objetivo:
--   Reset contable controlado por empresa para entorno de desarrollo,
--   dejando limpio el modulo contable antes de cargar el plan ERSAPS.
--
-- Alcance:
--   - Limpia plan de cuentas, reglas, plantillas, polizas y saldos.
--   - Borra configuraciones contables dependientes del catalogo.
--   - Desvincula referencias contables en modulos operativos.
--   - NO elimina clientes, facturas ni maestros comerciales.
--   - NO toca cfg_company ni catalogos base globales.
--
-- Uso:
--   1) Ajustar tmp_reset_contabilidad_params.company_id
--   2) Ejecutar en entorno DEV
--   3) Importar/cargar plan ERSAPS
--   4) Remapear cuentas en servicios, bancos, miscelaneos y configuraciones
--
-- Nota:
--   Se conservan por defecto:
--   - con_diario
--   - con_periodo_contable
--   - con_tipo_transaccion
--   - con_centro_costo
--   porque no bloquean la carga del plan ERSAPS y pueden reutilizarse.
-- ============================================================

DROP TABLE IF EXISTS tmp_reset_contabilidad_params;
CREATE TEMP TABLE tmp_reset_contabilidad_params (
    company_id bigint NOT NULL,
    user_name text NOT NULL
);

INSERT INTO tmp_reset_contabilidad_params (company_id, user_name)
VALUES (1, 'system');

BEGIN;

DO $$
DECLARE
    v_company_id bigint;
    v_user text;
BEGIN
    SELECT company_id, user_name
      INTO v_company_id, v_user
      FROM tmp_reset_contabilidad_params
     LIMIT 1;

    IF v_company_id IS NULL OR v_company_id <= 0 THEN
        RAISE EXCEPTION 'Debe indicar un company_id valido en tmp_reset_contabilidad_params.';
    END IF;

    RAISE NOTICE '==== RESET CONTABLE DEV / company_id=% ====', v_company_id;

    -- ------------------------------------------------------------
    -- 1) Desvincular referencias a polizas en otros modulos
    -- ------------------------------------------------------------
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_movimiento'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.ban_movimiento
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_kardex'
           AND column_name = 'partida_cuenta_id'
    ) THEN
        UPDATE public.ban_kardex
           SET partida_cuenta_id = NULL
         WHERE company_id = v_company_id
           AND partida_cuenta_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_kardex'
           AND column_name = 'poliza_id'
    ) THEN
        UPDATE public.ban_kardex
           SET poliza_id = NULL
         WHERE company_id = v_company_id
           AND poliza_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ven_factura'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.ven_factura
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ven_nota'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.ven_nota n
           SET con_partida_hdr_id = NULL
          FROM public.ven_factura f
         WHERE n.factura_id = f.factura_id
           AND f.company_id = v_company_id
           AND n.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ven_cobro'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.ven_cobro
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'com_factura'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.com_factura
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'com_pago'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.com_pago
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'inv_movimiento'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.inv_movimiento
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'af_activo'
           AND column_name = 'con_partida_hdr_alta_id'
    ) THEN
        UPDATE public.af_activo
           SET con_partida_hdr_alta_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_alta_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'af_depreciacion'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.af_depreciacion d
           SET con_partida_hdr_id = NULL
          FROM public.af_activo a
         WHERE d.activo_id = a.activo_id
           AND a.company_id = v_company_id
           AND d.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'af_baja'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.af_baja b
           SET con_partida_hdr_id = NULL
          FROM public.af_activo a
         WHERE b.activo_id = a.activo_id
           AND a.company_id = v_company_id
           AND b.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'adm_cxc_movimiento'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.adm_cxc_movimiento m
           SET con_partida_hdr_id = NULL
          FROM public.adm_cxc_resumen r
         WHERE m.cxc_id = r.cxc_id
           AND r.company_id = v_company_id
           AND m.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'adm_cxp_movimiento'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.adm_cxp_movimiento m
           SET con_partida_hdr_id = NULL
          FROM public.adm_cxp_resumen r
         WHERE m.cxp_id = r.cxp_id
           AND r.company_id = v_company_id
           AND m.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'adm_interes_mora'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.adm_interes_mora m
           SET con_partida_hdr_id = NULL
          FROM public.adm_cxc_resumen r
         WHERE m.cxc_id = r.cxc_id
           AND r.company_id = v_company_id
           AND m.con_partida_hdr_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'adm_ajuste_fiscal'
           AND column_name = 'con_partida_hdr_id'
    ) THEN
        UPDATE public.adm_ajuste_fiscal
           SET con_partida_hdr_id = NULL
         WHERE company_id = v_company_id
           AND con_partida_hdr_id IS NOT NULL;
    END IF;

    -- ------------------------------------------------------------
    -- 2) Desvincular referencias al plan de cuentas
    -- ------------------------------------------------------------
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'servicios'
           AND column_name = 'cont_account_id'
    ) THEN
        UPDATE public.servicios
           SET cont_account_id = NULL
         WHERE company_id = v_company_id
           AND cont_account_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'miscelaneos_catalogo'
           AND column_name = 'cont_account_id'
    ) THEN
        UPDATE public.miscelaneos_catalogo
           SET cont_account_id = NULL
         WHERE cont_account_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_cuenta'
           AND column_name = 'cont_account_id'
    ) THEN
        UPDATE public.ban_cuenta
           SET cont_account_id = NULL
         WHERE company_id = v_company_id
           AND cont_account_id IS NOT NULL;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'inv_categoria'
           AND column_name = 'cuenta_inventario_id'
    ) THEN
        UPDATE public.inv_categoria
           SET cuenta_inventario_id = NULL,
               cuenta_costo_id = NULL,
               cuenta_ingreso_id = NULL
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'inv_producto'
           AND column_name = 'cuenta_inventario_id'
    ) THEN
        UPDATE public.inv_producto
           SET cuenta_inventario_id = NULL,
               cuenta_costo_id = NULL,
               cuenta_ingreso_id = NULL
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'af_categoria'
           AND column_name = 'cuenta_activo_id'
    ) THEN
        UPDATE public.af_categoria
           SET cuenta_activo_id = NULL,
               cuenta_dep_acum_id = NULL,
               cuenta_gasto_dep_id = NULL
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'con_activo_fijo'
           AND column_name = 'asset_account_id'
    ) THEN
        UPDATE public.con_activo_fijo
           SET asset_account_id = NULL,
               depreciation_account_id = NULL
         WHERE company_id = v_company_id;
    END IF;

    -- ------------------------------------------------------------
    -- 3) Limpiar configuracion contable dependiente del catalogo
    -- ------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_configuracion_balance') THEN
        DELETE FROM public.con_configuracion_balance
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_configuracion_linea_resultado') THEN
        DELETE FROM public.con_configuracion_linea_resultado
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_configuracion_estado_resultado') THEN
        DELETE FROM public.con_configuracion_estado_resultado
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_configuracion_correlativo') THEN
        DELETE FROM public.con_configuracion_correlativo
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_configuracion_sistema') THEN
        DELETE FROM public.con_configuracion_sistema
         WHERE company_id = v_company_id;
    END IF;

    -- ------------------------------------------------------------
    -- 4) Limpiar reglas, plantillas, saldos y movimientos contables
    -- ------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_apertura_centro_costo') THEN
        DELETE FROM public.con_apertura_centro_costo
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_apertura_saldo') THEN
        DELETE FROM public.con_apertura_saldo
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_balance_mensual') THEN
        DELETE FROM public.con_balance_mensual
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_saldo_cuenta') THEN
        DELETE FROM public.con_saldo_cuenta
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_partida_dtl') THEN
        DELETE FROM public.con_partida_dtl
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_partida_hdr') THEN
        DELETE FROM public.con_partida_hdr
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_plantilla_partida_dtl') THEN
        DELETE FROM public.con_plantilla_partida_dtl
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_plantilla_partida_hdr') THEN
        DELETE FROM public.con_plantilla_partida_hdr
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_regla_integracion') THEN
        DELETE FROM public.con_regla_integracion
         WHERE company_id = v_company_id;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.tables
         WHERE table_schema = 'public'
           AND table_name = 'con_tipo_transaccion_rule'
    ) THEN
        DELETE FROM public.con_tipo_transaccion_rule
         WHERE company_id = v_company_id;
    END IF;

    -- ------------------------------------------------------------
    -- 5) Limpiar el plan de cuentas
    -- ------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'con_plan_cuentas') THEN
        DELETE FROM public.con_plan_cuentas
         WHERE company_id = v_company_id;
    END IF;

    RAISE NOTICE 'Reset contable completado para company_id=% por usuario=%', v_company_id, v_user;
END
$$;

COMMIT;

-- ============================================================
-- Verificacion minima posterior al reset
-- ============================================================
SELECT 'con_plan_cuentas' AS tabla, COUNT(*) AS registros
FROM public.con_plan_cuentas
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_regla_integracion', COUNT(*)
FROM public.con_regla_integracion
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_plantilla_partida_hdr', COUNT(*)
FROM public.con_plantilla_partida_hdr
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_partida_hdr', COUNT(*)
FROM public.con_partida_hdr
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_partida_dtl', COUNT(*)
FROM public.con_partida_dtl
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_saldo_cuenta', COUNT(*)
FROM public.con_saldo_cuenta
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)

UNION ALL

SELECT 'con_configuracion_sistema', COUNT(*)
FROM public.con_configuracion_sistema
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1);

-- Referencias que quedan pendientes de remapear despues de cargar ERSAPS.
SELECT
    'servicios_sin_cuenta' AS pendiente,
    COUNT(*) AS cantidad
FROM public.servicios
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)
  AND cont_account_id IS NULL

UNION ALL

SELECT
    'ban_cuenta_sin_cont_account_id',
    COUNT(*)
FROM public.ban_cuenta
WHERE company_id = (SELECT company_id FROM tmp_reset_contabilidad_params LIMIT 1)
  AND cont_account_id IS NULL

UNION ALL

SELECT
    'miscelaneos_catalogo_sin_cuenta',
    COUNT(*)
FROM public.miscelaneos_catalogo
WHERE cont_account_id IS NULL;

DROP TABLE IF EXISTS tmp_reset_contabilidad_params;
