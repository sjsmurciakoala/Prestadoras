-- =============================================================================
-- Aprobación por niveles — flujo de la REQUISICIÓN (Fase 7, parcial)
-- Fecha: 2026-08-31
-- Diseño: docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- La requisición ya tiene flujo de aprobación (Borrador → En revisión → Aprobada/Rechazada),
-- pero de UN SOLO escalón: quien tenga el permiso `module.inventario.requisiciones.aprobar`
-- aprueba, sin importar el monto y sin dejar rastro de niveles. Este script le da la tabla de
-- flujo para que use el mismo motor que la orden de compra: escalera por monto, aprobadores
-- configurables y bitácora.
--
-- QUÉ SE CREA
--   1) alm_requisicion_aprobacion -> flujo vivo: una fila por nivel exigido de cada requisición.
--
-- POR QUÉ UNA TABLA GEMELA Y NO UNA GENÉRICA
-- La alternativa era una sola `apr_flujo` para todos los documentos, pero eso obliga a migrar y
-- BORRAR `alm_orden_compra_aprobacion`, que ya está aplicada y probada. La gemela conserva la FK
-- compuesta tenant-safe y el ON DELETE CASCADE —que una tabla genérica no puede tener, porque
-- apunta a documentos distintos— y el motor resuelve la tabla desde una lista fija en código.
-- Si más adelante entran más documentos, conviene reconsiderar el modelo.
--
-- NO HAY CHECK QUE AMPLIAR: la requisición ya admite el estado 2 (En revisión), que es el que
-- usa la escalera mientras junta firmas. `alm_requisicion_hdr.aprobado_por` sigue guardando al
-- firmante FINAL, igual que en la orden de compra.
--
-- DEPENDE DE: Database/2026-08-31_apr_niveles_01_estructura.sql (cfg_aprobacion_* y apr_bitacora).
--
-- Cambio ADITIVO y reversible: una tabla nueva (vacía) y sus índices. No altera ni borra una
-- sola fila existente.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Flujo vivo de la requisición: una fila por nivel exigido
--    Gemela de alm_orden_compra_aprobacion: misma forma, mismos estados, misma semántica.
--    Se materializa al enviar a revisión y se BORRA al devolver a borrador (D4).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_requisicion_aprobacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    requisicion_id      INTEGER       NOT NULL,
    nivel               SMALLINT      NOT NULL,
    descripcion         VARCHAR(100)  NOT NULL,
    estado              SMALLINT      NOT NULL DEFAULT 1,
    usuario_firma       VARCHAR(256)  NULL,
    fecha_firma         TIMESTAMP     NULL,
    comentario          VARCHAR(500)  NULL,
    total_documento     NUMERIC(14,2) NOT NULL DEFAULT 0,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    -- Tenant-safe: el renglón del flujo vive SIEMPRE en la empresa de su requisición.
    CONSTRAINT fk_alm_req_aprobacion_hdr
        FOREIGN KEY (company_id, requisicion_id)
        REFERENCES public.alm_requisicion_hdr (company_id, id)
        ON DELETE CASCADE,
    CONSTRAINT uq_alm_req_aprobacion UNIQUE (company_id, requisicion_id, nivel),
    CONSTRAINT ck_alm_req_aprobacion_estado CHECK (estado IN (1, 2, 3, 4)),
    -- Coherencia: un nivel firmado tiene quién y cuándo; uno sin firmar, no.
    CONSTRAINT ck_alm_req_aprobacion_firma CHECK (
        (estado IN (3, 4) AND usuario_firma IS NOT NULL AND fecha_firma IS NOT NULL)
     OR (estado IN (1, 2) AND usuario_firma IS NULL     AND fecha_firma IS NULL)
    )
);
CREATE INDEX IF NOT EXISTS ix_alm_req_aprobacion_doc
    ON public.alm_requisicion_aprobacion (company_id, requisicion_id, nivel);
-- Índice de la bandeja: los niveles pendientes de firma.
CREATE INDEX IF NOT EXISTS ix_alm_req_aprobacion_pendiente
    ON public.alm_requisicion_aprobacion (company_id, estado)
    WHERE estado = 2;

COMMENT ON TABLE  public.alm_requisicion_aprobacion IS 'Flujo de aprobación de una requisición: una fila por nivel exigido. Gemela de alm_orden_compra_aprobacion; el rastro permanente vive en apr_bitacora.';
COMMENT ON COLUMN public.alm_requisicion_aprobacion.estado IS '1 Bloqueado (espera al nivel anterior) · 2 Pendiente (firmable ahora) · 3 Aprobado · 4 Rechazado.';
COMMENT ON COLUMN public.alm_requisicion_aprobacion.descripcion IS 'Snapshot de cfg_aprobacion_nivel.descripcion al enviar: renombrar el nivel después no reescribe la historia.';
COMMENT ON COLUMN public.alm_requisicion_aprobacion.total_documento IS 'Snapshot del total referencial de la requisición con el que se resolvió la escalera.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
--
--   -- La tabla existe y está vacía:
--   SELECT to_regclass('public.alm_requisicion_aprobacion') AS tabla,
--          (SELECT count(*) FROM public.alm_requisicion_aprobacion) AS filas;
--
--   -- La FK apunta a la requisición y es compuesta:
--   SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
--    WHERE conrelid = 'alm_requisicion_aprobacion'::regclass AND contype = 'f';
--
--   -- Las requisiciones existentes NO se tocaron (mismos estados de siempre):
--   SELECT estado, count(*) FROM public.alm_requisicion_hdr GROUP BY estado ORDER BY estado;
--
-- REVERSA (nace vacía: no hay datos que perder)
--   DROP TABLE IF EXISTS public.alm_requisicion_aprobacion;
-- =============================================================================
