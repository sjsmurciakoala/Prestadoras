-- Agrega valor disponible al detalle de presupuesto.
-- Regla: valor_disponible = max(valor_proyeccion - valor_real, 0).

ALTER TABLE IF EXISTS public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS valor_disponible NUMERIC(18,4);

UPDATE public.pst_config_presupuesto_dtl
   SET valor_disponible = GREATEST(
       COALESCE(valor_proyeccion, 0) - COALESCE(valor_real, 0),
       0
   )
 WHERE valor_disponible IS NULL
    OR valor_disponible <> GREATEST(
        COALESCE(valor_proyeccion, 0) - COALESCE(valor_real, 0),
        0
    );

ALTER TABLE IF EXISTS public.pst_config_presupuesto_dtl
    ALTER COLUMN valor_disponible SET DEFAULT 0;

ALTER TABLE IF EXISTS public.pst_config_presupuesto_dtl
    ALTER COLUMN valor_disponible SET NOT NULL;

COMMENT ON COLUMN public.pst_config_presupuesto_dtl.valor_disponible IS
    'Saldo disponible del detalle presupuestario: valor_proyeccion menos valor_real, sin permitir valores negativos.';

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

ALTER TABLE IF EXISTS public.view_lista_configuracion_presupuesto
    OWNER TO postgres;
