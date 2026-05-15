-- =============================================================================
-- Estados numéricos — Sprint 2 días 5-6 (anticipados al 2026-05-07)
-- Plan: PLAN_ENTREGA_2026-05-25.md / feedback_estados_numericos.md
--
-- Estrategia: convivencia. Se agregan lookups + columnas `*_id smallint`
-- con backfill desde la string actual. Las columnas string se mantienen
-- (sin tocar) hasta que SPs + C# + Java migren. La eliminación de las
-- columnas string queda para post-25-may.
--
-- Idempotente. Atómico (BEGIN/COMMIT).
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) cfg_estado_documento_comercial — A/C/N para factura y transaccion_abonado
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_estado_documento_comercial (
    estado_id smallint PRIMARY KEY,
    codigo varchar(10) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_estado_documento_comercial (estado_id, codigo, descripcion) VALUES
    (1, 'A', 'Activa / pendiente'),
    (2, 'C', 'Cobrada / compensada'),
    (3, 'N', 'Anulada')
ON CONFLICT (estado_id) DO NOTHING;

-- ============================================================================
-- 2) cfg_estado_correlativo_cai
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_estado_correlativo_cai (
    estado_id smallint PRIMARY KEY,
    codigo varchar(30) NOT NULL UNIQUE,
    descripcion varchar(100) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_estado_correlativo_cai (estado_id, codigo, descripcion) VALUES
    (1, 'PENDING_OFFLINE', 'Reservado offline, no enviado al servidor'),
    (2, 'PENDING_SYNC',    'Enviado pero pendiente de confirmar'),
    (3, 'CONFIRMADO',      'Confirmado contra servidor'),
    (4, 'SYNC_CONFLICT',   'Conflicto de sincronización detectado'),
    (5, 'ANULADO',         'Correlativo anulado por NC')
ON CONFLICT (estado_id) DO NOTHING;

-- ============================================================================
-- 3) cfg_estado_bloque_cai
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_estado_bloque_cai (
    estado_id smallint PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_estado_bloque_cai (estado_id, codigo, descripcion) VALUES
    (1, 'RESERVADO', 'Bloque reservado, disponible para emitir'),
    (2, 'AGOTADO',   'Bloque consumido completamente'),
    (3, 'EXPIRADO',  'Bloque expirado por fecha de vigencia')
ON CONFLICT (estado_id) DO NOTHING;

-- ============================================================================
-- 4) cfg_estado_conflicto_sync
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_estado_conflicto_sync (
    estado_id smallint PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_estado_conflicto_sync (estado_id, codigo, descripcion) VALUES
    (1, 'PENDIENTE', 'Conflicto registrado, sin atender'),
    (2, 'REVISADO',  'Operador revisó y dejó nota'),
    (3, 'CERRADO',   'Conflicto resuelto')
ON CONFLICT (estado_id) DO NOTHING;

-- ============================================================================
-- 5) cfg_codigo_conflicto — taxonomía de errores de sync
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_codigo_conflicto (
    codigo_conflicto_id smallint PRIMARY KEY,
    codigo varchar(40) NOT NULL UNIQUE,
    descripcion varchar(150) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_codigo_conflicto (codigo_conflicto_id, codigo, descripcion) VALUES
    (1, 'SYNC_CONFIRM_ERROR',  'Error confirmando lectura sincronizada'),
    (2, 'SYNC_CONFLICT_TOTAL', 'Total calculado en servidor difiere del enviado por app'),
    (3, 'FACTURA_YA_EMITIDA',  'Ya existe factura para el mismo cliente/anio/mes'),
    (4, 'CAI_VENCIDO',         'CAI usado fuera de fecha límite de emisión'),
    (5, 'CAI_NO_ENCONTRADO',   'CAI referenciado no existe o no está activo'),
    (99, 'OTRO',               'Otro motivo (debe detallarse en payload_resumen)')
ON CONFLICT (codigo_conflicto_id) DO NOTHING;

-- ============================================================================
-- 6) cfg_condicion_lectura — historicomedicion.condicion
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.cfg_condicion_lectura (
    condicion_id smallint PRIMARY KEY,
    codigo varchar(10) NOT NULL UNIQUE,
    descripcion varchar(80) NOT NULL,
    activo boolean NOT NULL DEFAULT true
);

INSERT INTO public.cfg_condicion_lectura (condicion_id, codigo, descripcion) VALUES
    (0, '',     'Sin condición / no aplica'),
    (1, 'N',    'Normal (lectura tomada)'),
    (2, 'MIN',  'Mínimo (sin medidor / consumo cero)'),
    (3, 'PND',  'Pendiente (no se pudo leer)'),
    (4, 'PD',   'Promedio (consumo histórico)'),
    (5, 'R',    'Reposición / repetir lectura')
ON CONFLICT (condicion_id) DO NOTHING;

-- ============================================================================
-- 7) ALTER TABLE: agregar columnas *_id + backfill + NOT NULL + FK
-- ============================================================================

-- 7.1) factura.estado_id
ALTER TABLE public.factura ADD COLUMN IF NOT EXISTS estado_id smallint;

UPDATE public.factura f
SET estado_id = CASE TRIM(UPPER(COALESCE(f.estado, '')))
    WHEN 'A' THEN 1
    WHEN 'C' THEN 2
    WHEN 'N' THEN 3
    ELSE 1
END
WHERE f.estado_id IS NULL;

ALTER TABLE public.factura ALTER COLUMN estado_id SET NOT NULL;
ALTER TABLE public.factura ALTER COLUMN estado_id SET DEFAULT 1;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_factura_estado') THEN
        ALTER TABLE public.factura ADD CONSTRAINT fk_factura_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_documento_comercial(estado_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_factura_estado_id ON public.factura(estado_id);

-- 7.2) transaccion_abonado.estado_id
ALTER TABLE public.transaccion_abonado ADD COLUMN IF NOT EXISTS estado_id smallint;

UPDATE public.transaccion_abonado ta
SET estado_id = CASE TRIM(UPPER(COALESCE(ta.estado, '')))
    WHEN 'A' THEN 1
    WHEN 'C' THEN 2
    WHEN 'N' THEN 3
    ELSE 1
END
WHERE ta.estado_id IS NULL;

ALTER TABLE public.transaccion_abonado ALTER COLUMN estado_id SET NOT NULL;
ALTER TABLE public.transaccion_abonado ALTER COLUMN estado_id SET DEFAULT 1;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_transaccion_abonado_estado') THEN
        ALTER TABLE public.transaccion_abonado ADD CONSTRAINT fk_transaccion_abonado_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_documento_comercial(estado_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_transaccion_abonado_estado_id ON public.transaccion_abonado(estado_id);

-- 7.3) adm_cai_correlativo_emitido.estado_id
ALTER TABLE public.adm_cai_correlativo_emitido ADD COLUMN IF NOT EXISTS estado_id smallint;

UPDATE public.adm_cai_correlativo_emitido ce
SET estado_id = CASE TRIM(UPPER(COALESCE(ce.estado_codigo, '')))
    WHEN 'PENDING_OFFLINE' THEN 1
    WHEN 'PENDING_SYNC'    THEN 2
    WHEN 'CONFIRMADO'      THEN 3
    WHEN 'SYNC_CONFLICT'   THEN 4
    WHEN 'ANULADO'         THEN 5
    ELSE 1
END
WHERE ce.estado_id IS NULL;

ALTER TABLE public.adm_cai_correlativo_emitido ALTER COLUMN estado_id SET NOT NULL;
ALTER TABLE public.adm_cai_correlativo_emitido ALTER COLUMN estado_id SET DEFAULT 1;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_adm_cai_correlativo_emitido_estado') THEN
        ALTER TABLE public.adm_cai_correlativo_emitido ADD CONSTRAINT fk_adm_cai_correlativo_emitido_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_correlativo_cai(estado_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_adm_cai_correlativo_emitido_estado_id ON public.adm_cai_correlativo_emitido(estado_id);

-- 7.4) adm_cai_bloque_reservado.estado_id
ALTER TABLE public.adm_cai_bloque_reservado ADD COLUMN IF NOT EXISTS estado_id smallint;

UPDATE public.adm_cai_bloque_reservado br
SET estado_id = CASE TRIM(UPPER(COALESCE(br.estado_codigo, '')))
    WHEN 'RESERVADO' THEN 1
    WHEN 'AGOTADO'   THEN 2
    WHEN 'EXPIRADO'  THEN 3
    ELSE 1
END
WHERE br.estado_id IS NULL;

ALTER TABLE public.adm_cai_bloque_reservado ALTER COLUMN estado_id SET NOT NULL;
ALTER TABLE public.adm_cai_bloque_reservado ALTER COLUMN estado_id SET DEFAULT 1;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_adm_cai_bloque_reservado_estado') THEN
        ALTER TABLE public.adm_cai_bloque_reservado ADD CONSTRAINT fk_adm_cai_bloque_reservado_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_bloque_cai(estado_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_adm_cai_bloque_reservado_estado_id ON public.adm_cai_bloque_reservado(estado_id);

-- 7.5) adm_lectura_v3_conflicto_sync.estado_id + codigo_conflicto_id
ALTER TABLE public.adm_lectura_v3_conflicto_sync
    ADD COLUMN IF NOT EXISTS estado_id smallint,
    ADD COLUMN IF NOT EXISTS codigo_conflicto_id smallint;

UPDATE public.adm_lectura_v3_conflicto_sync cs
SET estado_id = CASE TRIM(UPPER(COALESCE(cs.estado_codigo, '')))
    WHEN 'PENDIENTE' THEN 1
    WHEN 'REVISADO'  THEN 2
    WHEN 'CERRADO'   THEN 3
    ELSE 1
END
WHERE cs.estado_id IS NULL;

UPDATE public.adm_lectura_v3_conflicto_sync cs
SET codigo_conflicto_id = CASE TRIM(UPPER(COALESCE(cs.codigo_conflicto, '')))
    WHEN 'SYNC_CONFIRM_ERROR'  THEN 1
    WHEN 'SYNC_CONFLICT_TOTAL' THEN 2
    WHEN 'FACTURA_YA_EMITIDA'  THEN 3
    WHEN 'CAI_VENCIDO'         THEN 4
    WHEN 'CAI_NO_ENCONTRADO'   THEN 5
    ELSE 99
END
WHERE cs.codigo_conflicto_id IS NULL;

ALTER TABLE public.adm_lectura_v3_conflicto_sync ALTER COLUMN estado_id SET NOT NULL;
ALTER TABLE public.adm_lectura_v3_conflicto_sync ALTER COLUMN estado_id SET DEFAULT 1;
ALTER TABLE public.adm_lectura_v3_conflicto_sync ALTER COLUMN codigo_conflicto_id SET NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_adm_lectura_v3_conflicto_sync_estado') THEN
        ALTER TABLE public.adm_lectura_v3_conflicto_sync ADD CONSTRAINT fk_adm_lectura_v3_conflicto_sync_estado
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_conflicto_sync(estado_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_adm_lectura_v3_conflicto_sync_codigo') THEN
        ALTER TABLE public.adm_lectura_v3_conflicto_sync ADD CONSTRAINT fk_adm_lectura_v3_conflicto_sync_codigo
            FOREIGN KEY (codigo_conflicto_id) REFERENCES public.cfg_codigo_conflicto(codigo_conflicto_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_adm_lectura_v3_conflicto_sync_estado_id ON public.adm_lectura_v3_conflicto_sync(estado_id);

-- 7.6) historicomedicion.condicion_id
ALTER TABLE public.historicomedicion ADD COLUMN IF NOT EXISTS condicion_id smallint;

UPDATE public.historicomedicion hm
SET condicion_id = CASE TRIM(UPPER(COALESCE(hm.condicion, '')))
    WHEN ''    THEN 0
    WHEN 'N'   THEN 1
    WHEN 'MIN' THEN 2
    WHEN 'PND' THEN 3
    WHEN 'PD'  THEN 4
    WHEN 'R'   THEN 5
    ELSE 0
END
WHERE hm.condicion_id IS NULL;

ALTER TABLE public.historicomedicion ALTER COLUMN condicion_id SET NOT NULL;
ALTER TABLE public.historicomedicion ALTER COLUMN condicion_id SET DEFAULT 0;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_historicomedicion_condicion') THEN
        ALTER TABLE public.historicomedicion ADD CONSTRAINT fk_historicomedicion_condicion
            FOREIGN KEY (condicion_id) REFERENCES public.cfg_condicion_lectura(condicion_id);
    END IF;
END $$;

COMMIT;

-- ============================================================================
-- Verificación post-migración (todos los counts deben ser 0)
-- ============================================================================
SELECT 'factura sin estado_id' AS check, count(*) AS valor FROM public.factura WHERE estado_id IS NULL
UNION ALL
SELECT 'transaccion_abonado sin estado_id', count(*) FROM public.transaccion_abonado WHERE estado_id IS NULL
UNION ALL
SELECT 'adm_cai_correlativo_emitido sin estado_id', count(*) FROM public.adm_cai_correlativo_emitido WHERE estado_id IS NULL
UNION ALL
SELECT 'adm_cai_bloque_reservado sin estado_id', count(*) FROM public.adm_cai_bloque_reservado WHERE estado_id IS NULL
UNION ALL
SELECT 'adm_lectura_v3_conflicto_sync sin estado_id', count(*) FROM public.adm_lectura_v3_conflicto_sync WHERE estado_id IS NULL
UNION ALL
SELECT 'adm_lectura_v3_conflicto_sync sin codigo_conflicto_id', count(*) FROM public.adm_lectura_v3_conflicto_sync WHERE codigo_conflicto_id IS NULL
UNION ALL
SELECT 'historicomedicion sin condicion_id', count(*) FROM public.historicomedicion WHERE condicion_id IS NULL;

-- Contenido de los lookups
SELECT 'cfg_estado_documento_comercial' AS tabla, count(*) AS filas FROM public.cfg_estado_documento_comercial
UNION ALL
SELECT 'cfg_estado_correlativo_cai',     count(*) FROM public.cfg_estado_correlativo_cai
UNION ALL
SELECT 'cfg_estado_bloque_cai',          count(*) FROM public.cfg_estado_bloque_cai
UNION ALL
SELECT 'cfg_estado_conflicto_sync',      count(*) FROM public.cfg_estado_conflicto_sync
UNION ALL
SELECT 'cfg_codigo_conflicto',           count(*) FROM public.cfg_codigo_conflicto
UNION ALL
SELECT 'cfg_condicion_lectura',          count(*) FROM public.cfg_condicion_lectura;
