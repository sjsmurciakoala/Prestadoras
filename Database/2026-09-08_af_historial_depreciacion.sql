-- =============================================================================
-- Activos Fijos F1 — historial de depreciación por activo (solo lectura)
-- Fecha: 2026-09-08
-- Depende de: 2026-09-08_af_activos_fijos_f1_registro.sql
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- QUÉ HACE
--   Publica las dos funciones que alimentan la pestaña "Historial de depreciación"
--   de la ficha del activo. La tabla af_activo_fijo_depreciacion trae 44,579 filas
--   migradas de SIMAFI (MySQL bdsimafi.depreinve) que hasta hoy no se veían en
--   ninguna pantalla del portal.
--
-- ⚠️ LA RELACIÓN CON EL ACTIVO ESTÁ A MEDIAS. El script de migración
--   (2026-07-01) dejó activo_fijo_id en NULL y lo resolvió después por
--   codigo_activo. Quedaron 792 filas sin vincular, de las cuales 80 códigos
--   distintos son huérfanos: no existe ese activo en el maestro. Por eso el
--   listado busca por las DOS vías, id y código, y marca cada fila con
--   vinculo_por_codigo para que se vea de dónde salió.
--
-- ⚠️ EL DETALLE NO CUADRA CON EL MAESTRO. La suma del detalle da
--   L. 10,368,231.23 y la depreciación acumulada que declaran los activos da
--   L. 16,048,909.78. El desglose explica dos tercios del acumulado. La función
--   de resumen devuelve las dos cifras y su diferencia a propósito: el descuadre
--   se ve en la ficha en vez de quedar escondido. Resolverlo es requisito de la
--   Fase 2 (depreciación), porque el motor necesita saber desde dónde continuar.
--
-- ADITIVO / sin riesgo: solo funciones nuevas de LECTURA. No crea ni altera
--   tablas, no escribe ninguna fila.
-- IDEMPOTENTE: CREATE OR REPLACE. Los DROP son de estas mismas funciones.
-- =============================================================================

BEGIN;

DROP FUNCTION IF EXISTS public.fn_af_activo_depreciacion_listar(BIGINT, INTEGER);
DROP FUNCTION IF EXISTS public.fn_af_activo_depreciacion_resumen(BIGINT, INTEGER);

-- ── 1. Detalle mes a mes ─────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.fn_af_activo_depreciacion_listar(
    p_company_id     BIGINT,
    p_activo_fijo_id INTEGER)
RETURNS TABLE (id INTEGER, anio SMALLINT, mes SMALLINT, periodo TEXT,
               fecha_depreciacion DATE, valor_depreciado NUMERIC,
               valor_neto_libros NUMERIC, cuenta_depreciacion VARCHAR,
               cuenta_gasto VARCHAR, descripcion VARCHAR,
               vinculo_por_codigo BOOLEAN)
LANGUAGE sql STABLE AS $$
    WITH activo AS (
        SELECT a.id, a.codigo_activo
          FROM public.af_activo_fijo a
         WHERE a.company_id = p_company_id AND a.id = p_activo_fijo_id
    )
    SELECT d.id, d.anio, d.mes,
           -- El origen trae 3 filas con año 0 y sin código: dato huérfano ya
           -- existente en MySQL, no un error de la migración. Se rotula, no se oculta.
           CASE WHEN d.anio = 0 THEN 'Sin período'
                ELSE d.anio::TEXT || '-' || lpad(d.mes::TEXT, 2, '0')
           END,
           d.fecha_depreciacion, d.valor_depreciado, d.valor_neto_libros,
           d.cuenta_depreciacion, d.cuenta_gasto, d.descripcion,
           (d.activo_fijo_id IS NULL)
      FROM public.af_activo_fijo_depreciacion d
      JOIN activo act ON TRUE
     WHERE d.company_id = p_company_id
       AND (d.activo_fijo_id = act.id
            OR (d.activo_fijo_id IS NULL AND d.codigo_activo = act.codigo_activo))
     ORDER BY d.anio DESC, d.mes DESC, d.id DESC;
$$;

COMMENT ON FUNCTION public.fn_af_activo_depreciacion_listar(BIGINT, INTEGER) IS
'Detalle mensual de depreciación de un activo, más reciente primero. Resuelve por activo_fijo_id y, para las filas que la migración dejó sueltas, por codigo_activo.';

-- ── 2. Resumen y contraste contra el maestro ─────────────────────────────────
CREATE OR REPLACE FUNCTION public.fn_af_activo_depreciacion_resumen(
    p_company_id     BIGINT,
    p_activo_fijo_id INTEGER)
RETURNS TABLE (filas BIGINT, anio_desde SMALLINT, anio_hasta SMALLINT,
               total_detalle NUMERIC, acumulada_maestro NUMERIC,
               diferencia NUMERIC, valor_compra NUMERIC, valor_libros NUMERIC)
LANGUAGE sql STABLE AS $$
    WITH activo AS (
        SELECT a.id, a.codigo_activo, a.valor_compra, a.valor_libros,
               -- Mismo criterio que el resto de las lecturas del módulo: el
               -- histórico guarda la depreciación en valor_depreciado y deja
               -- depreciacion_acumulada en 0.
               GREATEST(coalesce(a.depreciacion_acumulada, 0),
                        coalesce(a.valor_depreciado, 0)) AS acumulada
          FROM public.af_activo_fijo a
         WHERE a.company_id = p_company_id AND a.id = p_activo_fijo_id
    ),
    detalle AS (
        SELECT count(*) AS filas,
               min(nullif(d.anio, 0)) AS desde,
               max(nullif(d.anio, 0)) AS hasta,
               coalesce(sum(d.valor_depreciado), 0) AS total
          FROM public.af_activo_fijo_depreciacion d
          JOIN activo act ON TRUE
         WHERE d.company_id = p_company_id
           AND (d.activo_fijo_id = act.id
                OR (d.activo_fijo_id IS NULL AND d.codigo_activo = act.codigo_activo))
    )
    SELECT det.filas, det.desde, det.hasta, det.total, act.acumulada,
           round(act.acumulada - det.total, 2),
           act.valor_compra, act.valor_libros
      FROM activo act CROSS JOIN detalle det;
$$;

COMMENT ON FUNCTION public.fn_af_activo_depreciacion_resumen(BIGINT, INTEGER) IS
'Totales del historial de un activo enfrentados a lo que declara el maestro. La diferencia distinta de cero señala que el detalle no explica todo el acumulado.';

COMMIT;

-- Verificación
--   SELECT * FROM public.fn_af_activo_depreciacion_resumen(2, 182);
--   SELECT periodo, valor_depreciado, valor_neto_libros
--     FROM public.fn_af_activo_depreciacion_listar(2, 182) LIMIT 12;
