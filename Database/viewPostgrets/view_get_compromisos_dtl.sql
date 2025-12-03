-- View: public.view_get_compromisos_dtl

-- DROP VIEW public.view_get_compromisos_dtl;

CREATE OR REPLACE VIEW public.view_get_compromisos_dtl
 AS
 SELECT DISTINCT h.numero_orden AS numero_orden_dtl,
    h.cod_presupuestario,
    vd.actividad,
    vd.programa,
    vd.objeto_gasto,
    vd.contable,
    h.monto AS montodtl,
    h.descripcion AS descripciondtl
   FROM prv_compromiso_dtl h
     JOIN view_centro_costo vd ON vd.codigo_costo::text = h.cod_presupuestario::text;

ALTER TABLE public.view_get_compromisos_dtl
    OWNER TO postgres;

