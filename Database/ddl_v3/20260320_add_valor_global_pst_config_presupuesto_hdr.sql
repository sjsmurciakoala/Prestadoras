-- Agrega valor global al encabezado de configuracion de presupuesto.
-- Regla: no nulo, default en cero para mantener compatibilidad con datos existentes.

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS valor_global NUMERIC(18,4);

UPDATE public.pst_config_presupuesto_hdr
   SET valor_global = 0
 WHERE valor_global IS NULL;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN valor_global SET DEFAULT 0;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_hdr
    ALTER COLUMN valor_global SET NOT NULL;

CREATE OR REPLACE VIEW public.view_lista_configuracion_presupuesto
AS
SELECT d.id_presupuesto,
       d.con_cuenta_code,
       h.valor_global,
       h.valor_disponible,
       d.valor_proyeccion,
       d.valor_real,
       d.valor_disponible AS valor_disponible_detalle,
       h.estado_aprobado,
       d.valor_disponible AS variacion,
       EXTRACT(YEAR FROM h.fecha_inicia)::INTEGER AS anio_presupuesto,
       h.rango_periodo,
       h.fecha_inicia,
       h.fecha_finaliza
  FROM public.pst_config_presupuesto_dtl d
  JOIN public.pst_config_presupuesto_hdr h
    ON h.id_presupuesto = d.id_presupuesto
 ORDER BY d.id_presupuesto, d.con_cuenta_code;
