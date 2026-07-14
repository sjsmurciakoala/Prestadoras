-- =============================================================================
-- Catálogo de impuestos (SAR Honduras): cfg_impuesto + cfg_impuesto_tasa
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ DOS TABLAS Y NO UNA
-- El impuesto (ISV) es una cosa; sus TASAS son otra, y cambian por decreto.
-- Si guardáramos solo "15%" en una tabla plana, el día que la tasa suba
-- reimprimir una factura vieja daría el impuesto EQUIVOCADO. Por eso las tasas
-- llevan VIGENCIA (desde/hasta): el sistema puede resolver "qué tasa regía el
-- 3 de marzo de 2025" y reconstruir el pasado con exactitud. El SAR lo exige.
--
-- ÁMBITO: GLOBAL (sin company_id)
-- Las tasas del ISV las fija la ley, no la empresa: nadie puede inventarse un
-- ISV del 12%. Se sigue el patrón de los demás catálogos SAR del repo
-- (cfg_tipo_documento_fiscal, cfg_motivo_anulacion, cfg_estado_documento_fiscal),
-- que también son globales. Lo que SÍ es por empresa es qué tasa lleva cada
-- artículo — eso vivirá en alm_articulo (tabla ya multiempresa), no aquí.
--
-- GRAVADO / EXENTO / EXONERADO no son lo mismo, y el SAR los declara por
-- separado (formulario SAR 222 tiene renglones distintos):
--   GRAVADO    -> paga ISV (15% general, 18% selectivo)
--   EXENTO     -> no paga por LEY (Art. 15: agua potable, alcantarillado,
--                 energía, medicinas, canasta básica, educación, salud, libros)
--   EXONERADO  -> pagaría, pero el SAR otorga exoneración por resolución
-- Por eso 'tipo' es una columna y no se deduce de porcentaje = 0.
--
-- ⚠️ LAS TASAS SEMILLA DEBEN SER VALIDADAS POR EL CONTADOR DE LA EMPRESA.
--    Se siembran las vigentes conocidas (15% general / 18% selectivo / exento /
--    exonerado), pero la clasificación fiscal de cada artículo y servicio NO se
--    decide aquí: se configura por artículo.
--
-- Cambio aditivo: dos tablas nuevas. No toca ninguna tabla existente.
-- =============================================================================
BEGIN;

-- Necesaria para el EXCLUDE que impide vigencias solapadas (mezcla '=' de btree
-- con '&&' de gist en una misma constraint).
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- ---------------------------------------------------------------------------
-- 1) El impuesto
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cfg_impuesto (
    id                  SERIAL PRIMARY KEY,
    codigo              VARCHAR(10)  NOT NULL,
    nombre              VARCHAR(80)  NOT NULL,
    descripcion         VARCHAR(250),
    activo              BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100),
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100),
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_impuesto_codigo ON cfg_impuesto(codigo);

COMMENT ON TABLE  cfg_impuesto IS 'Catálogo de impuestos (SAR Honduras). Global: lo fija la ley, no la empresa.';
COMMENT ON COLUMN cfg_impuesto.codigo IS 'Código corto del impuesto. Ej: ISV.';

-- ---------------------------------------------------------------------------
-- 2) Sus tasas, con vigencia
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cfg_impuesto_tasa (
    id                  SERIAL PRIMARY KEY,
    impuesto_id         INT          NOT NULL REFERENCES cfg_impuesto(id) ON DELETE RESTRICT,
    codigo              VARCHAR(20)  NOT NULL,
    nombre              VARCHAR(80)  NOT NULL,
    tipo                VARCHAR(12)  NOT NULL,
    porcentaje          NUMERIC(5,2) NOT NULL DEFAULT 0,
    vigencia_desde      DATE         NOT NULL,
    vigencia_hasta      DATE,
    descripcion         VARCHAR(250),
    activo              BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100),
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100),
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE,

    CONSTRAINT ck_cfg_impuesto_tasa_tipo
        CHECK (tipo IN ('GRAVADO', 'EXENTO', 'EXONERADO')),

    CONSTRAINT ck_cfg_impuesto_tasa_rango
        CHECK (porcentaje >= 0 AND porcentaje <= 100),

    -- Coherencia tipo <-> porcentaje: un GRAVADO sin porcentaje, o un EXENTO
    -- con porcentaje, son datos corruptos que romperían la declaración.
    CONSTRAINT ck_cfg_impuesto_tasa_coherencia
        CHECK ((tipo = 'GRAVADO' AND porcentaje > 0)
            OR (tipo <> 'GRAVADO' AND porcentaje = 0)),

    CONSTRAINT ck_cfg_impuesto_tasa_vigencia
        CHECK (vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde)
);

-- El invariante que de verdad protege: NUNCA puede haber dos tasas del mismo
-- código con vigencias que se pisen. Sin esto, "¿cuál de las dos 15% aplica el
-- 3 de marzo?" no tiene respuesta y el cálculo se vuelve no determinista.
ALTER TABLE cfg_impuesto_tasa DROP CONSTRAINT IF EXISTS ex_cfg_impuesto_tasa_vigencia;
ALTER TABLE cfg_impuesto_tasa ADD CONSTRAINT ex_cfg_impuesto_tasa_vigencia
    EXCLUDE USING gist (
        impuesto_id WITH =,
        codigo      WITH =,
        daterange(vigencia_desde, COALESCE(vigencia_hasta, 'infinity'::date), '[]') WITH &&
    );

CREATE INDEX IF NOT EXISTS ix_cfg_impuesto_tasa_impuesto  ON cfg_impuesto_tasa(impuesto_id);
CREATE INDEX IF NOT EXISTS ix_cfg_impuesto_tasa_vigente   ON cfg_impuesto_tasa(vigencia_desde, vigencia_hasta) WHERE activo;

COMMENT ON TABLE  cfg_impuesto_tasa IS 'Tasas de un impuesto CON VIGENCIA. Las tasas cambian por decreto: la vigencia permite resolver qué tasa regía en cualquier fecha y reconstruir documentos pasados con exactitud.';
COMMENT ON COLUMN cfg_impuesto_tasa.tipo IS 'GRAVADO (paga) | EXENTO (no paga por ley, Art. 15) | EXONERADO (pagaría, pero hay resolución del SAR). El SAR los declara por separado.';
COMMENT ON COLUMN cfg_impuesto_tasa.porcentaje IS 'Porcentaje aplicable. Obligatoriamente > 0 en GRAVADO y = 0 en EXENTO/EXONERADO (lo garantiza ck_cfg_impuesto_tasa_coherencia).';
COMMENT ON COLUMN cfg_impuesto_tasa.vigencia_hasta IS 'NULL = vigente indefinidamente. Al cambiar una tasa por decreto NO se edita la fila: se cierra su vigencia y se crea una nueva. Así el histórico queda intacto.';
COMMENT ON CONSTRAINT ex_cfg_impuesto_tasa_vigencia ON cfg_impuesto_tasa IS 'Impide dos tasas del mismo código con vigencias solapadas. Sin esto el cálculo del impuesto sería no determinista.';

-- ---------------------------------------------------------------------------
-- 3) Semilla — ISV Honduras
-- ---------------------------------------------------------------------------
INSERT INTO cfg_impuesto (codigo, nombre, descripcion, usuariocreacion)
VALUES ('ISV', 'Impuesto Sobre Ventas', 'Impuesto Sobre Ventas de Honduras, administrado por el SAR.', 'seed')
ON CONFLICT (codigo) DO NOTHING;

-- vigencia_desde deliberadamente AMPLIA (2010-01-01): no se verificó la fecha
-- exacta de cada decreto. Se usa una ventana amplia para que ningún documento
-- existente quede sin tasa aplicable. Ajústenla si necesitan precisión histórica.
INSERT INTO cfg_impuesto_tasa (impuesto_id, codigo, nombre, tipo, porcentaje, vigencia_desde, descripcion, usuariocreacion)
SELECT i.id, v.codigo, v.nombre, v.tipo, v.porcentaje, DATE '2010-01-01', v.descripcion, 'seed'
FROM cfg_impuesto i
CROSS JOIN (VALUES
    ('ISV15',     'ISV 15% (general)',   'GRAVADO',   15.00, 'Tasa general del ISV. Aplica a la mayoría de bienes y servicios.'),
    ('ISV18',     'ISV 18% (selectivo)', 'GRAVADO',   18.00, 'Tasa selectiva: bebidas alcohólicas, cerveza, cigarrillos y tabaco; boletos aéreos de clase ejecutiva/primera.'),
    ('EXENTO',    'Exento',              'EXENTO',     0.00, 'Exento por LEY (Art. 15 Ley del ISV): agua potable y alcantarillado, energía eléctrica, medicinas, canasta básica, educación, salud, libros.'),
    ('EXONERADO', 'Exonerado',           'EXONERADO',  0.00, 'Pagaría ISV, pero existe resolución de exoneración del SAR. Se declara por separado del exento.')
) AS v(codigo, nombre, tipo, porcentaje, descripcion)
WHERE i.codigo = 'ISV'
  AND NOT EXISTS (
      SELECT 1 FROM cfg_impuesto_tasa t
      WHERE t.impuesto_id = i.id AND t.codigo = v.codigo
  );

COMMIT;

-- =============================================================================
-- VERIFICACIÓN
-- =============================================================================
-- SELECT i.codigo AS impuesto, t.codigo, t.nombre, t.tipo, t.porcentaje,
--        t.vigencia_desde, coalesce(t.vigencia_hasta::text,'(vigente)') AS hasta
-- FROM cfg_impuesto i JOIN cfg_impuesto_tasa t ON t.impuesto_id = i.id
-- ORDER BY t.porcentaje DESC, t.codigo;
--
-- Prueba del EXCLUDE (debe FALLAR):
-- INSERT INTO cfg_impuesto_tasa (impuesto_id, codigo, nombre, tipo, porcentaje, vigencia_desde)
-- SELECT id, 'ISV15', 'Duplicada solapada', 'GRAVADO', 16.00, DATE '2026-01-01'
-- FROM cfg_impuesto WHERE codigo='ISV';
--   -> ERROR: conflicting key value violates exclusion constraint
-- =============================================================================
