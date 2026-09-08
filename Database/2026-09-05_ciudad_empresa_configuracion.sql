-- =====================================================================
-- 2026-09-05 · Ciudad de la empresa en con_empresa_configuracion
-- ---------------------------------------------------------------------
-- El pagaré a la vista del convenio de pago imprime "con domicilio en el
-- Municipio de <ciudad>" y el lugar donde se firma; el cheque usa el mismo
-- campo como lugar de emisión. La columna estaba vacía, así que ambos
-- documentos salían con la línea en blanco.
--
-- Idempotente: solo escribe la ciudad cuando está vacía, y ubica la empresa
-- por su nombre comercial para no depender del company_id de cada ambiente.
-- No toca estructura: es un dato de configuración.
-- =====================================================================

SET client_encoding TO 'UTF8';

BEGIN;

UPDATE public.con_empresa_configuracion e
   SET ciudad     = 'Puerto Cortés',
       updated_at = now(),
       updated_by = 'script_2026-09-05_ciudad'
  FROM public.cfg_company c
 WHERE c.company_id = e.company_id
   AND c.commercial_name ILIKE 'Aguas de Puerto Cort%'
   AND coalesce(nullif(btrim(e.ciudad), ''), '') = '';

COMMIT;

-- Verificación:
--   SELECT c.company_id, c.commercial_name, e.ciudad
--     FROM public.cfg_company c
--     JOIN public.con_empresa_configuracion e ON e.company_id = c.company_id;
