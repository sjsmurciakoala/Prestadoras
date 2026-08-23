\set ON_ERROR_STOP on

-- A) Sin folios pendientes: el ciclo cierra normal.
DO $$
DECLARE v_estado smallint;
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', false);
    SELECT status_id INTO v_estado FROM public.adm_periodo_comercial_ciclo WHERE periodo_ciclo_id = 901;
    IF v_estado <> 2 THEN RAISE EXCEPTION 'A FALLA: estado %', v_estado; END IF;
    RAISE NOTICE 'A OK: sin folios sueltos, cierra.';
    -- se reabre para los casos siguientes
    UPDATE public.adm_periodo_comercial_ciclo SET status_id = 1, fecha_cierre = NULL WHERE periodo_ciclo_id = 901;
END $$;

-- B) Con dos folios reservados sin confirmar del ciclo 19: bloquea.
INSERT INTO public.adm_cai_correlativo_emitido
    (cai_correlativo_emitido_id, company_id, cliente_id, correlativo, numero_factura, factura_id, estado_codigo, status_id)
VALUES
    (900, 2, 103072, 255, '000-020-0100000255', NULL, 'PENDING_SYNC', 1),
    (901, 2, 103514, 256, '000-020-0100000256', NULL, 'PENDING_SYNC', 1);

DO $$
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', false);
    RAISE EXCEPTION 'B FALLA: se esperaba CICLO_FOLIOS_SIN_CONFIRMAR.';
EXCEPTION WHEN sqlstate 'P0001' THEN
    IF SQLERRM NOT LIKE 'CICLO_FOLIOS_SIN_CONFIRMAR%' THEN RAISE; END IF;
    RAISE NOTICE 'B OK: %', SQLERRM;
END $$;

-- C) Los mismos folios, pero forzando: cierra igual (la decisión es humana).
DO $$
DECLARE v_estado smallint;
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', true);
    SELECT status_id INTO v_estado FROM public.adm_periodo_comercial_ciclo WHERE periodo_ciclo_id = 901;
    IF v_estado <> 2 THEN RAISE EXCEPTION 'C FALLA: estado %', v_estado; END IF;
    RAISE NOTICE 'C OK: con p_forzar cierra igual.';
    UPDATE public.adm_periodo_comercial_ciclo SET status_id = 1, fecha_cierre = NULL WHERE periodo_ciclo_id = 901;
END $$;

-- D) Un folio de OTRO ciclo (21) no bloquea el cierre del 19.
DELETE FROM public.adm_cai_correlativo_emitido;
INSERT INTO public.adm_cai_correlativo_emitido
    (cai_correlativo_emitido_id, company_id, cliente_id, correlativo, numero_factura, factura_id, estado_codigo, status_id)
VALUES (902, 2, 104000, 300, '000-020-0100000300', NULL, 'PENDING_SYNC', 1);

DO $$
DECLARE v_estado smallint;
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', false);
    SELECT status_id INTO v_estado FROM public.adm_periodo_comercial_ciclo WHERE periodo_ciclo_id = 901;
    IF v_estado <> 2 THEN RAISE EXCEPTION 'D FALLA: estado %', v_estado; END IF;
    RAISE NOTICE 'D OK: un folio del ciclo 21 no frena el cierre del 19.';
    UPDATE public.adm_periodo_comercial_ciclo SET status_id = 1, fecha_cierre = NULL WHERE periodo_ciclo_id = 901;
END $$;

-- E) Reservas que NO cuentan: anuladas, confirmadas y sin cliente.
DELETE FROM public.adm_cai_correlativo_emitido;
INSERT INTO public.adm_cai_correlativo_emitido
    (cai_correlativo_emitido_id, company_id, cliente_id, correlativo, numero_factura, factura_id, estado_codigo, status_id)
VALUES
    (903, 2, 103072, 257, '000-020-0100000257', NULL,  'PENDING_SYNC', 0),   -- anulada
    (904, 2, 103072, 258, '000-020-0100000258', 12345, 'CONFIRMADO',   1),   -- ya facturada
    (905, 2, NULL,   259, '000-020-0100000259', NULL,  'PENDING_SYNC', 1);   -- sin cliente

DO $$
DECLARE v_estado smallint;
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', false);
    SELECT status_id INTO v_estado FROM public.adm_periodo_comercial_ciclo WHERE periodo_ciclo_id = 901;
    IF v_estado <> 2 THEN RAISE EXCEPTION 'E FALLA: estado %', v_estado; END IF;
    RAISE NOTICE 'E OK: anuladas, confirmadas y sin cliente no bloquean.';
END $$;

-- F) Regresión: cerrar dos veces sigue avisando que ya está cerrado.
DO $$
BEGIN
    PERFORM public.sp_adm_periodo_ciclo_cerrar(2, 901, 'TEST', false);
    RAISE EXCEPTION 'F FALLA: se esperaba CICLO_YA_CERRADO.';
EXCEPTION WHEN sqlstate 'P0001' THEN
    IF SQLERRM NOT LIKE 'CICLO_YA_CERRADO%' THEN RAISE; END IF;
    RAISE NOTICE 'F OK: %', SQLERRM;
END $$;
