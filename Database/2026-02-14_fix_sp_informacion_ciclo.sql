-- Fix: sp_informacion_ciclo should not depend on historialmes.ruta when period is global.
-- It resolves the route's cycle and returns the open period (cerrarperiodo='P') for that cycle.

CREATE OR REPLACE FUNCTION public.sp_informacion_ciclo(p_ruta character varying)
RETURNS TABLE(r_ciclo integer, r_ano integer, r_mes integer)
LANGUAGE plpgsql
AS $BODY$
DECLARE
    v_ciclo integer;
BEGIN
    SELECT r.codciclo
    INTO v_ciclo
    FROM rutas r
    WHERE r.codruta = p_ruta
    LIMIT 1;

    IF v_ciclo IS NULL THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        CASE
            WHEN h.ciclo ~ '^[0-9]+$' THEN h.ciclo::int
            ELSE v_ciclo
        END AS r_ciclo,
        h.ano::int AS r_ano,
        h.mes::int AS r_mes
    FROM historialmes h
    WHERE h.cerrarperiodo = 'P'
      AND (h.ciclo = lpad(v_ciclo::text, 2, '0') OR h.ciclo = v_ciclo::text)
    ORDER BY h.ano DESC, h.mes DESC
    LIMIT 1;
END
$BODY$;

ALTER FUNCTION public.sp_informacion_ciclo(character varying)
    OWNER TO postgres;
