-- =============================================================================
-- Catálogo de retenciones a proveedores (SAR Honduras):
--   cfg_retencion + cfg_retencion_tasa (GLOBALES) + prv_retencion_cuenta (TENANT)
-- Fecha: 2026-08-06
-- Iniciativa: Retenciones a proveedores — Fase F1 (solo el catálogo/mantenimiento).
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost). NUNCA a prod.
--
-- POR QUÉ TRES TABLAS
--   cfg_retencion       -> el CONCEPTO de retención (ISR 12.5%, ISR 1%, ISV retenido).
--                          Qué impuesto retiene (ISR/ISV) y sobre qué base se calcula.
--   cfg_retencion_tasa  -> sus TASAS con VIGENCIA (desde/hasta). Las tasas cambian por
--                          decreto: la vigencia permite resolver "qué % regía tal fecha" y
--                          reconstruir documentos pasados con exactitud (igual que cfg_impuesto_tasa).
--   prv_retencion_cuenta-> la CUENTA CONTABLE del pasivo ("retenciones por pagar") POR EMPRESA.
--                          Es lo único que cambia entre empresas, por eso es tenant-scoped.
--
-- ÁMBITO
--   cfg_retencion / cfg_retencion_tasa: GLOBAL (sin company_id). Los porcentajes los fija la
--   ley (SAR), no la empresa — mismo criterio que cfg_impuesto / cfg_impuesto_tasa.
--   prv_retencion_cuenta: por empresa (company_id) — la cuenta del pasivo sí es de cada quien.
--
-- ⚠️ LAS TASAS SEMILLA DEBEN SER VALIDADAS POR EL CONTADOR.
--   Se siembran las retenciones de ISR confirmadas [OFICIAL]: 12.5% (honorarios/servicios,
--   Art. 50 Ley ISR) y 1% (proveedores, Acuerdo DEI-217-2010), ambas sobre base SIN ISV.
--   La retención de ISV (Acuerdo DEI-215-2010) se siembra SOLO como CONCEPTO, SIN TASA: su
--   porcentaje (≈75% del ISV) es [SECUNDARIO] y debe confirmarse con la SAR antes de activarlo
--   (pendiente D2). Ver la nota de la sección de semilla más abajo.
--
-- Cambio aditivo: tres tablas nuevas. No toca ninguna tabla existente.
-- =============================================================================
BEGIN;

-- Necesaria para el EXCLUDE que impide vigencias solapadas (mezcla '=' de btree
-- con '&&' de gist en una misma constraint).
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- ---------------------------------------------------------------------------
-- 1) La retención (el concepto). GLOBAL.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cfg_retencion (
    id                  SERIAL PRIMARY KEY,
    codigo              VARCHAR(20)  NOT NULL,
    nombre              VARCHAR(80)  NOT NULL,
    descripcion         VARCHAR(250),
    -- base_calculo: sobre qué monto se calcula la retención. Se llama base_calculo (no "base")
    -- porque "base" es palabra reservada de C# y ensuciaría la entidad EF (@base).
    -- TODO (base ISV, F2/D2): 'SIN_ISV' asume ISV al 15% para la retención de ISV
    -- (11.25% sobre el subtotal == 75% del ISV SOLO si el ISV es 15%). Si se retiene sobre
    -- ventas al 18% o cambia la tasa de ISV, revisar si el enum necesita un valor 'ISV'
    -- (base = monto del impuesto). A confirmar con el contador.
    base_calculo        VARCHAR(10)  NOT NULL,
    tipo_impuesto       VARCHAR(4)   NOT NULL,
    activo              BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100),
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100),
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE,

    CONSTRAINT ck_cfg_retencion_base
        CHECK (base_calculo IN ('TOTAL', 'SIN_ISV')),

    CONSTRAINT ck_cfg_retencion_tipo_impuesto
        CHECK (tipo_impuesto IN ('ISR', 'ISV'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_retencion_codigo ON cfg_retencion(codigo);

COMMENT ON TABLE  cfg_retencion IS 'Catálogo de retenciones a proveedores (SAR Honduras). Global: los % los fija la ley, no la empresa.';
COMMENT ON COLUMN cfg_retencion.codigo IS 'Código corto de la retención. Ej: ISR-SERV, ISR-PROV, ISV-RET.';
COMMENT ON COLUMN cfg_retencion.base_calculo IS 'Base de cálculo: TOTAL (monto con ISV) | SIN_ISV (subtotal). ISR va sobre SIN_ISV. Ver TODO sobre ISV en el script.';
COMMENT ON COLUMN cfg_retencion.tipo_impuesto IS 'Impuesto que se retiene: ISR (renta) | ISV (ventas). El SAR los entera y declara por separado.';

-- ---------------------------------------------------------------------------
-- 2) Sus tasas, con vigencia. GLOBAL.
--    A diferencia de cfg_impuesto_tasa NO lleva codigo/nombre/tipo: la retención
--    ES el concepto; la tasa es solo su % a lo largo del tiempo. Por eso el EXCLUDE
--    es por retencion_id (una sola tasa vigente por retención en cada fecha).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cfg_retencion_tasa (
    id                  SERIAL PRIMARY KEY,
    retencion_id        INT          NOT NULL REFERENCES cfg_retencion(id) ON DELETE RESTRICT,
    porcentaje          NUMERIC(5,2) NOT NULL,
    vigencia_desde      DATE         NOT NULL,
    vigencia_hasta      DATE,
    descripcion         VARCHAR(250),
    activo              BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100),
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100),
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE,

    -- Una retención siempre retiene algo: el % es estrictamente > 0 (a diferencia de
    -- cfg_impuesto_tasa, que admite 0 para EXENTO/EXONERADO).
    CONSTRAINT ck_cfg_retencion_tasa_rango
        CHECK (porcentaje > 0 AND porcentaje <= 100),

    CONSTRAINT ck_cfg_retencion_tasa_vigencia
        CHECK (vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde)
);

-- El invariante que de verdad protege: NUNCA puede haber dos tasas de la misma
-- retención con vigencias que se pisen. Sin esto, "¿qué % aplica el 3 de marzo?"
-- no tiene respuesta y el cálculo se vuelve no determinista.
ALTER TABLE cfg_retencion_tasa DROP CONSTRAINT IF EXISTS ex_cfg_retencion_tasa_vigencia;
ALTER TABLE cfg_retencion_tasa ADD CONSTRAINT ex_cfg_retencion_tasa_vigencia
    EXCLUDE USING gist (
        retencion_id WITH =,
        daterange(vigencia_desde, COALESCE(vigencia_hasta, 'infinity'::date), '[]') WITH &&
    );

CREATE INDEX IF NOT EXISTS ix_cfg_retencion_tasa_retencion ON cfg_retencion_tasa(retencion_id);
CREATE INDEX IF NOT EXISTS ix_cfg_retencion_tasa_vigente   ON cfg_retencion_tasa(vigencia_desde, vigencia_hasta) WHERE activo;

COMMENT ON TABLE  cfg_retencion_tasa IS 'Tasas de una retención CON VIGENCIA. La retención es el concepto; esta tabla es su % a lo largo del tiempo. Permite resolver qué % regía en cualquier fecha.';
COMMENT ON COLUMN cfg_retencion_tasa.porcentaje IS 'Porcentaje a retener. Estrictamente > 0 (una retención siempre retiene algo).';
COMMENT ON COLUMN cfg_retencion_tasa.vigencia_hasta IS 'NULL = vigente indefinidamente. Al cambiar el % por decreto NO se edita la fila: se cierra su vigencia y se crea una nueva.';
COMMENT ON CONSTRAINT ex_cfg_retencion_tasa_vigencia ON cfg_retencion_tasa IS 'Impide dos tasas de la misma retención con vigencias solapadas. Sin esto el cálculo sería no determinista.';

-- ---------------------------------------------------------------------------
-- 3) La cuenta contable del pasivo, POR EMPRESA. TENANT (company_id).
--    "Retenciones por pagar" es una cuenta distinta en el plan de cada empresa,
--    por eso esto sí es multiempresa. No se siembra: se configura desde la pantalla.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prv_retencion_cuenta (
    id                  BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    company_id          BIGINT       NOT NULL,
    retencion_id        INT          NOT NULL,
    account_id          BIGINT       NOT NULL,
    activo              BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100),
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100),
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE,

    CONSTRAINT fk_prv_retencion_cuenta_company
        FOREIGN KEY (company_id) REFERENCES cfg_company (company_id) ON DELETE CASCADE,
    CONSTRAINT fk_prv_retencion_cuenta_retencion
        FOREIGN KEY (retencion_id) REFERENCES cfg_retencion (id) ON DELETE RESTRICT,
    CONSTRAINT fk_prv_retencion_cuenta_account
        FOREIGN KEY (account_id) REFERENCES con_plan_cuentas (account_id)
);

-- Una sola cuenta configurada por (empresa, retención).
CREATE UNIQUE INDEX IF NOT EXISTS uq_prv_retencion_cuenta_company_retencion
    ON prv_retencion_cuenta(company_id, retencion_id);

CREATE INDEX IF NOT EXISTS ix_prv_retencion_cuenta_retencion ON prv_retencion_cuenta(retencion_id);

COMMENT ON TABLE  prv_retencion_cuenta IS 'Cuenta contable del pasivo ("retenciones por pagar") por empresa y retención. Tenant-scoped. No se siembra: se configura desde la pantalla.';
COMMENT ON COLUMN prv_retencion_cuenta.account_id IS 'FK a con_plan_cuentas(account_id). Debe ser una cuenta posteable (allows_posting) del pasivo de la empresa.';

-- ---------------------------------------------------------------------------
-- 4) Semilla — retenciones de Honduras
-- ---------------------------------------------------------------------------
-- Concepto de cada retención (global). Idempotente por código.
INSERT INTO cfg_retencion (codigo, nombre, descripcion, base_calculo, tipo_impuesto, usuariocreacion)
VALUES
    ('ISR-SERV', 'ISR 12.5% honorarios y servicios',
     'Retención de ISR sobre honorarios profesionales, dietas, comisiones y servicios técnicos (Art. 50 Ley del ISR). Base sin ISV. Es anticipo del ISR del proveedor.',
     'SIN_ISV', 'ISR', 'seed'),
    ('ISR-PROV', 'ISR 1% proveedores',
     'Retención del 1% de ISR a proveedores de bienes/servicios no sujetos a pagos a cuenta (Acuerdo DEI-217-2010). Base sin ISV. Es anticipo del ISR del proveedor.',
     'SIN_ISV', 'ISR', 'seed'),
    ('ISV-RET', 'ISV retenido (Acuerdo 215-2010)',
     'Retención del ISV sobre servicios del Acuerdo DEI-215-2010 (transporte de carga, limpieza, impresión, seguridad, alquileres). PENDIENTE (D2): confirmar el % con la SAR antes de activar su tasa.',
     'SIN_ISV', 'ISV', 'seed')
ON CONFLICT (codigo) DO NOTHING;

-- Tasas confirmadas [OFICIAL]. vigencia_desde amplia (2010-01-01) para que ningún
-- compromiso existente quede sin tasa aplicable; ajústenla si necesitan precisión histórica.
-- Idempotente: solo siembra si la retención aún no tiene ninguna tasa.
INSERT INTO cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, descripcion, usuariocreacion)
SELECT r.id, v.porcentaje, DATE '2010-01-01', v.descripcion, 'seed'
FROM cfg_retencion r
JOIN (VALUES
    ('ISR-SERV', 12.50, 'ISR 12.5% sobre honorarios y servicios (Art. 50 Ley del ISR).'),
    ('ISR-PROV',  1.00, 'ISR 1% a proveedores no sujetos a pagos a cuenta (Acuerdo DEI-217-2010).')
) AS v(codigo, porcentaje, descripcion) ON v.codigo = r.codigo
WHERE NOT EXISTS (
    SELECT 1 FROM cfg_retencion_tasa t WHERE t.retencion_id = r.id
);

-- ⚠️ ISV-RET (Acuerdo 215-2010): NO se siembra tasa a propósito (pendiente D2).
--   El porcentaje citado por fuentes secundarias es ~75% del ISV (equivalente a 11.25%
--   sobre el subtotal cuando el ISV es 15%), pero NO está confirmado en fuente oficial de la
--   SAR. No se hardcodea como verdad: el contador debe capturar la tasa desde la pantalla de
--   mantenimiento una vez confirmada con la SAR. El concepto ISV-RET ya existe en cfg_retencion
--   (arriba), listo para recibir su tasa. Cuando se confirme, la fila sería del estilo:
--   INSERT INTO cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, descripcion, usuariocreacion)
--   SELECT id, <PORCENTAJE_CONFIRMADO_SAR>, DATE '<vigencia>', 'ISV retenido (Acuerdo 215-2010).', 'seed'
--   FROM cfg_retencion WHERE codigo = 'ISV-RET';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN
-- =============================================================================
-- Conceptos + tasas (ISV-RET debe salir SIN tasa):
-- SELECT r.codigo, r.nombre, r.base_calculo, r.tipo_impuesto,
--        t.porcentaje, t.vigencia_desde, coalesce(t.vigencia_hasta::text,'(vigente)') AS hasta
-- FROM cfg_retencion r LEFT JOIN cfg_retencion_tasa t ON t.retencion_id = r.id
-- ORDER BY r.codigo;
--
-- Prueba del EXCLUDE (debe FALLAR — dos tasas de la misma retención con vigencias que se pisan):
-- INSERT INTO cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde)
-- SELECT id, 13.00, DATE '2026-01-01' FROM cfg_retencion WHERE codigo='ISR-SERV';
--   -> ERROR: conflicting key value violates exclusion constraint
-- =============================================================================
