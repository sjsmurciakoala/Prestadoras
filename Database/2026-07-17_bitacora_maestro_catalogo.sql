-- =============================================================================
-- Bitácora de maestros: catálogo configurable de entidades auditables (por empresa).
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) y en el servidor (prod).
-- Mueve la lista blanca de entidades auditables desde código a una tabla editable
-- desde la UI. Tenant-scoped (company_id). SIN disparadores: la captura sigue en el
-- interceptor de EF (capa de aplicación); esta tabla es solo datos.
-- La semilla inicial (los 9 maestros base) la aplica el backend por empresa la primera
-- vez que se abre la configuración (auto-seed idempotente), así que este script solo
-- crea la estructura vacía. IDEMPOTENTE.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.bitacora_maestro_catalogo (
    id                  SERIAL PRIMARY KEY,
    company_id          BIGINT       NOT NULL,
    tabla               VARCHAR(128) NOT NULL,   -- nombre técnico (GetTableName)
    nombre              VARCHAR(150) NOT NULL,   -- nombre amigable
    modulo              VARCHAR(80)  NOT NULL,
    activo              BOOLEAN      NOT NULL DEFAULT TRUE,
    usuariocreacion     VARCHAR(100) NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion VARCHAR(100) NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL
);

-- Único por empresa + tabla (case-insensitive).
CREATE UNIQUE INDEX IF NOT EXISTS uq_bitacora_maestro_catalogo_company_tabla
    ON public.bitacora_maestro_catalogo (company_id, lower(tabla));
CREATE INDEX IF NOT EXISTS ix_bitacora_maestro_catalogo_company
    ON public.bitacora_maestro_catalogo (company_id);

COMMENT ON TABLE public.bitacora_maestro_catalogo IS
    'Catálogo por empresa de entidades (tablas) auditables por la bitácora de maestros. Editable desde la UI. Sin triggers.';

COMMIT;
