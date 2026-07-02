-- Agrega estado de aprobacion en encabezado de presupuesto.
-- Regla: FALSE = no aprobado (permite editar detalle), TRUE = aprobado (solo gestionar).

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS estado_aprobado BOOLEAN;

UPDATE public.pst_config_presupuesto_hdr
   SET estado_aprobado = FALSE
 WHERE estado_aprobado IS NULL;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN estado_aprobado SET DEFAULT FALSE;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN estado_aprobado SET NOT NULL;
