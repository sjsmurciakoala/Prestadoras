-- =============================================================================
-- Bitácora de maestros: auditoría de acciones de usuario sobre tablas maestras.
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) y en el servidor (prod).
-- IDEMPOTENTE: CREATE TABLE IF NOT EXISTS + índices IF NOT EXISTS. No borra ni
-- modifica datos existentes.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.bitacora_maestros (
    id             BIGSERIAL PRIMARY KEY,
    company_id     BIGINT       NOT NULL,
    entidad        VARCHAR(100) NOT NULL,   -- nombre de TABLA: 'cliente_maestro', 'alm_articulo', ...
    accion         VARCHAR(10)  NOT NULL,   -- 'CREAR' | 'EDITAR' | 'ELIMINAR'
    registro_id    VARCHAR(60)  NOT NULL,   -- PK del registro afectado (string: soporta int/uuid/compuesta)
    registro_desc  VARCHAR(250) NULL,       -- descriptor legible (ej. nombre), best-effort
    cambios        JSONB        NULL,       -- [{ "campo": "...", "antes": ..., "despues": ... }]
    usuario        VARCHAR(100) NOT NULL,   -- User.Identity.Name ?? 'system'
    ip             VARCHAR(45)  NULL,       -- RemoteIpAddress (IPv4/IPv6)
    fecha          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_fecha    ON public.bitacora_maestros (company_id, fecha);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_entidad  ON public.bitacora_maestros (company_id, entidad);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_usuario  ON public.bitacora_maestros (company_id, usuario);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_registro ON public.bitacora_maestros (company_id, entidad, registro_id);

CREATE TABLE IF NOT EXISTS public.bitacora_maestro_config (
    id                  SERIAL PRIMARY KEY,
    company_id          BIGINT       NOT NULL,
    entidad             VARCHAR(100) NOT NULL,   -- nombre de TABLA, del catálogo AuditableMaestros
    habilitado          BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_crear        BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_editar       BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_eliminar     BOOLEAN      NOT NULL DEFAULT TRUE,
    usuariocreacion     VARCHAR(100) NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion VARCHAR(100) NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_bitacora_maestro_config_company_entidad
    ON public.bitacora_maestro_config (company_id, entidad);

COMMENT ON TABLE public.bitacora_maestros IS 'Auditoría append-only de acciones de usuario sobre tablas maestras.';
COMMENT ON TABLE public.bitacora_maestro_config IS 'Configura, por empresa, qué entidades maestras se auditan.';

COMMIT;
