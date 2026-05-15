-- Drop tarifas_catalogo and its FK/index from configuracion_tasas (clean model)
-- Safe to run multiple times.

ALTER TABLE IF EXISTS public.configuracion_tasas
    DROP CONSTRAINT IF EXISTS fk_configuracion_tasas_tarifas_catalogo;

DROP INDEX IF EXISTS public.ix_configuracion_tasas_cliente_tarifa;
DROP INDEX IF EXISTS public."IX_configuracion_tasas_tarifa_catalogo_id";

ALTER TABLE IF EXISTS public.configuracion_tasas
    DROP COLUMN IF EXISTS tarifa_catalogo_id;

DROP TABLE IF EXISTS public.tarifas_catalogo;
