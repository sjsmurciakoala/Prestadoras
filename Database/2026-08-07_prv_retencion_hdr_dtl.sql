-- =============================================================================
-- Retenciones de proveedores F4: registro fiscal estructurado (hdr/dtl) + folio
-- Fecha: 2026-08-07
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV.
--                  NUNCA a produccion (siad_v3 @ 172.16.0.9) por iniciativa propia.
--
-- POR QUE ESTAS TABLAS
-- F0-F3 ya aplican la retencion y la postean al mayor por el motor config-driven
-- (module='PROV'), pero el dato estructurado (que retencion, a que proveedor, ligada
-- a que poliza) se pierde: solo viaja como lineas debito/credito anonimas. Estas
-- tablas son el LIBRO FISCAL de retenciones: una cabecera por pago con su detalle,
-- ligada a la partida POSTED (partida_id) y al numero_abono. Es el subledger fiscal
-- por proveedor (cod_proveedor + RTN snapshot + total_retenido) que leera F5
-- (constancia CRT + reporte mensual). NO requiere third_party en el mayor.
--
-- ESTADO NUMERICO (regla dura post-merge: nada de estados string nuevos)
--   estado_id SMALLINT: 1 = Vigente, 9 = Anulada. Espejo de EstadoRetencion en C#.
--   Anulacion por estado (no por DELETE); el reverso de la partida lo hace F0.
--
-- IDEMPOTENCIA / UNICIDAD
--   UNIQUE (company_id, numero_orden, numero_abono): un hdr por pago. numero_abono es
--   monotono por (company, orden) — nunca se reusa (uq_prv_compromiso_abono_numero),
--   asi que esta clave es segura sin indice parcial.
--   UNIQUE (company_id, folio): correlativo interno por empresa (NO CAI todavia; F5).
--
-- Cambio ADITIVO y reversible: 3 tablas nuevas. No altera ni borra datos existentes.
-- =============================================================================
BEGIN;

-- 0) Prerrequisitos (fallar temprano y con mensaje claro, no con un error de FK)
DO $$
BEGIN
    IF to_regclass('public.prv_compromiso_hdr') IS NULL THEN
        RAISE EXCEPTION 'Falta prv_compromiso_hdr (modulo proveedores/compromisos).';
    END IF;
    IF to_regclass('public.cfg_retencion') IS NULL THEN
        RAISE EXCEPTION 'Falta cfg_retencion. Aplique antes Database/2026-08-06_cfg_retenciones.sql (F1).';
    END IF;
    IF to_regclass('public.con_plan_cuentas') IS NULL THEN
        RAISE EXCEPTION 'Falta con_plan_cuentas (plan contable).';
    END IF;
END $$;

-- 1) Cabecera fiscal de retencion — una por pago (company_id, numero_orden, numero_abono)
CREATE TABLE IF NOT EXISTS prv_retencion_hdr (
    retencion_hdr_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT        NOT NULL,
    numero_orden         INT           NOT NULL,
    numero_abono         INT           NOT NULL,
    folio                INT           NOT NULL,            -- correlativo interno por empresa (NO CAI aun)
    fecha_emision        DATE          NOT NULL,
    cod_proveedor        VARCHAR(20),
    rtn_proveedor        VARCHAR(20),                       -- snapshot desde prv_compromiso_hdr.rtn
    base_total           NUMERIC(15,2) NOT NULL,            -- bruto del pago sujeto a retencion
    total_retenido       NUMERIC(15,2) NOT NULL,            -- = SUM(dtl.monto_retenido)
    partida_id           BIGINT,                            -- poliza_id POSTED del motor (F0); NULL si encolada
    poliza_number        VARCHAR(40),                       -- snapshot legible de la poliza
    estado_id            SMALLINT      NOT NULL DEFAULT 1,  -- 1 Vigente · 9 Anulada
    cai_id               BIGINT,                            -- F5 (NULL por ahora)
    cai_proveedor        VARCHAR(50),                       -- F5 (NULL por ahora)
    motivo_anulacion     VARCHAR(250),
    usuario_creo         VARCHAR(100)  NOT NULL,
    fecha_creacion       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_modifica     VARCHAR(100),
    fecha_modificacion   TIMESTAMP WITHOUT TIME ZONE,
    usuario_anulacion    VARCHAR(100),
    fecha_anulacion      TIMESTAMP WITHOUT TIME ZONE,
    rowid                UUID          NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_prv_retencion_hdr_estado CHECK (estado_id IN (1, 9)),
    CONSTRAINT ck_prv_retencion_hdr_total  CHECK (total_retenido >= 0),

    -- El hijo vive SIEMPRE en la misma empresa y contra un compromiso existente
    -- (contraparte en BD del query filter tenant; defensa en profundidad).
    CONSTRAINT fk_prv_retencion_hdr_compromiso
        FOREIGN KEY (company_id, numero_orden)
        REFERENCES prv_compromiso_hdr (company_id, numero_orden) ON DELETE RESTRICT,

    CONSTRAINT uq_prv_retencion_hdr_folio  UNIQUE (company_id, folio),
    CONSTRAINT uq_prv_retencion_hdr_pago   UNIQUE (company_id, numero_orden, numero_abono),
    CONSTRAINT uq_prv_retencion_hdr_tenant UNIQUE (company_id, retencion_hdr_id)   -- AK para la FK compuesta del dtl
);

CREATE INDEX IF NOT EXISTS ix_prv_retencion_hdr_company_fecha ON prv_retencion_hdr (company_id, fecha_emision);
CREATE INDEX IF NOT EXISTS ix_prv_retencion_hdr_proveedor     ON prv_retencion_hdr (company_id, cod_proveedor);
CREATE INDEX IF NOT EXISTS ix_prv_retencion_hdr_partida       ON prv_retencion_hdr (company_id, partida_id);

COMMENT ON TABLE  prv_retencion_hdr IS
    'Libro fiscal de retenciones (F4): una cabecera por pago (company_id, numero_orden, numero_abono), ligada a la partida POSTED (partida_id) y al abono. Subledger fiscal por proveedor. estado_id 1=Vigente 9=Anulada.';
COMMENT ON COLUMN prv_retencion_hdr.folio IS 'Correlativo interno por empresa (uq company_id, folio). NO es CAI (F5).';
COMMENT ON COLUMN prv_retencion_hdr.partida_id IS 'poliza_id de la partida POSTED del motor (F0). NULL si el motor encolo por falta de periodo.';
COMMENT ON COLUMN prv_retencion_hdr.base_total IS 'Bruto del pago sujeto a retencion. La base por retencion va en prv_retencion_dtl.base_linea.';

-- 2) Detalle — una fila por retencion aplicada
CREATE TABLE IF NOT EXISTS prv_retencion_dtl (
    retencion_dtl_id     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT        NOT NULL,
    retencion_hdr_id     BIGINT        NOT NULL,
    retencion_id         INT           NOT NULL,
    codigo               VARCHAR(20)   NOT NULL,            -- snapshot cfg_retencion.codigo
    nombre               VARCHAR(150)  NOT NULL,            -- snapshot cfg_retencion.nombre
    porcentaje           NUMERIC(5,2)  NOT NULL,            -- % aplicado (snapshot)
    base_linea           NUMERIC(15,2) NOT NULL,            -- base de ESTA retencion (TOTAL o SIN_ISV)
    monto_retenido       NUMERIC(15,2) NOT NULL,
    account_id           BIGINT        NOT NULL,            -- cuenta del pasivo == cuenta de la linea POSTED

    CONSTRAINT ck_prv_retencion_dtl_monto CHECK (monto_retenido >= 0),

    CONSTRAINT fk_prv_retencion_dtl_hdr
        FOREIGN KEY (company_id, retencion_hdr_id)
        REFERENCES prv_retencion_hdr (company_id, retencion_hdr_id) ON DELETE CASCADE,
    CONSTRAINT fk_prv_retencion_dtl_retencion
        FOREIGN KEY (retencion_id) REFERENCES cfg_retencion (id) ON DELETE RESTRICT,
    CONSTRAINT fk_prv_retencion_dtl_account
        FOREIGN KEY (account_id) REFERENCES con_plan_cuentas (account_id)
);

CREATE INDEX IF NOT EXISTS ix_prv_retencion_dtl_hdr ON prv_retencion_dtl (company_id, retencion_hdr_id);

COMMENT ON TABLE prv_retencion_dtl IS
    'Detalle del libro fiscal de retenciones (F4): una fila por retencion aplicada, con snapshot de codigo/nombre/porcentaje y la cuenta del pasivo (== cuenta de la linea POSTED).';

-- 3) Correlativo de folio por empresa (patron alm_compra_correlativo)
CREATE TABLE IF NOT EXISTS prv_retencion_correlativo (
    company_id     BIGINT   PRIMARY KEY,
    ultimo_folio   INT      NOT NULL DEFAULT 0
);

COMMENT ON TABLE prv_retencion_correlativo IS
    'Contador de folio de retencion por empresa. Se reserva con upsert atomico (INSERT ... ON CONFLICT DO UPDATE RETURNING) en la transaccion del pago.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- 1) Tablas creadas:
-- SELECT to_regclass('public.prv_retencion_hdr'), to_regclass('public.prv_retencion_dtl'),
--        to_regclass('public.prv_retencion_correlativo');
-- 2) Constraints del hdr:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='prv_retencion_hdr'::regclass ORDER BY contype, conname;
--   -> fk_prv_retencion_hdr_compromiso(f), prv_retencion_hdr_pkey(p),
--      uq_prv_retencion_hdr_folio(u), uq_prv_retencion_hdr_pago(u), uq_prv_retencion_hdr_tenant(u),
--      ck_prv_retencion_hdr_estado(c), ck_prv_retencion_hdr_total(c)
-- 3) Indices:
-- SELECT indexname FROM pg_indexes WHERE tablename IN ('prv_retencion_hdr','prv_retencion_dtl') ORDER BY indexname;
-- 4) El CHECK de estado debe FALLAR:
-- INSERT INTO prv_retencion_hdr (company_id, numero_orden, numero_abono, folio, fecha_emision,
--        base_total, total_retenido, estado_id, usuario_creo)
-- VALUES (2, 1, 1, 1, now(), 100, 12.5, 5, 'test');   -- ERROR ck_prv_retencion_hdr_estado (5 no in (1,9))
-- =============================================================================
