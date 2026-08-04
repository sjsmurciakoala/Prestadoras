-- Fase 1 estados (docs/PLAN_ESTADOS_FASE1_2026-08-02.md), paso 0: saneo.
--
-- La letra factura.estado es la fuente de escritura y el trigger
-- trg_factura_sync_estado_id deriva estado_id (A=1, C=2, N=3, B=4). La
-- auditoria del 2026-08-02 encontro 11 facturas con estado 'C' y estado_id=1
-- en copia09 (filas tocadas por cargas masivas con triggers deshabilitados /
-- ventana F7). Antes de que el codigo lea por estado_id, letra e id deben
-- cuadrar al 100%.
--
-- Idempotente: recalcula estado_id SOLO donde difiere del mapeo canonico.

BEGIN;

UPDATE public.factura f
   SET estado_id = public.fn_estado_documento_comercial_id_from_codigo(f.estado)
 WHERE f.estado_id IS DISTINCT FROM public.fn_estado_documento_comercial_id_from_codigo(f.estado);

-- Verificacion: no debe quedar ningun par letra/id fuera del mapeo canonico.
DO $$
DECLARE
    v_descuadres bigint;
BEGIN
    SELECT count(*) INTO v_descuadres
    FROM public.factura f
    WHERE f.estado_id IS DISTINCT FROM public.fn_estado_documento_comercial_id_from_codigo(f.estado);

    IF v_descuadres > 0 THEN
        RAISE EXCEPTION 'Saneo incompleto: % facturas siguen descuadradas letra vs estado_id', v_descuadres;
    END IF;
END $$;

COMMIT;
