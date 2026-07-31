-- =============================================================================
-- F7 H5 (parte SQL) — limpieza del legado + series atómicas de cobranza
-- =============================================================================
-- Plan: PLAN_UNIFICACION_COBRANZA_2026-07.md §6-F7 y PLAN_F7_CORTE H5.
--
-- 1. Series de adm_documento_secuencia para los 4 correlativos de cobranza que
--    F6 dejó con el generador top-1 por string (carrera + dependencia del ancho
--    D6): planes de pago, notas de cobro, cartas de cobro y cortes masivos.
--    Sin prefijo y padding 6 para continuar la numeración existente tal cual.
--    Sembradas desde el MAX vigente por empresa.
-- 2. DROP de los SPs legacy sin callers (los 4 caminos C# murieron con la
--    fachada; verificado por grep: solo los referenciaba CaptacionPagosService).
--    NO se toca fn_getclientesaldos_posteomanual (viva, mismo archivo fuente).
-- 3. DROP del overload de 1 argumento de sp_obtener_cliente_saldo (cross-company
--    por diseño, documentado como bug — ya sin callers).
-- 4. DROP de la tabla vacía tipo_transaccion (scaffold SIMAFI; la entidad EF y
--    su DbSet salen en el mismo commit).
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ----------------------------------------------------------------------------
-- 1) Series de cobranza (idempotente; una por empresa, canal 0, sin prefijo)
-- ----------------------------------------------------------------------------
INSERT INTO public.adm_documento_secuencia
    (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, activo, updated_by)
SELECT c.company_id, s.tipo, 0, '', 6, COALESCE(s.maximo, 0), true, 'uc_f7_h5'
FROM public.cfg_company c
CROSS JOIN LATERAL (
    VALUES
        ('PLAN_PAGO',    (SELECT MAX(h.correlativo::int) FROM public.cln_plan_pago_hdr h
                           WHERE h.company_id = c.company_id AND h.correlativo ~ '^[0-9]+$')),
        ('NOTA_COBRO',   (SELECT MAX(n.correlativo::int) FROM public.cln_nota_cobro n
                           WHERE n.company_id = c.company_id AND n.correlativo ~ '^[0-9]+$')),
        ('CARTA_COBRO',  (SELECT MAX(h.correlativo::int) FROM public.cln_carta_cobro_hdr h
                           WHERE h.company_id = c.company_id AND h.correlativo ~ '^[0-9]+$')),
        ('CORTE_MASIVO', (SELECT MAX(h.correlativo::int) FROM public.cln_corte_masivo_hdr h
                           WHERE h.company_id = c.company_id AND h.correlativo ~ '^[0-9]+$'))
) AS s(tipo, maximo)
ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- 2) SPs legacy muertos (firmas exactas inventariadas en pg_proc)
-- ----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS public.sp_lectura(integer, integer, character varying, date, character varying, numeric, numeric, numeric, numeric, numeric, numeric, character, character, character varying, character varying, numeric, character, character, character varying, integer, integer, character, character varying, character varying, bytea, numeric, character);
DROP PROCEDURE IF EXISTS public.sp_lectura_v2(integer, integer, character varying, date, character varying, numeric, numeric, character, character, character varying, character varying, numeric, character, character, character varying, integer, integer, character, character varying, character varying, bytea, numeric, character, jsonb);
DROP PROCEDURE IF EXISTS public.sp_posteo(character varying, character varying, character varying, character varying);
DROP PROCEDURE IF EXISTS public.sp_registrar_posteo_lectoras(character varying, integer, character varying, character varying, character varying, character varying, numeric, numeric, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying);
DROP PROCEDURE IF EXISTS public.sp_registrar_posteo_manual(character varying, integer, character varying, character varying, character varying, character varying, numeric, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying, character varying);
DROP PROCEDURE IF EXISTS public.sp_reversar_posteo_manual(character varying, integer, character varying);
DROP PROCEDURE IF EXISTS public.sp_actualizar_detalle_posteolectora(integer, character varying, character varying, character varying, numeric, numeric);
DROP FUNCTION  IF EXISTS public.sp_actualizar_detalle_posteolectora(bigint, integer, numeric, numeric);
DROP FUNCTION  IF EXISTS public.sp_actualizar_detalle_posteomanual(integer, numeric, numeric);
DROP FUNCTION  IF EXISTS public.sp_actualizar_detalle_posteomanual(bigint, numeric, numeric);
DROP PROCEDURE IF EXISTS public.sp_actualizar_factura_pago(integer, character varying, character varying, character varying, date);
DROP FUNCTION  IF EXISTS public.sp_actualizar_factura_pago(character varying, character varying, character, character varying, character varying);

-- ----------------------------------------------------------------------------
-- 3) Overload cross-company de 1 argumento (bug documentado, sin callers)
-- ----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.sp_obtener_cliente_saldo(character varying);

-- ----------------------------------------------------------------------------
-- 4) Tabla vacía del scaffold SIMAFI
-- ----------------------------------------------------------------------------
DO $$
DECLARE v_filas bigint;
BEGIN
    SELECT count(*) INTO v_filas FROM public.tipo_transaccion;
    IF v_filas > 0 THEN
        RAISE EXCEPTION 'tipo_transaccion tiene % filas — se esperaba vacía. Revisar antes de dropear.', v_filas;
    END IF;
END $$;
DROP TABLE IF EXISTS public.tipo_transaccion;

COMMIT;

-- ----------------------------------------------------------------------------
-- Humo
-- ----------------------------------------------------------------------------
DO $$
DECLARE v_n int;
BEGIN
    SELECT count(*) INTO v_n FROM pg_proc
     WHERE proname IN ('sp_lectura','sp_lectura_v2','sp_posteo','sp_registrar_posteo_manual',
                       'sp_registrar_posteo_lectoras','sp_reversar_posteo_manual',
                       'sp_actualizar_detalle_posteolectora','sp_actualizar_factura_pago',
                       'sp_actualizar_detalle_posteomanual');
    IF v_n <> 0 THEN RAISE EXCEPTION 'Quedaron % SPs legacy sin dropear', v_n; END IF;

    SELECT count(*) INTO v_n FROM pg_proc WHERE proname = 'sp_obtener_cliente_saldo';
    IF v_n <> 1 THEN RAISE EXCEPTION 'sp_obtener_cliente_saldo debería tener 1 firma y tiene %', v_n; END IF;

    SELECT count(*) INTO v_n FROM pg_proc WHERE proname = 'fn_getclientesaldos_posteomanual';
    IF v_n = 0 THEN RAISE EXCEPTION 'fn_getclientesaldos_posteomanual NO debía dropearse'; END IF;

    SELECT count(*) INTO v_n FROM public.adm_documento_secuencia
     WHERE tipo_documento IN ('PLAN_PAGO','NOTA_COBRO','CARTA_COBRO','CORTE_MASIVO');
    IF v_n < 4 THEN RAISE EXCEPTION 'Faltan series de cobranza (hay %)', v_n; END IF;

    RAISE NOTICE 'H5-SQL OK: SPs legacy fuera, overload 1-arg fuera, tipo_transaccion fuera, 4 series sembradas.';
END $$;
