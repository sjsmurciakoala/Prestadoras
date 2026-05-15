-- Desacopla tarifas_contador de tarifas base
-- Permite registrar combinaciones (tipo, categoria, codigo) independientes.

ALTER TABLE IF EXISTS public.tarifas_contador
    DROP CONSTRAINT IF EXISTS fk_tarifas_contador_tarifa;
