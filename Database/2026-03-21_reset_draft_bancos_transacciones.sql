BEGIN;

CREATE TEMP TABLE tmp_reset_report_draft_params
(
    company_id bigint NOT NULL,
    codigo varchar(100) NOT NULL
) ON COMMIT DROP;

INSERT INTO tmp_reset_report_draft_params (company_id, codigo)
VALUES
(
    1, -- CAMBIAR por la empresa objetivo
    'bancos-transacciones'
);

DO $$
DECLARE
    v_company_id bigint;
    v_codigo varchar(100);
    v_deleted integer;
BEGIN
    SELECT company_id, codigo
    INTO v_company_id, v_codigo
    FROM tmp_reset_report_draft_params
    LIMIT 1;

    IF v_company_id IS NULL OR v_company_id <= 0 THEN
        RAISE EXCEPTION 'Defina un company_id valido en tmp_reset_report_draft_params antes de ejecutar el script.';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM public.rep_catalogo_informe report
        WHERE report.company_id = v_company_id
          AND report.codigo = v_codigo
    ) THEN
        RAISE EXCEPTION 'No existe el reporte % para company_id=% en rep_catalogo_informe.', v_codigo, v_company_id;
    END IF;

    DELETE FROM public.rep_reporte_layout layout
    USING public.rep_catalogo_informe report
    WHERE report.company_id = v_company_id
      AND report.codigo = v_codigo
      AND layout.company_id = report.company_id
      AND layout.informe_id = report.informe_id
      AND layout.estado = 'DRAFT';

    GET DIAGNOSTICS v_deleted = ROW_COUNT;

    RAISE NOTICE 'Drafts eliminados para company_id=% reporte=%: %', v_company_id, v_codigo, v_deleted;
END $$;

SELECT
    report.company_id,
    report.codigo,
    report.nombre,
    layout.report_layout_id,
    layout.estado,
    layout.version_num,
    layout.created_at,
    layout.updated_at,
    layout.published_at
FROM public.rep_catalogo_informe report
LEFT JOIN public.rep_reporte_layout layout
    ON layout.company_id = report.company_id
   AND layout.informe_id = report.informe_id
WHERE EXISTS
(
    SELECT 1
    FROM tmp_reset_report_draft_params p
    WHERE p.company_id = report.company_id
      AND p.codigo = report.codigo
)
ORDER BY
    CASE layout.estado
        WHEN 'DRAFT' THEN 1
        WHEN 'PUBLISHED' THEN 2
        ELSE 3
    END,
    layout.version_num DESC NULLS LAST;

COMMIT;
