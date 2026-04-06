-- Fix: sp_cobros_adicionales_ws debe mapear servicios por roles (servicios_roles_ws)
-- y no por codigos hardcodeados (101..102).
-- El app usa PorcentajeAgua y PorcentajeAlcantarilla para calcular cobros adicionales.

CREATE OR REPLACE FUNCTION public.sp_cobros_adicionales_ws()
    RETURNS TABLE(
        r_concepto_id integer,
        r_categoria_id integer,
        r_porcentaje_agua numeric,
        r_porcentaje_alcantarillado numeric,
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
        max(case when sr.rol = 'agua' then f.porcentaje end) as agua,
        max(case when sr.rol = 'alcantarillado' then f.porcentaje end) as alcantarillado,
        cca.aplica_descuento,
        co.concepto
    from public.configuracion_cobros_adicionales cca
    inner join configuracion_cobros_adicionales_detalle f
        on f.configuracion_cobro_adicional_ide = cca.ide
    inner join servicios s
        on f.servicio_id = s.servicios_id
    inner join servicios_roles_ws sr
        on sr.servicios_codigo = s.servicios_codigo
       and sr.activo = true
       and sr.rol in ('agua','alcantarillado')
    left join concepto_cobro_adicional co
        on co.ide = cca.concepto_id
    group by cca.concepto_id, cca.categoria_id, cca.aplica_descuento, co.concepto;
end
$BODY$;

ALTER FUNCTION public.sp_cobros_adicionales_ws()
    OWNER TO postgres;
