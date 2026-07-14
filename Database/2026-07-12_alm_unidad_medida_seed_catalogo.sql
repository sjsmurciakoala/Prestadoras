-- =============================================================================
-- Almacén: catálogo de unidades de medida estándar (alm_unidad_medida)
-- Fecha: 2026-07-12
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- Agrega unidades de uso común a cada categoría existente (Peso, Volumen, Longitud,
-- Cantidad), enlazadas a su unidad base (KG, LT, MTR, UND) por factor de conversión
-- (factor = cuántas unidades base equivale 1 de la unidad derivada).
--
-- IDEMPOTENTE: ON CONFLICT (company_id, codigo) DO NOTHING → no duplica ni modifica
-- las unidades existentes. La unidad base se resuelve por su código dentro de la
-- empresa, por lo que es portable entre bases (no depende de ids concretos).
-- Empresa objetivo: company_id = 2 (la que tiene el catálogo de almacén).
-- =============================================================================
BEGIN;

WITH nuevas (codigo, nombre, abreviatura, categoria, base_codigo, factor, permite_decimales) AS (
    VALUES
        -- Peso (base: KG)
        ('LB',   'Libra',        'lb',  'Peso',     'KG',   0.453592, true),
        ('OZ',   'Onza',         'oz',  'Peso',     'KG',   0.028350, true),
        ('TON',  'Tonelada',     't',   'Peso',     'KG',   1000.0,   true),
        ('QQ',   'Quintal',      'qq',  'Peso',     'KG',   45.359200, true),
        ('MG',   'Miligramo',    'mg',  'Peso',     'KG',   0.000001, true),
        -- Volumen (base: LT)
        ('GAL',  'Galón',        'gal', 'Volumen',  'LT',   3.785412, true),
        ('M3',   'Metro cúbico', 'm3',  'Volumen',  'LT',   1000.0,   true),
        -- Longitud (base: MTR)
        ('KM',   'Kilómetro',    'km',  'Longitud', 'MTR',  1000.0,   true),
        ('MM',   'Milímetro',    'mm',  'Longitud', 'MTR',  0.001000, true),
        ('PUL',  'Pulgada',      'in',  'Longitud', 'MTR',  0.025400, true),
        ('YD',   'Yarda',        'yd',  'Longitud', 'MTR',  0.914400, true),
        -- Cantidad (base: UND) — sin decimales
        ('CIEN', 'Ciento',       'c',   'Cantidad', 'UND',  100.0,    false),
        ('MIL',  'Millar',       'mil', 'Cantidad', 'UND',  1000.0,   false),
        ('PAR',  'Par',          'par', 'Cantidad', 'UND',  2.0,      false),
        ('GRS',  'Gruesa',       'grs', 'Cantidad', 'UND',  144.0,    false)
)
-- Nota: aplicar DESPUÉS de 2026-07-12_alm_categoria_unidad.sql (que crea el catálogo).
-- La categoría se guarda como FK (categoria_id), resuelta por nombre contra el catálogo.
INSERT INTO alm_unidad_medida
    (company_id, codigo, nombre, abreviatura, categoria_id, permite_decimales, activo,
     unidad_base_id, factor_conversion, usuariocreacion, fechacreacion)
SELECT 2, n.codigo, n.nombre, n.abreviatura, cat.id, n.permite_decimales, true,
       b.id, n.factor::numeric(18,6), 'seed_catalogo', (now() AT TIME ZONE 'utc')
FROM nuevas n
JOIN alm_unidad_medida b ON b.company_id = 2 AND b.codigo = n.base_codigo
LEFT JOIN alm_categoria_unidad cat ON cat.company_id = 2 AND lower(cat.nombre) = lower(n.categoria)
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;
