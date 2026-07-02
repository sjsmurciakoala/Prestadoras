-- View: public.view_lista_configuracion_presupuesto

-- DROP VIEW public.view_lista_configuracion_presupuesto;

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
    h.rango_periodo,
    h.fecha_inicia,
    h.fecha_finaliza
   FROM public.pst_config_presupuesto_dtl d
     JOIN public.pst_config_presupuesto_hdr h ON h.id_presupuesto = d.id_presupuesto
  ORDER BY d.id_presupuesto, d.con_cuenta_code;

ALTER TABLE public.view_lista_configuracion_presupuesto
    OWNER TO postgres;
