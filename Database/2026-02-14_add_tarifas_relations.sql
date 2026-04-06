-- Relaciones para tarifas / tarifas_contador
-- Ejecutar en PostgreSQL (schema public)

-- 1) FK tarifas -> categoria_servicio
ALTER TABLE IF EXISTS public.tarifas
    ADD CONSTRAINT fk_tarifas_categoria_servicio
    FOREIGN KEY (categoria_id)
    REFERENCES public.categoria_servicio (categoria_servicio_id);

-- 2) FK tarifas_contador -> categoria_servicio (opcional)
ALTER TABLE IF EXISTS public.tarifas_contador
    ADD CONSTRAINT fk_tarifas_contador_categoria_servicio
    FOREIGN KEY (categoria_id)
    REFERENCES public.categoria_servicio (categoria_servicio_id);

-- 3) FK tarifas_contador -> tarifas (opcional, compuesto)
ALTER TABLE IF EXISTS public.tarifas_contador
    ADD CONSTRAINT fk_tarifas_contador_tarifa
    FOREIGN KEY (tipo, categoria_id, codigo)
    REFERENCES public.tarifas (tipo, categoria_id, codigo);

-- Indices recomendados
CREATE INDEX IF NOT EXISTS ix_tarifas_categoria_servicio
    ON public.tarifas (categoria_id);

CREATE INDEX IF NOT EXISTS ix_tarifas_contador_categoria_servicio
    ON public.tarifas_contador (categoria_id);

CREATE INDEX IF NOT EXISTS ix_tarifas_contador_tarifa
    ON public.tarifas_contador (tipo, categoria_id, codigo);
