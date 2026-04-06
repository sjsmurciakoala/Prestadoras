-- Resetea el layout persistido del reporte "reporte-20260321165750"
-- para que el siguiente acceso regenere la plantilla desde el catálogo actual.
--
-- Cambie company_id por la empresa correcta antes de ejecutarlo.

BEGIN;

WITH report_target AS (
    SELECT informe_id
    FROM public.rep_catalogo_informe
    WHERE company_id = 1
      AND codigo = 'reporte-20260321165750'
    LIMIT 1
)
DELETE FROM public.rep_reporte_layout layout
USING report_target target
WHERE layout.company_id = 1
  AND layout.informe_id = target.informe_id;

COMMIT;

SELECT
    report.codigo,
    layout.estado,
    layout.version_num,
    layout.updated_at
FROM public.rep_catalogo_informe report
LEFT JOIN public.rep_reporte_layout layout
  ON layout.company_id = report.company_id
 AND layout.informe_id = report.informe_id
WHERE report.company_id = 1
  AND report.codigo = 'reporte-20260321165750'
ORDER BY layout.version_num DESC NULLS LAST;
