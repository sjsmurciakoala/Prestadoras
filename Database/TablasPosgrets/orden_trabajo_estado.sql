BEGIN;

CREATE TABLE IF NOT EXISTS public.orden_trabajo_estado
(
    codigo               varchar(3)  PRIMARY KEY,
    nombre               varchar(120) NOT NULL,
    permite_asignacion   boolean      NOT NULL DEFAULT true
);

INSERT INTO public.orden_trabajo_estado (codigo, nombre, permite_asignacion) VALUES
    ('P', 'Pendiente', true),
    ('A', 'Asignada', true),
    ('E', 'Ejecutada', false),
    ('C', 'Cancelada', false)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre,
    permite_asignacion = EXCLUDED.permite_asignacion;

COMMIT;
