BEGIN;

ALTER TABLE IF EXISTS public.rep_dataset_parametro
    ADD COLUMN IF NOT EXISTS nombre_origen VARCHAR(80) NULL;

UPDATE public.rep_dataset_parametro
SET nombre_origen = nombre
WHERE nombre_origen IS NULL
  AND btrim(COALESCE(nombre, '')) <> '';

UPDATE public.rep_dataset_parametro p
SET nombre_origen = 'p_company_id',
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
WHERE d.company_id = p.company_id
  AND d.dataset_id = p.dataset_id
  AND d.codigo = 'sp_clientes'
  AND p.nombre = 'CompanyId';

COMMIT;

SELECT
    d.company_id,
    d.codigo AS dataset_codigo,
    p.nombre,
    p.nombre_origen,
    p.fuente_valor
FROM public.rep_dataset_parametro p
JOIN public.rep_catalogo_dataset d
  ON d.company_id = p.company_id
 AND d.dataset_id = p.dataset_id
WHERE d.codigo = 'sp_clientes'
ORDER BY d.company_id, p.orden, p.nombre;
