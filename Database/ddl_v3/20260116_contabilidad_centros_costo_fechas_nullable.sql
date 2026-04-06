-- Permite fechas nulas cuando el centro de costo no es periodico.
ALTER TABLE public.con_centro_costo
    ALTER COLUMN start_date DROP NOT NULL,
    ALTER COLUMN end_date DROP NOT NULL;

ALTER TABLE public.con_centro_costo
    ALTER COLUMN start_date DROP DEFAULT,
    ALTER COLUMN end_date DROP DEFAULT;

-- Opcional: limpiar fechas existentes en centros no periodicos.
-- UPDATE public.con_centro_costo
-- SET start_date = NULL,
--     end_date = NULL
-- WHERE is_periodic = false;
