\set ON_ERROR_STOP on
\echo '=== DESPUES DEL FIX ==='

-- B) El bloque ya no reparte un correlativo tomado.
DO $$
DECLARE v_act bigint; v_sig bigint;
BEGIN
    SELECT correlativo_actual, correlativo_siguiente INTO v_act, v_sig
    FROM public.sp_adm_obtener_o_reservar_bloque_cai_ruta(2, '00L1', 250, 'TEST', 1::smallint);

    IF v_act <> 256 OR v_sig <> 257 THEN
        RAISE EXCEPTION 'B FALLA: esperaba actual=256/siguiente=257, obtuvo %/%', v_act, v_sig;
    END IF;
    RAISE NOTICE 'B OK: reparte 257 (actual=256, derivado de lo emitido).';
END $$;

-- C) El prepare avanza el contador del bloque y del CAI.
DO $$
DECLARE v_bloque bigint; v_cai bigint;
BEGIN
    PERFORM public.sp_adm_prepare_correlativo_cai_sync(
        2, 103072, 7, 257, '000-020-0100000257', 'uuid-agosto-nuevo', 'TEST');

    SELECT correlativo_actual INTO v_bloque
      FROM public.adm_cai_bloque_reservado WHERE cai_bloque_id = 18;
    SELECT correlativo_actual INTO v_cai
      FROM public.adm_cai_facturacion WHERE cai_id = 7;

    IF v_bloque <> 257 OR v_cai <> 257 THEN
        RAISE EXCEPTION 'C FALLA: bloque=% cai=%, esperaba 257/257', v_bloque, v_cai;
    END IF;
    RAISE NOTICE 'C OK: reservar consume el correlativo (bloque y CAI en 257).';
END $$;

-- D) Anular una reserva huerfana libera el correlativo, sin borrar la fila.
DO $$
DECLARE v_estado varchar;
BEGIN
    UPDATE public.adm_cai_correlativo_emitido
       SET status_id = 0, updated_at = now(), updated_by = 'TEST'
     WHERE cai_correlativo_emitido_id = 149;

    SELECT estado_codigo INTO v_estado
    FROM public.sp_adm_prepare_correlativo_cai_sync(
        2, 103072, 7, 255, '000-020-0100000255', '6eca1285-b451-4e73-850d-80196ad377c9', 'TEST');

    IF v_estado <> 'PENDING_SYNC' THEN
        RAISE EXCEPTION 'D FALLA: estado %', v_estado;
    END IF;
    RAISE NOTICE 'D OK: con la reserva 149 anulada, el 255 se vuelve a emitir (la fila vieja queda como historia).';
END $$;

-- E) Reintento idempotente: misma lectura, misma terna -> sin error.
DO $$
DECLARE v_estado varchar;
BEGIN
    SELECT estado_codigo INTO v_estado
    FROM public.sp_adm_prepare_correlativo_cai_sync(
        2, 103072, 7, 255, '000-020-0100000255', '6eca1285-b451-4e73-850d-80196ad377c9', 'TEST');

    IF v_estado <> 'PENDING_SYNC' THEN
        RAISE EXCEPTION 'E FALLA: estado %', v_estado;
    END IF;
    RAISE NOTICE 'E OK: el reintento del mismo UUID con la misma terna sigue siendo idempotente.';
END $$;

-- F) Regresion: el mismo UUID con otra terna sigue siendo CORRELATIVO_DUPLICADO.
DO $$
BEGIN
    PERFORM public.sp_adm_prepare_correlativo_cai_sync(
        2, 103072, 7, 258, '000-020-0100000258', '6eca1285-b451-4e73-850d-80196ad377c9', 'TEST');
    RAISE EXCEPTION 'F FALLA: se esperaba CORRELATIVO_DUPLICADO.';
EXCEPTION WHEN sqlstate 'P0001' THEN
    IF SQLERRM NOT LIKE 'CORRELATIVO_DUPLICADO%' THEN RAISE; END IF;
    RAISE NOTICE 'F OK: UUID reusado con otra terna sigue rechazado.';
END $$;

-- G) El confirm sigue funcionando y no retrocede el contador.
DO $$
DECLARE v_estado varchar; v_bloque bigint;
BEGIN
    SELECT estado_codigo INTO v_estado
    FROM public.sp_adm_confirmar_correlativo_cai_sync(
        2, 103072, 7, 255, '000-020-0100000255', '6eca1285-b451-4e73-850d-80196ad377c9', 9001, 'TEST');

    SELECT correlativo_actual INTO v_bloque
      FROM public.adm_cai_bloque_reservado WHERE cai_bloque_id = 18;

    IF v_estado <> 'CONFIRMADO' OR v_bloque <> 257 THEN
        RAISE EXCEPTION 'G FALLA: estado=% bloque=%', v_estado, v_bloque;
    END IF;
    RAISE NOTICE 'G OK: confirmar el 255 no retrocede el contador (sigue en 257).';
END $$;

\echo '=== estado final ==='
SELECT cai_correlativo_emitido_id AS id, correlativo, numero_factura, estado_codigo, status_id, factura_id
FROM public.adm_cai_correlativo_emitido ORDER BY cai_correlativo_emitido_id;
SELECT cai_bloque_id, correlativo_actual FROM public.adm_cai_bloque_reservado;
