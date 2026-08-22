-- =============================================================================
-- Configuración: catálogo de formatos fiscales (cfg_formato_fiscal)
-- Fecha: 2026-08-22
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) y en el servidor
--
-- POR QUÉ
-- En la recepción de compra, "No. factura (SAR)" y "CAI" son dos cajas de texto libre
-- sin ninguna validación: el servicio solo hace Trim() y trunca en silencio a 30/50
-- caracteres. Cada digitador teclea el número del proveedor a su manera. Este catálogo
-- guarda, por empresa, la MÁSCARA de cada campo fiscal; de ella se derivan el ejemplo,
-- la expresión regular de validación y la máscara de DevExpress.
--
-- Notación de la máscara (la que teclea el usuario en el mantenimiento):
--   '#' dígito · 'X' letra o dígito · 'H' hexadecimal · cualquier otro carácter es literal.
--
-- ADITIVO / bajo riesgo: CREATE TABLE nueva. No borra ni reescribe datos, no altera
-- ninguna tabla existente, no crea FK entrantes ni salientes. SIN seed: la semilla va
-- en 2026-08-22_cfg_formato_fiscal_seed.sql y es opcional.
--
-- COMPATIBILIDAD: con el catálogo vacío (o con la fila inactiva) los dos campos siguen
-- siendo texto libre, exactamente como hoy. El SQL sin el binario es inocuo.
--
-- IDEMPOTENTE: CREATE ... IF NOT EXISTS.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS cfg_formato_fiscal (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    codigo              VARCHAR(30)   NOT NULL,
    nombre              VARCHAR(60)   NOT NULL,
    mascara             VARCHAR(80)   NOT NULL,
    patron              VARCHAR(200)  NULL,
    modo_validacion     SMALLINT      NOT NULL DEFAULT 3,
    obligatorio         BOOLEAN       NOT NULL DEFAULT false,
    normalizar          BOOLEAN       NOT NULL DEFAULT true,
    mayusculas          BOOLEAN       NOT NULL DEFAULT true,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    CONSTRAINT uq_cfg_formato_fiscal_company_codigo UNIQUE (company_id, codigo),
    CONSTRAINT ck_cfg_formato_fiscal_modo CHECK (modo_validacion IN (1, 2, 3))
);

CREATE INDEX IF NOT EXISTS ix_cfg_formato_fiscal_company ON cfg_formato_fiscal(company_id);

COMMENT ON TABLE cfg_formato_fiscal IS 'Catálogo por empresa del formato de los códigos fiscales que se transcriben del proveedor (No. de factura SAR, CAI). Una fila por campo; extensible a otros campos sin tocar código.';
COMMENT ON COLUMN cfg_formato_fiscal.codigo IS 'Identificador del campo: NUMERO_SAR, CAI, ... Es la clave que usa la pantalla para pedir su formato.';
COMMENT ON COLUMN cfg_formato_fiscal.nombre IS 'Etiqueta visible del campo, tal como aparece en la vista que lo captura.';
COMMENT ON COLUMN cfg_formato_fiscal.mascara IS 'Máscara de captura. # = dígito, X = letra o dígito, H = hexadecimal; cualquier otro carácter es literal. Ej: ###-###-##-########';
COMMENT ON COLUMN cfg_formato_fiscal.patron IS 'Expresión regular de validación. NULL = se deriva de la máscara. Se contrasta contra el valor ya formateado (con literales).';
COMMENT ON COLUMN cfg_formato_fiscal.modo_validacion IS '1 = no valida, 2 = advierte y deja guardar, 3 = bloquea el guardado.';
COMMENT ON COLUMN cfg_formato_fiscal.obligatorio IS 'true = la vista exige el dato para guardar el documento.';
COMMENT ON COLUMN cfg_formato_fiscal.normalizar IS 'true = el valor se guarda sin separadores (solo letras y dígitos) y se muestra con la máscara.';
COMMENT ON COLUMN cfg_formato_fiscal.mayusculas IS 'true = el valor se normaliza a mayúsculas antes de guardarlo.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- SELECT to_regclass('public.cfg_formato_fiscal') AS tabla;   -- NULL = falta
--
-- SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns
--  WHERE table_name = 'cfg_formato_fiscal'
--  ORDER BY ordinal_position;                                  -- esperado: 15 columnas
--
-- SELECT conname, contype FROM pg_constraint
--  WHERE conrelid = 'public.cfg_formato_fiscal'::regclass
--  ORDER BY conname;   -- esperado: cfg_formato_fiscal_pkey, ck_..._modo, uq_..._company_codigo
-- =============================================================================
