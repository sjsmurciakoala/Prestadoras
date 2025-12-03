-- View: public.vw_listaplanespago

-- DROP VIEW public.vw_listaplanespago;

CREATE OR REPLACE VIEW public.vw_listaplanespago
 AS
 SELECT cph.correlativo,
    cm.maestro_cliente_nombre AS nombrecliente,
    cph.estadopago AS estado,
    cph.monto AS total,
    cph.fecha,
    cph.id AS idhdr,
    cm.maestro_cliente_clave AS codcliente
   FROM cln_plan_pago_hdr cph
     JOIN cliente_maestro cm ON cph.clienteid = cm.maestro_cliente_id;

ALTER TABLE public.vw_listaplanespago
    OWNER TO postgres;

