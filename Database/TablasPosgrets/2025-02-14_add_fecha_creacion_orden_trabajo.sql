-- Agrega columna de auditoría para registrar cuándo se creó cada orden de trabajo.
ALTER TABLE IF EXISTS public.orden_trabajo
    ADD COLUMN IF NOT EXISTS fecha_creacion timestamp without time zone NOT NULL DEFAULT NOW();

-- Normaliza los registros existentes tomando la fecha operativa como aproximación de creación.
UPDATE public.orden_trabajo
SET fecha_creacion = COALESCE(fecha_creacion, fecha::timestamp)
WHERE fecha_creacion IS NULL;
