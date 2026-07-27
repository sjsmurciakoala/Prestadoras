-- =============================================================================
-- Unificación de cobranza — Conciliación automática de recibos pendientes
-- Fecha: 2026-07-27
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md · Backlog pruebas operativas
--
-- Escenario (definido con el usuario): el cliente pide en oficina un "recibo
-- para banco" (parcial o total) — un pendiente 202/'P' que NO toca la factura.
-- Si luego la factura se salda por CUALQUIER canal (caja, WS bancario, o la
-- compensación al emitir la factura del período siguiente), ese papel quedaba
-- huérfano ("no aplicado" para siempre).
--
-- Solución: trigger sobre factura — al pasar a 'C' (saldada/compensada), los
-- pendientes 'P' de ese recibo se anulan automáticamente como "cubiertos".
-- Cubre uniformemente todos los canales sin tocar sp_ban_ws_pagar ni el motor
-- (el sync trigger de transaccion_abonado deriva estado_pago_id=3 ANULADO).
--
-- Idempotente. Aplicar en local (siad_v3_copia09) y test (siad_v3_test).
-- NO aplicar en 0.9.
-- =============================================================================

BEGIN;

CREATE OR REPLACE FUNCTION public.fn_factura_concilia_recibos_pendientes()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE public.transaccion_abonado t
    SET estado = 'A',
        descripcion = 'CUBIERTO: factura saldada — ' || COALESCE(t.descripcion, '')
    WHERE t.company_id = NEW.company_id
      AND t.recibo = NEW.numrecibo
      AND t.tipotransaccion = '202'
      AND t.estado = 'P';
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_factura_concilia_pendientes ON public.factura;
CREATE TRIGGER trg_factura_concilia_pendientes
    AFTER UPDATE OF estado ON public.factura
    FOR EACH ROW
    WHEN (NEW.estado = 'C' AND OLD.estado IS DISTINCT FROM 'C')
    EXECUTE FUNCTION public.fn_factura_concilia_recibos_pendientes();

COMMENT ON FUNCTION public.fn_factura_concilia_recibos_pendientes() IS
'Al saldarse una factura (estado C) por cualquier canal, anula automáticamente
sus recibos pendientes de pago (202/P) como "cubiertos" para que no queden
huérfanos. Unificación cobranza (2026-07-27).';

COMMIT;

-- =============================================================================
-- SMOKE TEST (auto-contenido)
-- =============================================================================
DO $$
DECLARE
    v_factura_id integer;
    v_numrecibo integer;
    v_pendiente integer;
    r record;
BEGIN
    INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
        ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
    VALUES (2, 'SMOKE-CONC', 'SMOKE-CONC', 'F', '2026', '7', current_date, 'A', 'S', 1)
    RETURNING id, numrecibo INTO v_factura_id, v_numrecibo;

    INSERT INTO public.transaccion_abonado
        (company_id, cliente_clave, recibo, tipotransaccion, estado, creditos, debitos, descripcion)
    VALUES (2, 'SMOKE-CONC', v_numrecibo, '202', 'P', 100, 0, 'Recibo pendiente de pago')
    RETURNING ide INTO v_pendiente;

    -- Saldar la factura por cualquier canal
    UPDATE public.factura SET estado = 'C' WHERE id = v_factura_id;

    SELECT estado, estado_pago_id, descripcion INTO r
    FROM public.transaccion_abonado WHERE ide = v_pendiente;

    IF r.estado <> 'A' OR r.estado_pago_id <> 3 OR r.descripcion NOT LIKE 'CUBIERTO:%' THEN
        RAISE EXCEPTION 'Conciliación falló: estado=%, estado_pago=%, desc=%',
            r.estado, r.estado_pago_id, r.descripcion;
    END IF;

    DELETE FROM public.transaccion_abonado WHERE ide = v_pendiente;
    DELETE FROM public.factura WHERE id = v_factura_id;
    RAISE NOTICE 'Smoke OK: pendiente anulado como CUBIERTO al saldarse la factura';
END $$;
