-- =============================================================================
-- Retencion del historico de recorridos de cuadrillas.
--
-- OJO - a que tabla aplica:
--   NO aplica a coordenadas_empleado. Esa tabla no crece: el API legacy hace
--   UPSERT y mantiene una sola fila por empleado (verificado 2026-07-28 en
--   produccion: 7 filas, 7 empleados, id_seq.last_value = 7).
--
--   Aplica a coordenadas_empleado_historico, la que llena el trigger de
--   2026-07-28_historico_coordenadas_empleado.sql. Esa SI crece:
--   ~2 880 filas por telefono por dia con la app reportando cada 30s.
--   ~7 M filas al ano con los 7 que reportan hoy; ~19 M con los 18 registrados.
--
-- Criterio aplicado:
--   Se conservan 90 dias de recorridos: suficiente para aclarar un reclamo de
--   campo o revisar por donde anduvo una cuadrilla, y acota la tabla.
--
--   El punto ACTUAL de cada empleado no depende de esta tabla (vive en
--   coordenadas_empleado, que el trigger no toca), asi que purgar aqui nunca
--   puede hacer que alguien desaparezca del mapa.
--
--   El borrado va por lotes de 50 000 filas para no tomar un lock largo mientras
--   el trigger sigue insertando.
--
-- Uso:
--   1) Ejecutar este script una vez para crear las funciones.
--   2) Revisar cuanto borraria ANTES de borrar:
--        SELECT * FROM public.fn_coord_historico_purgar_simulacion(90);
--   3) Ejecutar la purga:
--        SELECT public.fn_coord_historico_purgar(90);
--   4) Programarla (ver nota al final).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- Simulacion: NO borra nada, solo informa el impacto.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_coord_historico_purgar_simulacion(
    p_dias_retencion integer DEFAULT 90)
RETURNS TABLE (
    filas_totales     bigint,
    filas_a_borrar    bigint,
    filas_a_conservar bigint,
    fecha_corte       timestamp without time zone,
    tamano_actual     text)
LANGUAGE sql
STABLE
AS $$
    WITH corte AS (
        SELECT (now() - make_interval(days => p_dias_retencion))::timestamp AS f
    )
    SELECT
        (SELECT count(*) FROM public.coordenadas_empleado_historico),
        (SELECT count(*) FROM public.coordenadas_empleado_historico h, corte
          WHERE h.fecha IS NOT NULL AND h.fecha < corte.f),
        (SELECT count(*) FROM public.coordenadas_empleado_historico h, corte
          WHERE h.fecha IS NULL OR h.fecha >= corte.f),
        (SELECT f FROM corte),
        pg_size_pretty(pg_total_relation_size('public.coordenadas_empleado_historico'));
$$;


-- -----------------------------------------------------------------------------
-- Purga real, por lotes. Devuelve la cantidad de filas borradas.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_coord_historico_purgar(
    p_dias_retencion integer DEFAULT 90,
    p_tamano_lote    integer DEFAULT 50000)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    v_corte    timestamp without time zone;
    v_borradas bigint := 0;
    v_lote     bigint;
BEGIN
    IF p_dias_retencion < 7 THEN
        RAISE EXCEPTION 'Retencion demasiado agresiva (% dias). Minimo 7.', p_dias_retencion;
    END IF;

    v_corte := (now() - make_interval(days => p_dias_retencion))::timestamp;

    LOOP
        WITH candidatas AS (
            SELECT h.id
            FROM public.coordenadas_empleado_historico h
            WHERE h.fecha IS NOT NULL
              AND h.fecha < v_corte
            LIMIT p_tamano_lote
        )
        DELETE FROM public.coordenadas_empleado_historico d
        USING candidatas
        WHERE d.id = candidatas.id;

        GET DIAGNOSTICS v_lote = ROW_COUNT;
        v_borradas := v_borradas + v_lote;

        EXIT WHEN v_lote = 0;
    END LOOP;

    ANALYZE public.coordenadas_empleado_historico;

    RAISE NOTICE 'coordenadas_empleado_historico: % filas borradas (corte %)', v_borradas, v_corte;
    RETURN v_borradas;
END;
$$;


COMMENT ON FUNCTION public.fn_coord_historico_purgar(integer, integer) IS
    'Borra recorridos de cuadrillas mas viejos que N dias. No afecta la posicion actual, que vive en coordenadas_empleado.';


-- =============================================================================
-- Como programarla
-- -----------------------------------------------------------------------------
-- Opcion A - pg_cron (si la extension esta disponible en el servidor):
--
--   CREATE EXTENSION IF NOT EXISTS pg_cron;
--   SELECT cron.schedule(
--       'purgar_coord_historico',
--       '0 3 * * 0',                                    -- domingos 3:00 AM
--       $$SELECT public.fn_coord_historico_purgar(90)$$);
--
-- Opcion B - Tarea programada de Windows en el servidor del portal:
--
--   psql -h 172.16.0.9 -U postgres -d siad_v3 ^
--        -c "SELECT public.fn_coord_historico_purgar(90);"
--
-- Correr la simulacion la primera vez y revisar el conteo antes de automatizar.
-- Tras la primera purga grande conviene un VACUUM (FULL solo con la app detenida).
-- =============================================================================
