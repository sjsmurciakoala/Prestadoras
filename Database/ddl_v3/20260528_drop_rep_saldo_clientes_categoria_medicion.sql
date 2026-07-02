-- =============================================================================
-- Eliminacion del informe de medicion: saldo de clientes por categoria
-- =============================================================================

DELETE FROM public.rep_dataset_parametros p
USING public.rep_catalogo_datasets d
WHERE d.codigo = 'saldo-clientes-categoria'
  AND p.company_id = d.company_id
  AND p.dataset_id = d.dataset_id;

DELETE FROM public.rep_catalogo_datasets
WHERE codigo = 'saldo-clientes-categoria';

DELETE FROM public.rep_catalogo_informes
WHERE codigo = 'saldo-clientes-categoria';

DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date, integer, integer);
DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date, date, integer, integer);
DROP FUNCTION IF EXISTS public.rep_saldo_clientes_categoria(bigint, date, date);
