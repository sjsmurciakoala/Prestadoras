-- WS v2: Cobros adicionales por servicio (sin roles)
-- Devuelve un registro por concepto/categoria/servicio.

CREATE OR REPLACE FUNCTION public.sp_cobros_adicionales_ws_v2()
    RETURNS TABLE(
        r_concepto_id integer,
        r_categoria_id integer,
        r_servicios_codigo character varying,
        r_porcentaje numeric,
        r_aplica_descuento boolean,
        r_concepto character varying
    )
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000
AS $BODY$
begin
    return query
    select
        cca.concepto_id,
        cca.categoria_id,
        s.servicios_codigo,
        f.porcentaje,
        cca.aplica_descuento,
        co.concepto
    from public.configuracion_cobros_adicionales cca
    inner join configuracion_cobros_adicionales_detalle f
        on f.configuracion_cobro_adicional_ide = cca.ide
    inner join servicios s
        on f.servicio_id = s.servicios_id
    left join concepto_cobro_adicional co
        on co.ide = cca.concepto_id
    where s.facturable_app = true
    order by cca.concepto_id, cca.categoria_id, s.servicios_codigo;
end
$BODY$;

ALTER FUNCTION public.sp_cobros_adicionales_ws_v2()
    OWNER TO postgres;
