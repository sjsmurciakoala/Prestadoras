-- View: public.view_centro_costo

-- DROP VIEW public.view_centro_costo;

CREATE OR REPLACE VIEW public.view_centro_costo
 AS
 SELECT h.cuenta AS codigo_costo,
    s.nombre AS actividad,
    gr.nombre AS programa,
    h.nombre AS objeto_gasto,
    h.contable
   FROM cnt_centrocostos_hdr h
     JOIN cnt_centro_costos_subgrupo s ON s.codccg::integer = h.codccg::integer AND s.codsccg::integer = h.codsccg::integer
     JOIN cnt_centro_costos_grupo gr ON gr.codccg::text = h.codccg::text
  ORDER BY h.cuenta;

ALTER TABLE public.view_centro_costo
    OWNER TO postgres;

