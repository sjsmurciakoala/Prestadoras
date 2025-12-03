-- View: public.view_bus_codigo

-- DROP VIEW public.view_bus_codigo;

CREATE OR REPLACE VIEW public.view_bus_codigo
 AS
 SELECT c.cod_cuenta AS codcuenta,
    c.nombre AS nombres,
    cc.cuenta AS centro
   FROM cnt_catalogo c
     JOIN cnt_centrocostos_hdr cc ON c.cod_cuenta::text = cc.contable::text;

ALTER TABLE public.view_bus_codigo
    OWNER TO postgres;

