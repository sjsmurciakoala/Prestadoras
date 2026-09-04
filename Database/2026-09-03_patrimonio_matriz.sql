-- El estado de cambios en el patrimonio, como matriz (2026-09-03).
--
-- El juego impreso presenta este estado como una MATRIZ: una fila por movimiento -saldo inicial,
-- aumentos, disminuciones, saldo final- y una columna por componente del patrimonio -capital,
-- reserva legal, reservas, resultados acumulados, total-.
--
-- rep_estado_cambios_patrimonio devuelve justo la transpuesta: una fila POR COMPONENTE, con sus
-- cuatro importes en columnas. Impreso asi, el estado queda girado noventa grados respecto del
-- que firma la empresa.
--
-- Esta funcion no recalcula nada: llama a la existente y la despliega a formato largo
-- (fila, columna, monto), que es lo que consume una tabla de referencias cruzadas. Las columnas
-- no pueden fijarse en la firma porque los componentes salen de la configuracion contable de
-- cada empresa y no son los mismos para todas.
--
-- ADITIVO / bajo riesgo: crea una funcion nueva y no toca la existente, que sigue sirviendo a
-- quien la use.
--
-- LO QUE ESTA FUNCION NO PUEDE DAR: el juego impreso nombra cada movimiento -"Traslado de
-- utilidades", "Pago de dividendos", "Incremento de reservas"-. Esa clasificacion no existe en
-- el dato: la fuente solo distingue aumentos de disminuciones. Nombrarlos exige clasificar los
-- movimientos contra una configuracion que hoy no esta.

BEGIN;

CREATE OR REPLACE FUNCTION public.rep_estado_cambios_patrimonio_matriz(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date)
RETURNS TABLE(
    empresa_nombre text,
    empresa_nombre_legal text,
    empresa_direccion text,
    fila_orden integer,
    fila_nombre text,
    componente_orden integer,
    componente text,
    monto numeric)
LANGUAGE sql
STABLE
AS $function$
    WITH rango AS (
        SELECT
            COALESCE(p_fecha_desde, date_trunc('year', CURRENT_DATE)::date) AS desde,
            COALESCE(p_fecha_hasta, CURRENT_DATE)::date                     AS hasta
    ),
    base AS (
        -- El calculo NO se repite aqui: se toma tal cual de la funcion que ya lo hace.
        -- Se excluye su fila de total porque la matriz suma sus propias columnas; dejarla
        -- entrar la contaria dos veces.
        SELECT e.*
        FROM rango r
        CROSS JOIN LATERAL public.rep_estado_cambios_patrimonio(
            p_company_id, r.desde, r.hasta) e
        WHERE NOT e.es_total
    )
    SELECT
        b.empresa_nombre,
        b.empresa_nombre_legal,
        b.empresa_direccion,
        f.orden::integer   AS fila_orden,
        f.nombre::text     AS fila_nombre,
        b.linea_orden      AS componente_orden,
        b.componente,
        f.monto::numeric
    FROM base b
    CROSS JOIN rango r
    CROSS JOIN LATERAL (
        VALUES
            -- Apertura del ejercicio ANTERIOR: un anio antes del inicio del rango, menos un dia.
            (1, 'Saldo al ' || to_char((r.desde - INTERVAL '1 year' - INTERVAL '1 day')::date,
                                        'DD/MM/YYYY'),            b.saldo_inicial_anterior),
            (2, 'Aumentos del ejercicio anterior',                b.aumentos_anterior),
            (3, 'Disminuciones del ejercicio anterior',           b.disminuciones_anterior),
            (4, 'Saldo al ' || to_char((r.desde - INTERVAL '1 day')::date, 'DD/MM/YYYY'),
                                                                  b.saldo_final_anterior),
            (5, 'Aumentos del ejercicio',                         b.aumentos),
            (6, 'Disminuciones del ejercicio',                    b.disminuciones),
            (7, 'Saldo al ' || to_char(r.hasta, 'DD/MM/YYYY'),    b.saldo_final)
    ) AS f(orden, nombre, monto)
    ORDER BY f.orden, b.linea_orden;
$function$;

-- El dataset del reporte pasa a leer la matriz. Los parametros no cambian -company, desde,
-- hasta-, asi que el catalogo de parametros queda igual.
UPDATE public.rep_catalogo_dataset
   SET origen_clave = 'public.rep_estado_cambios_patrimonio_matriz',
       updated_at = now(),
       updated_by = 'patrimonio-matriz'
 WHERE codigo = 'estado-cambios-patrimonio'
   AND origen_clave = 'public.rep_estado_cambios_patrimonio';

COMMIT;

-- Verificacion:
--   SELECT fila_orden, fila_nombre, count(*) AS componentes, sum(monto) AS total
--   FROM public.rep_estado_cambios_patrimonio_matriz(2, date '2026-01-01', current_date)
--   GROUP BY 1, 2 ORDER BY 1;
--
-- Debe devolver siete filas -los cuatro saldos y los movimientos de los dos ejercicios-, cada
-- una con tantos componentes como tenga configurados la empresa.
