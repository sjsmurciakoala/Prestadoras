-- View: public.view_get_compromisos_hdr

-- DROP VIEW public.view_get_compromisos_hdr;

CREATE OR REPLACE VIEW public.view_get_compromisos_hdr
 AS
 SELECT DISTINCT h.numero_orden,
    COALESCE(pr.nombre, h.pagar_a) AS proveedor,
    h.rtn,
    h.concepto,
    h.monto,
    h.cuenta_contable,
    COALESCE(pr.cod_proveedor, h.cod_proveedor) AS cod_proveedor
   FROM prv_compromiso_hdr h
     LEFT JOIN prv_proveedores pr ON pr.cod_proveedor::text = h.cod_proveedor::text
  WHERE h.status_transacc IS NULL;

ALTER TABLE public.view_get_compromisos_hdr
    OWNER TO postgres;

