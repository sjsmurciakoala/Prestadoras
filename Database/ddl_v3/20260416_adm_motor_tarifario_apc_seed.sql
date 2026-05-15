-- =============================================================================
-- Seed APC 2026: cuadros y reglas tarifarias iniciales
-- Fecha: 2026-04-16
-- Requiere:
--   - 20260416_adm_motor_tarifario_core.sql ejecutado previamente
--
-- Cobertura inicial:
--   - Agua potable sin medicion   (APC tarifas tipo = 1)
--   - Alcantarillado              (APC tarifas tipo = 2)
--   - Tasa ambiental sin medicion (APC tarifas tipo = 3)
--   - Agua potable con medicion   (APC tarifas_contador tipo = 1)
--   - Tasa ambiental con medicion (APC tarifas_contador.cuota3)
--
-- Notas:
--   - NO se siembra Tasa SVA ERSAPS porque la fuente APC compartida no trae monto.
--   - NO se siembra gestion legal / abogados porque no forma parte del nucleo base.
--   - En agua con medicion se conserva la logica APC actual:
--       primera fila: cuota * consumo + valor_base
--       filas siguientes: cuota * diferencia
--       alquiler se guarda en parametros por regla
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) Agua potable sin medicion (APC tipo = 1)
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 199.27::numeric),
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 820.86::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 369.19::numeric),
            ('DOMESTICO', 'DOMESTICA_TULIAN', 'C', 'DOMESTICA TULIAN', 153.22::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'E', 'PREVENTIVA DOMESTICA', 5.00::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 5171.55::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 2459.57::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 1195.63::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'D', 'PREVENTIVA COMERCIAL', 5.00::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 3102.92::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 1856.63::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 827.45::numeric),
            ('GUBERNAMENTAL', 'PUBLICA_UNICA', 'U', 'PUBLICA UNICA', 482.08::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id, servicio_id, categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
    codigo, nombre, descripcion, vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
    status_id, created_at, created_by
)
SELECT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'APC_AGUA_SM_' || src.segmento_codigo,
    'Agua Potable sin medicion - ' || src.descripcion_legacy || ' (APC 2026)',
    'Cuadro tarifario APC para agua potable sin medicion.',
    DATE '2026-01-01',
    NULL::date,
    1,
    'APC tarifas 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id AND srv.codigo = 'AGUA_POTABLE'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id AND cm.codigo = 'SIN_MEDICION'
JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id AND seg.codigo = src.segmento_codigo
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

WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 199.27::numeric),
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 820.86::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 369.19::numeric),
            ('DOMESTICO', 'DOMESTICA_TULIAN', 'C', 'DOMESTICA TULIAN', 153.22::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'E', 'PREVENTIVA DOMESTICA', 5.00::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 5171.55::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 2459.57::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 1195.63::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'D', 'PREVENTIVA COMERCIAL', 5.00::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 3102.92::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 1856.63::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 827.45::numeric),
            ('GUBERNAMENTAL', 'PUBLICA_UNICA', 'U', 'PUBLICA UNICA', 482.08::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_regla_tarifaria (
    company_id, cuadro_tarifario_id, tipo_regla_tarifaria_id, orden, consumo_minimo, consumo_maximo,
    monto_fijo, monto_unitario, porcentaje, servicio_referencia_id, parametros, status_id, created_at, created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    1,
    NULL,
    NULL,
    src.valor,
    NULL,
    NULL,
    NULL,
    jsonb_build_object(
        'origen', 'APC_TARIFAS',
        'tipo_legacy', 1,
        'codigo_legacy', src.codigo_legacy,
        'descripcion_legacy', src.descripcion_legacy,
        'modo_calculo', 'MONTO_DIRECTO_APC'
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN source src
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id AND ct.codigo = 'APC_AGUA_SM_' || src.segmento_codigo
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id AND tr.codigo = 'MONTO_FIJO'
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
-- 2) Alcantarillado (APC tipo = 2)
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 491.91::numeric),
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 119.56::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 221.52::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 3102.92::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 717.38::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 1475.75::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_GRANDE', 'A', 'INDUSTRIAL GRANDE', 0.00::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 1299.63::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 2172.06::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 496.47::numeric),
            ('GUBERNAMENTAL', 'PUBLICA_UNICA', 'U', 'PUBLICA UNICA', 455.47::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id, servicio_id, categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
    codigo, nombre, descripcion, vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
    status_id, created_at, created_by
)
SELECT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'APC_ALC_NA_' || src.segmento_codigo,
    'Alcantarillado - ' || src.descripcion_legacy || ' (APC 2026)',
    'Cuadro tarifario APC para alcantarillado.',
    DATE '2026-01-01',
    NULL::date,
    1,
    'APC tarifas 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id AND srv.codigo = 'ALCANTARILLADO'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id AND cm.codigo = 'NO_APLICA'
JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id AND seg.codigo = src.segmento_codigo
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

WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 491.91::numeric),
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 119.56::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 221.52::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 3102.92::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 717.38::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 1475.75::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_GRANDE', 'A', 'INDUSTRIAL GRANDE', 0.00::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 1299.63::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 2172.06::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 496.47::numeric),
            ('GUBERNAMENTAL', 'PUBLICA_UNICA', 'U', 'PUBLICA UNICA', 455.47::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_regla_tarifaria (
    company_id, cuadro_tarifario_id, tipo_regla_tarifaria_id, orden, consumo_minimo, consumo_maximo,
    monto_fijo, monto_unitario, porcentaje, servicio_referencia_id, parametros, status_id, created_at, created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    1,
    NULL,
    NULL,
    src.valor,
    NULL,
    NULL,
    NULL,
    jsonb_build_object(
        'origen', 'APC_TARIFAS',
        'tipo_legacy', 2,
        'codigo_legacy', src.codigo_legacy,
        'descripcion_legacy', src.descripcion_legacy,
        'modo_calculo', 'MONTO_DIRECTO_APC'
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN source src
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id AND ct.codigo = 'APC_ALC_NA_' || src.segmento_codigo
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id AND tr.codigo = 'MONTO_FIJO'
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
-- 3) Tasa ambiental sin medicion (APC tipo = 3)
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 23.60::numeric),
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 5.90::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 10.60::numeric),
            ('DOMESTICO', 'DOMESTICA_TULIAN', 'C', 'DOMESTICA TULIAN', 7.66::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 147.50::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 35.40::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 70.80::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 53.10::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 88.50::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'U', 'INDUSTRIAL ENP', 400000.00::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 23.60::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id, servicio_id, categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
    codigo, nombre, descripcion, vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
    status_id, created_at, created_by
)
SELECT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'APC_TAMB_SM_' || src.segmento_codigo,
    'Tasa Ambiental sin medicion - ' || src.descripcion_legacy || ' (APC 2026)',
    'Cuadro APC para tasa ambiental sin medicion.',
    DATE '2026-01-01',
    NULL,
    1,
    'PLAN DE ARBITRIOS 2026 / APC tarifas 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id AND srv.codigo = 'TASA_AMBIENTAL'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id AND cm.codigo = 'SIN_MEDICION'
JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id AND seg.codigo = src.segmento_codigo
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

WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', 'DOMESTICA_ALTA', 'A', 'DOMESTICO ALTA', 23.60::numeric),
            ('DOMESTICO', 'DOMESTICA_BAJA', 'B', 'DOMESTICA BAJA', 5.90::numeric),
            ('DOMESTICO', 'DOMESTICA_MEDIA', 'M', 'DOMESTICA MEDIA', 10.60::numeric),
            ('DOMESTICO', 'DOMESTICA_TULIAN', 'C', 'DOMESTICA TULIAN', 7.66::numeric),
            ('COMERCIAL', 'COMERCIAL_GRANDE', 'A', 'COMERCIAL GRANDE', 147.50::numeric),
            ('COMERCIAL', 'COMERCIAL_PEQUENA', 'B', 'COMERCIAL PEQUENA', 35.40::numeric),
            ('COMERCIAL', 'COMERCIAL_MEDIANA', 'M', 'COMERCIAL MEDIANA', 70.80::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_PEQUENA', 'B', 'INDUSTRIAL PEQUENA', 53.10::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_MEDIANA', 'M', 'INDUSTRIAL MEDIANA', 88.50::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'U', 'INDUSTRIAL ENP', 400000.00::numeric),
            ('GUBERNAMENTAL', 'GUBERNAMENTAL_UNICA', 'A', 'UNICA', 23.60::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, valor)
)
INSERT INTO public.adm_regla_tarifaria (
    company_id, cuadro_tarifario_id, tipo_regla_tarifaria_id, orden, consumo_minimo, consumo_maximo,
    monto_fijo, monto_unitario, porcentaje, servicio_referencia_id, parametros, status_id, created_at, created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    1,
    NULL,
    NULL,
    src.valor,
    NULL,
    NULL,
    srv_ref.servicio_id,
    jsonb_build_object(
        'origen', 'APC_TARIFAS',
        'tipo_legacy', 3,
        'codigo_legacy', src.codigo_legacy,
        'descripcion_legacy', src.descripcion_legacy,
        'modo_calculo', 'MONTO_DIRECTO_APC'
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN source src
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id AND ct.codigo = 'APC_TAMB_SM_' || src.segmento_codigo
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id AND tr.codigo = 'MONTO_FIJO'
JOIN public.adm_servicio srv_ref
    ON srv_ref.company_id = c.company_id AND srv_ref.codigo = 'AGUA_POTABLE'
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
-- 4) Agua potable con medicion (APC tarifas_contador tipo = 1)
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 'A', 'Domestico', 0.00::numeric, 20.00::numeric, 0.00::numeric, 163.67::numeric, 0.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'B', 'Domestico', 21.00::numeric, 30.00::numeric, 12.98::numeric, 0.00::numeric, 1.50::numeric),
            ('DOMESTICO', NULL::varchar(40), 'C', 'Domestico', 31.00::numeric, 40.00::numeric, 15.55::numeric, 0.00::numeric, 0.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'D', 'Domestico', 41.00::numeric, 999999999.00::numeric, 19.20::numeric, 0.00::numeric, 0.00::numeric),
            ('COMERCIAL', NULL::varchar(40), 'A', 'Comercial', 0.00::numeric, 30.00::numeric, 0.00::numeric, 632.99::numeric, 0.00::numeric),
            ('COMERCIAL', NULL::varchar(40), 'B', 'Comercial', 31.00::numeric, 50.00::numeric, 25.96::numeric, 0.00::numeric, 1.50::numeric),
            ('COMERCIAL', NULL::varchar(40), 'C', 'Comercial', 51.00::numeric, 9999999.00::numeric, 30.57::numeric, 0.00::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'A', 'Industrial', 0.00::numeric, 50.00::numeric, 0.00::numeric, 1644.38::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'B', 'Industrial', 51.00::numeric, 70.00::numeric, 36.73::numeric, 0.00::numeric, 1.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'C', 'Industrial', 71.00::numeric, 1000.00::numeric, 43.73::numeric, 0.00::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'D', 'Industrial', 1001.00::numeric, 99999999.00::numeric, 56.10::numeric, 0.00::numeric, 0.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'A', 'Publica', 0.00::numeric, 70.00::numeric, 0.00::numeric, 701.52::numeric, 0.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'B', 'Publica', 71.00::numeric, 9999999.00::numeric, 24.40::numeric, 0.00::numeric, 1.50::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'A', 'Industrial (ENP)', 0.00::numeric, 999999999.00::numeric, 70.53::numeric, 0.00::numeric, 1.50::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'A', 'Preventiva Domestica', 0.00::numeric, 999999999.00::numeric, 0.00::numeric, 5.00::numeric, 0.00::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'A', 'Preventiva Comercial', 0.00::numeric, 9999999.00::numeric, 0.00::numeric, 5.00::numeric, 0.00::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, minimo, maximo, cuota, valor_base, alquiler)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id, servicio_id, categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
    codigo, nombre, descripcion, vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
    status_id, created_at, created_by
)
SELECT DISTINCT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'APC_AGUA_CM_' || COALESCE(src.segmento_codigo, src.categoria_codigo),
    'Agua Potable con medicion - ' || COALESCE(seg.nombre, cat.nombre) || ' (APC 2026)',
    'Cuadro APC para agua potable con medicion.',
    DATE '2026-01-01',
    NULL::date,
    1,
    'APC tarifas contador 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id AND srv.codigo = 'AGUA_POTABLE'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id AND cm.codigo = 'CON_MEDICION'
LEFT JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id AND seg.codigo = src.segmento_codigo
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
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 'A', 'Domestico', 0.00::numeric, 20.00::numeric, 0.00::numeric, 163.67::numeric, 0.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'B', 'Domestico', 21.00::numeric, 30.00::numeric, 12.98::numeric, 0.00::numeric, 1.50::numeric),
            ('DOMESTICO', NULL::varchar(40), 'C', 'Domestico', 31.00::numeric, 40.00::numeric, 15.55::numeric, 0.00::numeric, 0.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'D', 'Domestico', 41.00::numeric, 999999999.00::numeric, 19.20::numeric, 0.00::numeric, 0.00::numeric),
            ('COMERCIAL', NULL::varchar(40), 'A', 'Comercial', 0.00::numeric, 30.00::numeric, 0.00::numeric, 632.99::numeric, 0.00::numeric),
            ('COMERCIAL', NULL::varchar(40), 'B', 'Comercial', 31.00::numeric, 50.00::numeric, 25.96::numeric, 0.00::numeric, 1.50::numeric),
            ('COMERCIAL', NULL::varchar(40), 'C', 'Comercial', 51.00::numeric, 9999999.00::numeric, 30.57::numeric, 0.00::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'A', 'Industrial', 0.00::numeric, 50.00::numeric, 0.00::numeric, 1644.38::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'B', 'Industrial', 51.00::numeric, 70.00::numeric, 36.73::numeric, 0.00::numeric, 1.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'C', 'Industrial', 71.00::numeric, 1000.00::numeric, 43.73::numeric, 0.00::numeric, 0.00::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'D', 'Industrial', 1001.00::numeric, 99999999.00::numeric, 56.10::numeric, 0.00::numeric, 0.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'A', 'Publica', 0.00::numeric, 70.00::numeric, 0.00::numeric, 701.52::numeric, 0.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'B', 'Publica', 71.00::numeric, 9999999.00::numeric, 24.40::numeric, 0.00::numeric, 1.50::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'A', 'Industrial (ENP)', 0.00::numeric, 999999999.00::numeric, 70.53::numeric, 0.00::numeric, 1.50::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'A', 'Preventiva Domestica', 0.00::numeric, 999999999.00::numeric, 0.00::numeric, 5.00::numeric, 0.00::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'A', 'Preventiva Comercial', 0.00::numeric, 9999999.00::numeric, 0.00::numeric, 5.00::numeric, 0.00::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, minimo, maximo, cuota, valor_base, alquiler)
),
ordered AS (
    SELECT
        src.*,
        ROW_NUMBER() OVER (
            PARTITION BY src.categoria_codigo, src.segmento_codigo
            ORDER BY src.minimo, src.maximo, src.codigo_legacy
        ) AS orden
    FROM source src
)
INSERT INTO public.adm_regla_tarifaria (
    company_id, cuadro_tarifario_id, tipo_regla_tarifaria_id, orden, consumo_minimo, consumo_maximo,
    monto_fijo, monto_unitario, porcentaje, servicio_referencia_id, parametros, status_id, created_at, created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    o.orden,
    o.minimo,
    o.maximo,
    o.valor_base,
    o.cuota,
    NULL,
    NULL,
    jsonb_build_object(
        'origen', 'APC_TARIFA_CONTADOR',
        'tipo_legacy', 1,
        'codigo_legacy', o.codigo_legacy,
        'descripcion_legacy', o.descripcion_legacy,
        'modo_calculo', 'ACUMULADO_POR_RANGO_APC',
        'alquiler', o.alquiler
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN ordered o
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id AND ct.codigo = 'APC_AGUA_CM_' || COALESCE(o.segmento_codigo, o.categoria_codigo)
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id AND tr.codigo = 'RANGO_CONSUMO'
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
-- 5) Tasa ambiental con medicion (APC tarifas_contador.cuota3)
-- -----------------------------------------------------------------------------
WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 'A', 'Domestico', 0.00::numeric, 20.00::numeric, 5.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'B', 'Domestico', 21.00::numeric, 30.00::numeric, 6.90::numeric),
            ('DOMESTICO', NULL::varchar(40), 'C', 'Domestico', 31.00::numeric, 40.00::numeric, 11.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'D', 'Domestico', 41.00::numeric, 999999999.00::numeric, 24.25::numeric),
            ('COMERCIAL', NULL::varchar(40), 'A', 'Comercial', 0.00::numeric, 30.00::numeric, 18.75::numeric),
            ('COMERCIAL', NULL::varchar(40), 'B', 'Comercial', 31.00::numeric, 50.00::numeric, 26.25::numeric),
            ('COMERCIAL', NULL::varchar(40), 'C', 'Comercial', 51.00::numeric, 9999999.00::numeric, 99.40::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'A', 'Industrial', 0.00::numeric, 50.00::numeric, 47.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'B', 'Industrial', 51.00::numeric, 70.00::numeric, 49.60::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'C', 'Industrial', 71.00::numeric, 1000.00::numeric, 393.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'D', 'Industrial', 1001.00::numeric, 99999999.00::numeric, 3646.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'A', 'Publica', 0.00::numeric, 70.00::numeric, 20.30::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'B', 'Publica', 71.00::numeric, 9999999.00::numeric, 244.30::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'A', 'Industrial (ENP)', 0.00::numeric, 999999999.00::numeric, 40000.00::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'A', 'Preventiva Domestica', 0.00::numeric, 999999999.00::numeric, 0.00::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'A', 'Preventiva Comercial', 0.00::numeric, 9999999.00::numeric, 0.00::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, minimo, maximo, valor)
)
INSERT INTO public.adm_cuadro_tarifario (
    company_id, servicio_id, categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
    codigo, nombre, descripcion, vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
    status_id, created_at, created_by
)
SELECT DISTINCT
    c.company_id,
    srv.servicio_id,
    cat.categoria_regulatoria_id,
    cm.condicion_medicion_id,
    seg.segmento_tarifario_id,
    'APC_TAMB_CM_' || COALESCE(src.segmento_codigo, src.categoria_codigo),
    'Tasa Ambiental con medicion - ' || COALESCE(seg.nombre, cat.nombre) || ' (APC 2026)',
    'Cuadro APC para tasa ambiental con medicion.',
    DATE '2026-01-01',
    NULL::date,
    1,
    'PLAN DE ARBITRIOS 2026 / APC tarifas contador 2026',
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN public.adm_servicio srv
    ON srv.company_id = c.company_id AND srv.codigo = 'TASA_AMBIENTAL'
JOIN source src
    ON 1 = 1
JOIN public.adm_categoria_regulatoria cat
    ON cat.company_id = c.company_id AND cat.codigo = src.categoria_codigo
JOIN public.adm_condicion_medicion cm
    ON cm.company_id = c.company_id AND cm.codigo = 'CON_MEDICION'
LEFT JOIN public.adm_segmento_tarifario seg
    ON seg.company_id = c.company_id AND seg.codigo = src.segmento_codigo
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

WITH source AS (
    SELECT *
    FROM (
        VALUES
            ('DOMESTICO', NULL::varchar(40), 'A', 'Domestico', 0.00::numeric, 20.00::numeric, 5.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'B', 'Domestico', 21.00::numeric, 30.00::numeric, 6.90::numeric),
            ('DOMESTICO', NULL::varchar(40), 'C', 'Domestico', 31.00::numeric, 40.00::numeric, 11.00::numeric),
            ('DOMESTICO', NULL::varchar(40), 'D', 'Domestico', 41.00::numeric, 999999999.00::numeric, 24.25::numeric),
            ('COMERCIAL', NULL::varchar(40), 'A', 'Comercial', 0.00::numeric, 30.00::numeric, 18.75::numeric),
            ('COMERCIAL', NULL::varchar(40), 'B', 'Comercial', 31.00::numeric, 50.00::numeric, 26.25::numeric),
            ('COMERCIAL', NULL::varchar(40), 'C', 'Comercial', 51.00::numeric, 9999999.00::numeric, 99.40::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'A', 'Industrial', 0.00::numeric, 50.00::numeric, 47.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'B', 'Industrial', 51.00::numeric, 70.00::numeric, 49.60::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'C', 'Industrial', 71.00::numeric, 1000.00::numeric, 393.50::numeric),
            ('INDUSTRIAL', NULL::varchar(40), 'D', 'Industrial', 1001.00::numeric, 99999999.00::numeric, 3646.00::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'A', 'Publica', 0.00::numeric, 70.00::numeric, 20.30::numeric),
            ('GUBERNAMENTAL', NULL::varchar(40), 'B', 'Publica', 71.00::numeric, 9999999.00::numeric, 244.30::numeric),
            ('INDUSTRIAL', 'INDUSTRIAL_ENP', 'A', 'Industrial (ENP)', 0.00::numeric, 999999999.00::numeric, 40000.00::numeric),
            ('DOMESTICO', 'PREVENTIVA_DOMESTICA', 'A', 'Preventiva Domestica', 0.00::numeric, 999999999.00::numeric, 0.00::numeric),
            ('COMERCIAL', 'PREVENTIVA_COMERCIAL', 'A', 'Preventiva Comercial', 0.00::numeric, 9999999.00::numeric, 0.00::numeric)
    ) AS v(categoria_codigo, segmento_codigo, codigo_legacy, descripcion_legacy, minimo, maximo, valor)
),
ordered AS (
    SELECT
        src.*,
        ROW_NUMBER() OVER (
            PARTITION BY src.categoria_codigo, src.segmento_codigo
            ORDER BY src.minimo, src.maximo, src.codigo_legacy
        ) AS orden
    FROM source src
)
INSERT INTO public.adm_regla_tarifaria (
    company_id, cuadro_tarifario_id, tipo_regla_tarifaria_id, orden, consumo_minimo, consumo_maximo,
    monto_fijo, monto_unitario, porcentaje, servicio_referencia_id, parametros, status_id, created_at, created_by
)
SELECT
    c.company_id,
    ct.cuadro_tarifario_id,
    tr.tipo_regla_tarifaria_id,
    o.orden,
    o.minimo,
    o.maximo,
    o.valor,
    NULL,
    NULL,
    srv_ref.servicio_id,
    jsonb_build_object(
        'origen', 'APC_TARIFA_CONTADOR',
        'tipo_legacy', 1,
        'codigo_legacy', o.codigo_legacy,
        'descripcion_legacy', o.descripcion_legacy,
        'campo_legacy', 'cuota3',
        'modo_calculo', 'TRAMO_DIRECTO_APC'
    ),
    1,
    now(),
    'system'
FROM public.cfg_company c
JOIN ordered o
    ON 1 = 1
JOIN public.adm_cuadro_tarifario ct
    ON ct.company_id = c.company_id AND ct.codigo = 'APC_TAMB_CM_' || COALESCE(o.segmento_codigo, o.categoria_codigo)
JOIN public.adm_tipo_regla_tarifaria tr
    ON tr.company_id = c.company_id AND tr.codigo = 'RANGO_CONSUMO'
JOIN public.adm_servicio srv_ref
    ON srv_ref.company_id = c.company_id AND srv_ref.codigo = 'AGUA_POTABLE'
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
-- 6) Pendientes intencionales
-- -----------------------------------------------------------------------------
-- `TASA_SVA_ERSAPS`:
--   se omite del seed porque la fuente APC compartida no trae monto confirmado.
--
-- `tipo = 5` gestion legal / abogados:
--   se deja fuera del nucleo tarifario inicial; se evaluara en una capa aparte.
--
-- `adulto mayor / jubilado`:
--   el Art. 42 del Plan de Arbitrios confirma 25% con tope mensual de L300.00
--   sobre consumo de agua. Se recomienda sembrarlo en `adm_ajuste_tarifario`
--   cuando se cierre la condicion funcional exacta de aplicacion.
