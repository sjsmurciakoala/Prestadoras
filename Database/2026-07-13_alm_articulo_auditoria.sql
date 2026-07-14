-- =============================================================================
-- Almacén: campos de auditoría en el artículo (alm_articulo)
-- Fecha: 2026-07-13
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Agrega la auditoría estándar del sistema (quién/cuándo creó y editó) al artículo,
-- igual que el resto de los catálogos. Se registra en la base y en el backend; NO se
-- muestra en la vista. No se borra ni modifica ningún dato existente.
--
-- Backfill: fechacreacion se toma de la fecha_registro legacy (SIMAFI) cuando existe;
-- el usuario de creación queda NULL en los migrados (desconocido).
-- IDEMPOTENTE: ADD IF NOT EXISTS + UPDATE solo de fechacreacion nula.
-- =============================================================================
BEGIN;

ALTER TABLE alm_articulo
    ADD COLUMN IF NOT EXISTS usuariocreacion     VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS fechacreacion       TIMESTAMP    NULL,
    ADD COLUMN IF NOT EXISTS usuariomodificacion VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS fechamodificacion   TIMESTAMP    NULL;

COMMENT ON COLUMN alm_articulo.usuariocreacion     IS 'Usuario que creó el artículo (auditoría).';
COMMENT ON COLUMN alm_articulo.fechacreacion       IS 'Fecha/hora de creación del artículo (auditoría).';
COMMENT ON COLUMN alm_articulo.usuariomodificacion IS 'Usuario que editó por última vez el artículo (auditoría).';
COMMENT ON COLUMN alm_articulo.fechamodificacion   IS 'Fecha/hora de la última edición del artículo (auditoría).';

-- Backfill de fechacreacion desde la fecha_registro legacy.
UPDATE alm_articulo
SET fechacreacion = fecha_registro::timestamp
WHERE fechacreacion IS NULL AND fecha_registro IS NOT NULL;

COMMIT;
