-- =============================================================================
-- Seed inicial de ajuste: tercera edad / jubilado
-- Fecha: 2026-04-18
-- Regla implementada:
--   - 25% sobre AGUA_POTABLE
--   - categoria DOMESTICO
--   - tope por factura L300.00
--   - se ejecuta via adm_ajuste_tarifario
-- =============================================================================

INSERT INTO public.adm_ajuste_tarifario (
    company_id,
    cuadro_tarifario_id,
    tipo_ajuste_tarifario_id,
    orden,
    servicio_referencia_id,
    porcentaje,
    tope_maximo,
    condicion_codigo,
    parametros,
    status_id,
    created_by
)
SELECT
    ct.company_id,
    ct.cuadro_tarifario_id,
    tat.tipo_ajuste_tarifario_id,
    1,
    ct.servicio_id,
    25.000000,
    300.0000,
    'TERCERA_EDAD_DOMESTICO',
    jsonb_build_object(
        'alcance', 'AGUA_POTABLE',
        'categoria', 'DOMESTICO',
        'tope_por_factura', 300.00
    ),
    1,
    'system'
FROM public.adm_cuadro_tarifario ct
JOIN public.adm_servicio s
  ON s.company_id = ct.company_id
 AND s.servicio_id = ct.servicio_id
JOIN public.adm_categoria_regulatoria cr
  ON cr.company_id = ct.company_id
 AND cr.categoria_regulatoria_id = ct.categoria_regulatoria_id
JOIN public.adm_tipo_ajuste_tarifario tat
  ON tat.company_id = ct.company_id
 AND tat.codigo = 'DESCUENTO'
WHERE s.codigo = 'AGUA_POTABLE'
  AND cr.codigo = 'DOMESTICO'
  AND ct.status_id = 1
  AND NOT EXISTS (
        SELECT 1
        FROM public.adm_ajuste_tarifario aj
        WHERE aj.company_id = ct.company_id
          AND aj.cuadro_tarifario_id = ct.cuadro_tarifario_id
          AND aj.condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
          AND aj.status_id = 1
    );
