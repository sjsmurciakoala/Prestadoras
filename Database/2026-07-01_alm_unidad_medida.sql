-- =============================================================================
-- Catálogo de unidades de medida de almacén (alm_unidad_medida)
-- Fecha: 2026-07-01
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- Motivo: en el legacy MySQL bdsimafi NO existía catálogo de unidades; el campo
-- almacen.unidad era texto libre (74% vacío, valores inconsistentes: und/UND./
-- UNI/UNID, números, diámetros). Este catálogo normaliza las unidades con
-- soporte de conversión entre unidades de una misma categoría.
--
-- alm_articulo.unidad_medida (texto libre) se conserva; se agrega la FK opcional
-- unidad_medida_id para migrar gradualmente sin romper datos existentes.
-- =============================================================================

BEGIN;

-- ── 1. Tabla de catálogo ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS alm_unidad_medida (
    id                  SERIAL         PRIMARY KEY,
    company_id          BIGINT         NOT NULL,
    codigo              VARCHAR(10)    NOT NULL,
    nombre              VARCHAR(60)    NOT NULL,
    abreviatura         VARCHAR(10)    NULL,
    permite_decimales   BOOLEAN        NOT NULL DEFAULT true,
    activo              BOOLEAN        NOT NULL DEFAULT true,
    -- Conversión ----------------------------------------------------------------
    categoria           VARCHAR(30)    NULL,
    unidad_base_id      INTEGER        NULL REFERENCES alm_unidad_medida(id) ON DELETE SET NULL,
    factor_conversion   NUMERIC(18,6)  NOT NULL DEFAULT 1,
    -- Auditoría -----------------------------------------------------------------
    usuariocreacion     VARCHAR(100)   NULL,
    fechacreacion       TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)   NULL,
    fechamodificacion   TIMESTAMP      NULL,
    CONSTRAINT uq_alm_unidad_medida_company_codigo UNIQUE (company_id, codigo)
);

CREATE INDEX IF NOT EXISTS ix_alm_unidad_medida_company ON alm_unidad_medida(company_id);

COMMENT ON TABLE  alm_unidad_medida IS 'Catálogo de unidades de medida de almacén, con conversión entre unidades de la misma categoría.';
COMMENT ON COLUMN alm_unidad_medida.permite_decimales IS 'Si false, la unidad sólo admite cantidades enteras (p. ej. UNIDAD, DOCENA).';
COMMENT ON COLUMN alm_unidad_medida.categoria IS 'Agrupa unidades convertibles entre sí: Cantidad, Longitud, Peso, Volumen.';
COMMENT ON COLUMN alm_unidad_medida.unidad_base_id IS 'Unidad base de la categoría (NULL si esta fila ES la base).';
COMMENT ON COLUMN alm_unidad_medida.factor_conversion IS 'Cuántas unidades base equivale 1 de esta unidad (base = 1). Ej. 1 DOCENA = 12 UNIDAD.';

-- ── 2. Seed de unidades comunes (company_id = 2, Empresa Demo) ────────────────
-- Bases de cada categoría (factor 1, sin unidad base).
INSERT INTO alm_unidad_medida (company_id, codigo, nombre, abreviatura, permite_decimales, categoria, factor_conversion, usuariocreacion)
VALUES
    (2, 'UND', 'Unidad',     'und', false, 'Cantidad', 1, 'system'),
    (2, 'MTR', 'Metro',      'm',   true,  'Longitud', 1, 'system'),
    (2, 'KG',  'Kilogramo',  'kg',  true,  'Peso',     1, 'system'),
    (2, 'LT',  'Litro',      'L',   true,  'Volumen',  1, 'system')
ON CONFLICT (company_id, codigo) DO NOTHING;

-- Derivadas (referencian a su base por código; factores exactos).
INSERT INTO alm_unidad_medida (company_id, codigo, nombre, abreviatura, permite_decimales, categoria, unidad_base_id, factor_conversion, usuariocreacion)
SELECT d.company_id, d.codigo, d.nombre, d.abreviatura, d.permite_decimales, d.categoria, b.id, d.factor, 'system'
FROM (VALUES
    (2, 'DOC', 'Docena',      'doc', false, 'Cantidad', 'UND', 12.0),
    (2, 'CM',  'Centímetro',  'cm',  true,  'Longitud', 'MTR', 0.01),
    (2, 'PIE', 'Pie',         'pie', true,  'Longitud', 'MTR', 0.3048),
    (2, 'GR',  'Gramo',       'g',   true,  'Peso',     'KG',  0.001),
    (2, 'ML',  'Mililitro',   'ml',  true,  'Volumen',  'LT',  0.001)
) AS d(company_id, codigo, nombre, abreviatura, permite_decimales, categoria, base_codigo, factor)
JOIN alm_unidad_medida b ON b.company_id = d.company_id AND b.codigo = d.base_codigo
ON CONFLICT (company_id, codigo) DO NOTHING;

-- ── 3. FK opcional desde alm_articulo ────────────────────────────────────────
ALTER TABLE alm_articulo
    ADD COLUMN IF NOT EXISTS unidad_medida_id INTEGER NULL
        REFERENCES alm_unidad_medida(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_alm_articulo_unidad_medida ON alm_articulo(unidad_medida_id);

COMMENT ON COLUMN alm_articulo.unidad_medida_id IS 'FK opcional al catálogo alm_unidad_medida. Convive con el texto libre unidad_medida durante la migración gradual.';

COMMIT;
