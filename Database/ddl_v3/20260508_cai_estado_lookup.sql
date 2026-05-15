-- =============================================================================
-- CAI: estado lookup numerico (regla del proyecto: estados como id, no string)
-- Fecha: 2026-05-08
-- Alcance:
--   - cfg_cai_estado: catalogo global (5 estados)
--   - adm_cai_facturacion.estado_id smallint NOT NULL DEFAULT 1
--   - backfill desde status_id + fechas + correlativos
--   - status_id se conserva (compat) hasta retiro en PR aparte
-- Idempotente.
-- =============================================================================

BEGIN;

-- 1. Catalogo de estados CAI (global, no tenant-scoped).
CREATE TABLE IF NOT EXISTS public.cfg_cai_estado (
    cai_estado_id smallint PRIMARY KEY,
    codigo        varchar(20) NOT NULL UNIQUE,
    descripcion   varchar(80) NOT NULL,
    orden         smallint NOT NULL DEFAULT 0,
    activo        boolean NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.cfg_cai_estado IS
    'Catalogo de estados de CAI fiscal (SAR). Lookup numerico segun regla del proyecto.';

INSERT INTO public.cfg_cai_estado (cai_estado_id, codigo, descripcion, orden) VALUES
    (1, 'DISPONIBLE', 'Disponible',                     10),
    (2, 'EN_USO',     'En uso',                         20),
    (3, 'VIGENTE',    'Vigente',                        30),
    (4, 'VENCIDA',    'Vencida',                        40),
    (5, 'ANULADA',    'Anulada',                        50)
ON CONFLICT (cai_estado_id) DO UPDATE
    SET codigo      = EXCLUDED.codigo,
        descripcion = EXCLUDED.descripcion,
        orden       = EXCLUDED.orden;

-- 2. Columna estado_id en adm_cai_facturacion.
ALTER TABLE public.adm_cai_facturacion
    ADD COLUMN IF NOT EXISTS estado_id smallint;

-- 3. Backfill: derivar estado_id ALMACENADO desde estado actual + fechas + correlativos.
--    Reglas de negocio (mismas que en CaiTarifarioService.GuardarCai/CambiarEstado):
--    - status_id = 0                     -> 5 ANULADA (override manual, gana sobre todo)
--    - vigencia_hasta < hoy              -> 4 VENCIDA (la fecha gana sobre el estado declarado)
--    - correlativo_actual >= rango_hasta -> 4 VENCIDA (agotado: no se puede emitir)
--    - correlativo_actual >= rango_desde -> 2 EN_USO  (ya emitio al menos un correlativo)
--    - resto                             -> 1 DISPONIBLE
-- Nota: el SELECT del service ademas re-corrige al leer (CASE WHEN), por si la fecha
-- pasa entre dos writes y el storage queda atras. Esto es defensa profunda.
UPDATE public.adm_cai_facturacion
   SET estado_id = CASE
       WHEN status_id = 0
            THEN 5
       WHEN vigencia_hasta IS NOT NULL AND vigencia_hasta < CURRENT_DATE
            THEN 4
       WHEN correlativo_actual >= rango_hasta
            THEN 4
       WHEN correlativo_actual >= rango_desde
            THEN 2
       ELSE 1
   END
 WHERE estado_id IS NULL;

-- 4. NOT NULL + DEFAULT + FK + CHECK.
ALTER TABLE public.adm_cai_facturacion
    ALTER COLUMN estado_id SET NOT NULL,
    ALTER COLUMN estado_id SET DEFAULT 1;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'fk_adm_cai_facturacion_estado'
           AND conrelid = 'public.adm_cai_facturacion'::regclass
    ) THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT fk_adm_cai_facturacion_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_cai_estado (cai_estado_id);
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_adm_cai_facturacion_estado'
           AND conrelid = 'public.adm_cai_facturacion'::regclass
    ) THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT ck_adm_cai_facturacion_estado
            CHECK (estado_id BETWEEN 1 AND 5);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_adm_cai_facturacion_estado
    ON public.adm_cai_facturacion (company_id, estado_id);

COMMIT;

-- Verificacion
SELECT cai_estado_id, codigo, descripcion FROM public.cfg_cai_estado ORDER BY orden;
SELECT cai_id, codigo_cai, status_id, estado_id, vigencia_hasta, correlativo_actual, rango_desde, rango_hasta
  FROM public.adm_cai_facturacion
 ORDER BY cai_id;
