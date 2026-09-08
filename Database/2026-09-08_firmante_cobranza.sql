-- =====================================================================
-- 2026-09-08 · Quién firma por la unidad de cobranza
-- ---------------------------------------------------------------------
-- El pagaré y el compromiso de pago llevan al pie la firma de la empresa.
-- Hasta ahora esa línea salía con un rótulo genérico ("Representante Legal",
-- "Unidad de Cobranza") porque el sistema no guarda el nombre de quien firma.
--
-- La columna es opcional: mientras esté vacía, los documentos siguen imprimiendo
-- el rótulo genérico de hoy. Se edita desde Contabilidad → Empresas →
-- Configuración del sistema.
--
-- Aditivo y re-ejecutable: no toca datos ni estructura existente.
-- =====================================================================

SET client_encoding TO 'UTF8';

BEGIN;

ALTER TABLE public.con_empresa_configuracion
    ADD COLUMN IF NOT EXISTS firmante_cobranza varchar(120);

COMMENT ON COLUMN public.con_empresa_configuracion.firmante_cobranza IS
    'Nombre de quien firma por la unidad de cobranza en el pagaré y el compromiso de pago; vacío imprime el rótulo genérico.';

COMMIT;

-- Verificación:
--   SELECT column_name, data_type, character_maximum_length, is_nullable
--     FROM information_schema.columns
--    WHERE table_name = 'con_empresa_configuracion'
--      AND column_name = 'firmante_cobranza';
