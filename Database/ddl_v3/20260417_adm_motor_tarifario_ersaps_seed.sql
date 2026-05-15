-- =============================================================================
-- Seed incremental 2026: Tasa SVA ERSAPS desde configuracion de cobros adicionales
-- Fecha: 2026-04-17
-- Requiere:
--   - 20260416_adm_motor_tarifario_core.sql
--   - 20260416_adm_motor_tarifario_apc_seed.sql
--
-- Fuente:
--   - docs/regulatorio/configuracioncobroadicional.csv
--
-- Criterio:
--   - El CSV confirma la logica legacy de cobros adicionales por categoria.
--   - Concepto 2 = Alcantarillado
--   - Concepto 3 = Ambiental
--   - Concepto 4 = ERSAPS
--
-- Decision de modelado:
--   - Alcantarillado y Tasa Ambiental se mantienen con el seed APC ya cargado,
--     porque existen montos/cuadros explicitos en APC.
--   - TASA_SVA_ERSAPS se siembra aqui como concepto DERIVADO con reglas
--     PORCENTAJE_SERVICIO sobre Agua Potable y Alcantarillado.
--
-- Nota:
--   - Los porcentajes del CSV no distinguen entre con/sin medicion.
--   - Por eso el cuadro se crea con condicion NO_APLICA.
--   - Para segmentos especiales (INDUSTRIAL_ENP, PREVENTIVA_DOMESTICA) se crean
--     cuadros especificos; para categorias base se deja segmento NULL.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) Cuadros tarifarios ERSAPS
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 0.020000::numeric, 0.020000::numeric, false),
            ('COMERCIAL', NULL::varchar(40), 0.020000::numeric, 0.020000::numeric, false),
            ('INDUSTRIAL', NULL::varchar(40), 0.020000::numeric, 0.020000::numeric, false),
            ('GUBERNAMENTAL', NULL::varchar(40), 0.020000::numeric, 0.020000::numeric, false),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 0.020000::numeric, 0.020000::numeric, false),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 0.020000::numeric, 0.020000::numeric, false)
    ) AS v(categoria_codigo, segmento_codigo, porcentaje_agua, porcentaje_alcantarillado, aplica_descuento)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id,
    servicio_id,
    categoria_regulatoria_id,
    condicion_medicion_id,
    segmento_tarifario_id,
    codigo,
    nombre,
    descripcion,
    vigencia_desde,
    vigencia_hasta,
    prioridad,
    referencia_normativa,
    status_id,
    created_at,
    created_by
)
SELECT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'CFG_ERSAPS_NA_' || COALESCE(src.segmento_codigo, src.categoria_codigo),
    'Tasa SVA ERSAPS - ' || COALESCE(seg.nombre, cat.nombre) || ' (CFG adicional 2026)',
    'Cuadro derivado desde configuracioncobroadicional.csv para Tasa SVA ERSAPS.',
    DATE '2026-01-01',
    NULL::date,
    1,
    'configuracioncobroadicional.csv / compatibilidad app 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id
   AND srv.codigo = 'TASA_SVA_ERSAPS'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id
   AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id
   AND cm.codigo = 'NO_APLICA'
LEFT JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id
   AND seg.codigo = src.segmento_codigo
ON CONFLICT (company_id, codigo) DO UPDATE
SET
    servicio_id = EXCLUDED.servicio_id,
    categoria_regulatoria_id = EXCLUDED.categoria_regulatoria_id,
    condicion_medicion_id = EXCLUDED.condicion_medicion_id,
    segmento_tarifario_id = EXCLUDED.segmento_tarifario_id,
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion,
    vigencia_desde = EXCLUDED.vigencia_desde,
    vigencia_hasta = EXCLUDED.vigencia_hasta,
    prioridad = EXCLUDED.prioridad,
    referencia_normativa = EXCLUDED.referencia_normativa,
    status_id = 1,
    updated_at = now(),
    updated_by = 'system';

-- -----------------------------------------------------------------------------
-- 2) Regla porcentaje sobre Agua Potable
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 0.020000::numeric, false),
            ('COMERCIAL', NULL::varchar(40), 0.020000::numeric, false),
            ('INDUSTRIAL', NULL::varchar(40), 0.020000::numeric, false),
            ('GUBERNAMENTAL', NULL::varchar(40), 0.020000::numeric, false),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 0.020000::numeric, false),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 0.020000::numeric, false)
    ) AS v(categoria_codigo, segmento_codigo, porcentaje, aplica_descuento)
)
INSERT INTO public.adm_regla_tarifaria (
    company_id,
    cuadro_tarifario_id,
    tipo_regla_tarifaria_id,
    orden,
    consumo_minimo,
    consumo_maximo,
    monto_fijo,
    monto_unitario,
    porcentaje,
    servicio_referencia_id,
    parametros,
    status_id,
    created_at,
    created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    1,
    NULL,
    NULL,
    NULL,
    NULL,
    src.porcentaje,
    srv_ref.servicio_id,
    jsonb_build_object(
        'origen', 'CONFIGURACION_COBROS_ADICIONALES',
        'concepto_legacy_id', 4,
        'base_legacy', 'PorcentajeAgua',
        'modo_calculo', 'PORCENTAJE_SOBRE_SERVICIO_RESUELTO',
        'aplica_para_descuento', src.aplica_descuento
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN source src
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id
   AND ct.codigo = 'CFG_ERSAPS_NA_' || COALESCE(src.segmento_codigo, src.categoria_codigo)
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id
   AND tr.codigo = 'PORCENTAJE_SERVICIO'
JOIN public.adm_servicio srv_ref
    ON srv_ref.company_id = c.company_id
   AND srv_ref.codigo = 'AGUA_POTABLE'
ON CONFLICT (company_id, cuadro_tarifario_id, orden) DO UPDATE
SET
    tipo_regla_tarifaria_id = EXCLUDED.tipo_regla_tarifaria_id,
    consumo_minimo = EXCLUDED.consumo_minimo,
    consumo_maximo = EXCLUDED.consumo_maximo,
    monto_fijo = EXCLUDED.monto_fijo,
    monto_unitario = EXCLUDED.monto_unitario,
    porcentaje = EXCLUDED.porcentaje,
    servicio_referencia_id = EXCLUDED.servicio_referencia_id,
    parametros = EXCLUDED.parametros,
    status_id = 1,
    updated_at = now(),
    updated_by = 'system';

-- -----------------------------------------------------------------------------
-- 3) Regla porcentaje sobre Alcantarillado
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 0.020000::numeric, false),
            ('COMERCIAL', NULL::varchar(40), 0.020000::numeric, false),
            ('INDUSTRIAL', NULL::varchar(40), 0.020000::numeric, false),
            ('GUBERNAMENTAL', NULL::varchar(40), 0.020000::numeric, false),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 0.020000::numeric, false),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 0.020000::numeric, false)
    ) AS v(categoria_codigo, segmento_codigo, porcentaje, aplica_descuento)
)
INSERT INTO public.adm_regla_tarifaria (
    company_id,
    cuadro_tarifario_id,
    tipo_regla_tarifaria_id,
    orden,
    consumo_minimo,
    consumo_maximo,
    monto_fijo,
    monto_unitario,
    porcentaje,
    servicio_referencia_id,
    parametros,
    status_id,
    created_at,
    created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    2,
    NULL,
    NULL,
    NULL,
    NULL,
    src.porcentaje,
    srv_ref.servicio_id,
    jsonb_build_object(
        'origen', 'CONFIGURACION_COBROS_ADICIONALES',
        'concepto_legacy_id', 4,
        'base_legacy', 'PorcentajeAlcantarilla',
        'modo_calculo', 'PORCENTAJE_SOBRE_SERVICIO_RESUELTO',
        'aplica_para_descuento', src.aplica_descuento
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN source src
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id
   AND ct.codigo = 'CFG_ERSAPS_NA_' || COALESCE(src.segmento_codigo, src.categoria_codigo)
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id
   AND tr.codigo = 'PORCENTAJE_SERVICIO'
JOIN public.adm_servicio srv_ref
    ON srv_ref.company_id = c.company_id
   AND srv_ref.codigo = 'ALCANTARILLADO'
ON CONFLICT (company_id, cuadro_tarifario_id, orden) DO UPDATE
SET
    tipo_regla_tarifaria_id = EXCLUDED.tipo_regla_tarifaria_id,
    consumo_minimo = EXCLUDED.consumo_minimo,
    consumo_maximo = EXCLUDED.consumo_maximo,
    monto_fijo = EXCLUDED.monto_fijo,
    monto_unitario = EXCLUDED.monto_unitario,
    porcentaje = EXCLUDED.porcentaje,
    servicio_referencia_id = EXCLUDED.servicio_referencia_id,
    parametros = EXCLUDED.parametros,
    status_id = 1,
    updated_at = now(),
    updated_by = 'system';
