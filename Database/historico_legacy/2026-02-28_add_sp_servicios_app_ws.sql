-- WS v2: Servicios facturables para la app (sin roles)
-- Devuelve solo servicios marcados con facturable_app = true.

CREATE OR REPLACE FUNCTION public.sp_servicios_app_ws()
    RETURNS TABLE(
        r_servicios_codigo character varying,
        r_descripcion character varying,
        r_app_orden integer,
        r_app_grupo character varying
    )
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000
AS $BODY$
begin
    return query
    select
        s.servicios_codigo,
        s.servicios_descripcioncorta,
        s.app_orden,
        s.app_grupo
    from servicios s
    where s.facturable_app = true
    order by s.app_orden, s.servicios_codigo;
end
$BODY$;

ALTER FUNCTION public.sp_servicios_app_ws()
    OWNER TO postgres;
