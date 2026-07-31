-- =============================================================================
-- Indice para el mapa de ubicacion en vivo de cuadrillas.
--
-- PRIORIDAD: BAJA. Es preventivo, no urgente. Ver la nota de tamano real abajo.
--
-- Contexto:
--   La pagina /mapa consulta "la ultima posicion de cada empleado", que se resuelve
--   con GROUP BY nombre -> MAX(id), y la ejecuta cada 15 segundos por cada usuario
--   que tenga el mapa abierto. La tabla solo tenia el PRIMARY KEY (id).
--
-- Tamano real (verificado 2026-07-28 contra siad_v3_copia09):
--   5 filas / 5 empleados. El API legacy hace UPSERT, no INSERT: mantiene una sola
--   fila por empleado y la sobreescribe. Con ese tamano el indice no cambia nada
--   medible; se deja aplicado porque es barato y porque protege el dia que se
--   cambie el endpoint para guardar historico de recorridos.
--
-- Seguro de re-ejecutar: usa IF NOT EXISTS.
-- =============================================================================

CREATE INDEX IF NOT EXISTS ix_coordenadas_empleado_nombre_id
    ON public.coordenadas_empleado (nombre, id DESC);

-- Util para depurar y para cualquier reporte historico por rango de fechas.
CREATE INDEX IF NOT EXISTS ix_coordenadas_empleado_fecha
    ON public.coordenadas_empleado (fecha DESC);

ANALYZE public.coordenadas_empleado;
