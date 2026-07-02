ALTER TABLE public.prv_compromiso_hdr
    ADD COLUMN IF NOT EXISTS correlativo_proveedor integer;

ALTER TABLE public.prv_proveedores
    ADD COLUMN IF NOT EXISTS ultimo_correlativo_compromiso integer NOT NULL DEFAULT 0;

WITH correlativos AS (
    SELECT h.numero_orden,
           ROW_NUMBER() OVER (
               PARTITION BY btrim(h.cod_proveedor)
               ORDER BY h.fecha, h.numero_orden
           ) AS correlativo_proveedor
    FROM public.prv_compromiso_hdr h
    WHERE NULLIF(btrim(h.cod_proveedor), '') IS NOT NULL
)
UPDATE public.prv_compromiso_hdr h
SET correlativo_proveedor = c.correlativo_proveedor
FROM correlativos c
WHERE h.numero_orden = c.numero_orden
  AND h.correlativo_proveedor IS DISTINCT FROM c.correlativo_proveedor;

UPDATE public.prv_compromiso_hdr
SET correlativo_proveedor = NULL
WHERE NULLIF(btrim(cod_proveedor), '') IS NULL;

UPDATE public.prv_proveedores p
SET ultimo_correlativo_compromiso = COALESCE(c.max_correlativo, 0)
FROM (
    SELECT btrim(cod_proveedor) AS cod_proveedor,
           MAX(correlativo_proveedor) AS max_correlativo
    FROM public.prv_compromiso_hdr
    WHERE NULLIF(btrim(cod_proveedor), '') IS NOT NULL
    GROUP BY btrim(cod_proveedor)
) c
WHERE btrim(p.cod_proveedor) = c.cod_proveedor;

UPDATE public.prv_proveedores
SET ultimo_correlativo_compromiso = 0
WHERE ultimo_correlativo_compromiso IS NULL;

COMMENT ON COLUMN public.prv_compromiso_hdr.correlativo_proveedor IS 'Correlativo consecutivo del compromiso dentro del proveedor.';
COMMENT ON COLUMN public.prv_proveedores.ultimo_correlativo_compromiso IS 'Ultimo correlativo utilizado en compromisos para este proveedor.';
