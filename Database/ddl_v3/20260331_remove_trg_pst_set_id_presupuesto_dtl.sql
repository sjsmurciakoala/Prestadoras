-- Elimina el disparador de numeracion de detalles de presupuesto.
-- La aplicacion ahora asigna id_presupuesto_dtl explicitamente
-- usando la funcion public.fn_pst_next_id_presupuesto_dtl().

DROP TRIGGER IF EXISTS trg_pst_set_id_presupuesto_dtl
    ON public.pst_config_presupuesto_dtl;

DROP FUNCTION IF EXISTS public.trg_pst_set_id_presupuesto_dtl();
