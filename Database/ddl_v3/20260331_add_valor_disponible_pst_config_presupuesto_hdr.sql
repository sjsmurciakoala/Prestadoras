-- Agrega y recalcula el saldo disponible del encabezado de presupuesto.
-- El valor disminuye segun la ejecucion real de cuentas con allows_budget = true.

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS valor_disponible NUMERIC(18,4);

WITH totales AS (
    SELECT d.id_presupuesto,
           COALESCE(SUM(d.valor_real), 0) AS valor_real_total
      FROM public.pst_config_presupuesto_dtl d
     GROUP BY d.id_presupuesto
)
UPDATE public.pst_config_presupuesto_hdr h
   SET valor_disponible = GREATEST(COALESCE(h.valor_global, 0) - COALESCE(t.valor_real_total, 0), 0)
  FROM totales t
 WHERE h.id_presupuesto = t.id_presupuesto;

UPDATE public.pst_config_presupuesto_hdr
   SET valor_disponible = COALESCE(valor_global, 0)
 WHERE valor_disponible IS NULL;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN valor_disponible SET DEFAULT 0;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN valor_disponible SET NOT NULL;

COMMENT ON COLUMN public.pst_config_presupuesto_hdr.valor_disponible IS
    'Saldo disponible del presupuesto. Disminuye segun la ejecucion real de cuentas contables con allows_budget=true.';
