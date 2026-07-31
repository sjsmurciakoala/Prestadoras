-- =============================================================================
-- Historico de recorridos de cuadrillas.
--
-- Problema:
--   El API legacy (190.4.5.34:8086, sin codigo fuente disponible) hace UPSERT
--   sobre coordenadas_empleado: mantiene UNA fila por empleado y la sobreescribe
--   en cada reporte. Verificado el 2026-07-28 contra produccion: 7 filas, 7
--   empleados, coordenadas_empleado_id_seq.last_value = 7.
--
--   Resultado: solo existe el punto actual de cada cuadrilla. No se puede saber
--   por donde anduvo, ni a que hora estuvo donde.
--
-- Solucion:
--   Un trigger archiva cada posicion en una tabla aparte ANTES de que la
--   sobreescriban. El API legacy sigue haciendo exactamente lo mismo y no se
--   entera; no hay que tocar ni la app ni el API.
--
-- IMPORTANTE - por que el trigger nunca puede fallar:
--   Esta tabla la escribe un sistema que no controlamos. Si el trigger lanzara
--   una excepcion, el UPDATE del API fallaria y el telefono dejaria de poder
--   reportar su posicion. Por eso el INSERT va dentro de un bloque EXCEPTION:
--   si archivar falla, se registra un WARNING y la operacion original continua.
--   Preferimos perder un punto del historico antes que romper el rastreo.
--
-- Crecimiento esperado:
--   Con la app corregida reportando cada 30s son ~2 880 filas por telefono por
--   dia. Con los 7 que reportan hoy son ~7 M filas al ano; con los 18 usuarios
--   registrados, ~19 M. Aplicar tambien:
--     2026-07-28_retencion_coordenadas_empleado.sql
--
-- Reversion (al final del archivo).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1) Tabla de historico
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.coordenadas_empleado_historico (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    coordenada_id   integer,
    nombre          character varying(50),
    latitud         character varying(25),
    longitud        character varying(25),
    fecha           timestamp without time zone,
    origen          character varying(10),
    registrado_en   timestamp without time zone NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.coordenadas_empleado_historico IS
    'Historico de posiciones de cuadrillas. Lo llena el trigger sobre coordenadas_empleado, que el API legacy sobreescribe.';

-- Consulta tipica: el recorrido de un empleado en un rango de fechas.
CREATE INDEX IF NOT EXISTS ix_coord_hist_nombre_fecha
    ON public.coordenadas_empleado_historico (nombre, fecha DESC);

CREATE INDEX IF NOT EXISTS ix_coord_hist_fecha
    ON public.coordenadas_empleado_historico (fecha DESC);


-- -----------------------------------------------------------------------------
-- 2) Funcion del trigger
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_coordenadas_empleado_archivar()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    -- Un UPDATE que no cambia nada no aporta un punto al recorrido.
    IF TG_OP = 'UPDATE' AND NEW IS NOT DISTINCT FROM OLD THEN
        RETURN NEW;
    END IF;

    BEGIN
        INSERT INTO public.coordenadas_empleado_historico
            (coordenada_id, nombre, latitud, longitud, fecha, origen)
        VALUES
            (NEW.id, NEW.nombre, NEW.latitud, NEW.longitud, NEW.fecha, TG_OP);
    EXCEPTION WHEN OTHERS THEN
        -- Jamas propagar el error: rompería el reporte de ubicación de la app.
        RAISE WARNING 'No se pudo archivar la coordenada de % (%): %',
            NEW.nombre, NEW.id, SQLERRM;
    END;

    RETURN NEW;
END;
$$;


-- -----------------------------------------------------------------------------
-- 3) Trigger
-- -----------------------------------------------------------------------------
DROP TRIGGER IF EXISTS tr_coordenadas_empleado_archivar ON public.coordenadas_empleado;

CREATE TRIGGER tr_coordenadas_empleado_archivar
    AFTER INSERT OR UPDATE ON public.coordenadas_empleado
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_coordenadas_empleado_archivar();


-- -----------------------------------------------------------------------------
-- 4) Semilla: guardar las posiciones actuales para no arrancar en blanco.
--    Idempotente: no vuelve a insertar lo ya archivado.
-- -----------------------------------------------------------------------------
INSERT INTO public.coordenadas_empleado_historico
    (coordenada_id, nombre, latitud, longitud, fecha, origen)
SELECT c.id, c.nombre, c.latitud, c.longitud, c.fecha, 'SEED'
FROM public.coordenadas_empleado c
WHERE NOT EXISTS (
    SELECT 1 FROM public.coordenadas_empleado_historico h
    WHERE h.coordenada_id = c.id AND h.fecha IS NOT DISTINCT FROM c.fecha);

ANALYZE public.coordenadas_empleado_historico;


-- =============================================================================
-- Verificacion (correr despues de instalar y esperar unos minutos con un
-- telefono reportando):
--
--   SELECT nombre, count(*) AS puntos, min(fecha) AS desde, max(fecha) AS hasta
--   FROM public.coordenadas_empleado_historico
--   GROUP BY nombre ORDER BY hasta DESC;
--
-- Si un empleado activo no acumula puntos nuevos, el trigger no esta corriendo
-- o el telefono no esta reportando (revisar coordenadas_empleado.fecha).
-- =============================================================================


-- =============================================================================
-- REVERSION completa:
--
--   DROP TRIGGER IF EXISTS tr_coordenadas_empleado_archivar ON public.coordenadas_empleado;
--   DROP FUNCTION IF EXISTS public.fn_coordenadas_empleado_archivar();
--   -- La tabla se conserva a proposito; borrarla elimina el historico:
--   -- DROP TABLE public.coordenadas_empleado_historico;
-- =============================================================================
