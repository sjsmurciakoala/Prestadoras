-- Tabla catálogo de tarifas por cliente
CREATE TABLE IF NOT EXISTS public.tarifas_catalogo (
    tarifa_catalogo_id SERIAL PRIMARY KEY,
    nombre             VARCHAR(120) NOT NULL,
    precio_base        NUMERIC(11,4),
    cargo_fijo         NUMERIC(11,4),
    descripcion        VARCHAR(250),
    activo             BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion     TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    fecha_modificacion TIMESTAMP WITHOUT TIME ZONE
);

-- Agregar relación a configuracion_tasas
ALTER TABLE IF EXISTS public.configuracion_tasas
    ADD COLUMN IF NOT EXISTS tarifa_catalogo_id INTEGER;

ALTER TABLE IF EXISTS public.configuracion_tasas
    ADD CONSTRAINT fk_configuracion_tasas_tarifas_catalogo
        FOREIGN KEY (tarifa_catalogo_id)
        REFERENCES public.tarifas_catalogo (tarifa_catalogo_id)
        ON UPDATE NO ACTION
        ON DELETE SET NULL;

-- Índice auxiliar para consultas por cliente + tarifa
CREATE INDEX IF NOT EXISTS ix_configuracion_tasas_cliente_tarifa
    ON public.configuracion_tasas (maestro_cliente_id, tarifa_catalogo_id);
