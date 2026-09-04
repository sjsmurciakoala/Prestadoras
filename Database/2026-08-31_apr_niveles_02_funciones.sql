-- =============================================================================
-- Aprobación por niveles — funciones de lectura (Fase 2 de 7)
-- Fecha: 2026-08-31
-- Diseño: docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- El motor (SIAD.Services/Aprobaciones/AprobacionService.cs) no puede resolver con LINQ ni
-- reconstruir en C# la escalera y la elegibilidad: la regla `hodsoft-sin-linq` manda que el
-- acceso a datos viva en funciones y vistas. Estas tres funciones son ESA capa: las tres son
-- de solo lectura y no escriben una fila.
--
-- QUÉ SE CREA
--   1) fn_apr_escalera       -> qué niveles exige un monto (D1, acumulativa).
--   2) fn_apr_es_aprobador   -> si una persona puede firmar un nivel (D3: usuario o rol).
--   3) fn_apr_oc_pendientes  -> bandeja "Mis aprobaciones" de órdenes de compra.
--
-- DEPENDE DE: Database/2026-08-31_apr_niveles_01_estructura.sql (las tablas cfg_aprobacion_*
-- y alm_orden_compra_aprobacion). Aplicar el 01 primero.
--
-- Cambio ADITIVO y re-ejecutable: tres funciones nuevas de solo lectura. No toca tablas,
-- columnas, índices ni datos.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) La escalera de un monto (D1, ACUMULATIVA)
--    Devuelve TODOS los niveles activos cuyo umbral no supera el total. Un total de 75,000
--    con umbrales 0 / 10,000.01 / 50,000.01 devuelve los niveles 1, 2 y 3 — no solo el 3.
--
--    `tiene_aprobadores` viaja con cada nivel para que el motor pueda negarse a ABRIR un flujo
--    que nadie podría firmar, en vez de descubrirlo cuando la orden ya está detenida.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_apr_escalera(
    p_company_id BIGINT,
    p_documento  VARCHAR,
    p_total      NUMERIC
)
RETURNS TABLE (
    nivel             SMALLINT,
    descripcion       VARCHAR,
    monto_desde       NUMERIC,
    tiene_aprobadores BOOLEAN
)
LANGUAGE sql
STABLE
AS $$
    SELECT n.nivel,
           n.descripcion,
           n.monto_desde,
           EXISTS (
               SELECT 1
                 FROM public.cfg_aprobacion_aprobador a
                WHERE a.company_id = n.company_id
                  AND a.nivel_id   = n.id
                  AND a.activo
           ) AS tiene_aprobadores
      FROM public.cfg_aprobacion_nivel n
     WHERE n.company_id  = p_company_id
       AND n.documento   = p_documento
       AND n.activo
       AND n.monto_desde <= COALESCE(p_total, 0)
     ORDER BY n.nivel;
$$;

COMMENT ON FUNCTION public.fn_apr_escalera(BIGINT, VARCHAR, NUMERIC) IS
    'Niveles que exige un monto (escalera acumulativa, D1): todos los activos con monto_desde <= total. Incluye si cada nivel tiene aprobadores activos.';

-- -----------------------------------------------------------------------------
-- 2) Elegibilidad de un firmante (D3)
--    Un aprobador puede estar declarado como USUARIO (tipo 1, el user_name en minúsculas) o
--    como ROL de Identity (tipo 2). Los roles llegan desde la sesión: la base no los conoce
--    porque Identity vive en otro schema y otro DbContext.
--
--    Ambas comparaciones son insensibles a mayúsculas. La del usuario además rechaza el vacío:
--    sin esta guarda, una sesión sin nombre coincidiría con cualquier aprobador mal capturado.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_apr_es_aprobador(
    p_company_id BIGINT,
    p_documento  VARCHAR,
    p_nivel      SMALLINT,
    p_usuario    VARCHAR,
    p_roles      VARCHAR[]
)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1
          FROM public.cfg_aprobacion_nivel n
          JOIN public.cfg_aprobacion_aprobador a
            ON a.company_id = n.company_id
           AND a.nivel_id   = n.id
         WHERE n.company_id = p_company_id
           AND n.documento  = p_documento
           AND n.nivel      = p_nivel
           AND n.activo
           AND a.activo
           AND (
                 (    a.tipo = 1
                  AND btrim(COALESCE(p_usuario, '')) <> ''
                  AND lower(a.valor) = lower(btrim(p_usuario)) )
              OR (    a.tipo = 2
                  AND p_roles IS NOT NULL
                  AND EXISTS (SELECT 1 FROM unnest(p_roles) r WHERE lower(r) = lower(a.valor)) )
               )
    );
$$;

COMMENT ON FUNCTION public.fn_apr_es_aprobador(BIGINT, VARCHAR, SMALLINT, VARCHAR, VARCHAR[]) IS
    'Si una persona puede firmar un nivel: por usuario nominal (tipo 1) o por rol de Identity (tipo 2). Comparación insensible a mayúsculas; usuario vacío nunca coincide.';

-- -----------------------------------------------------------------------------
-- 3) Bandeja "Mis aprobaciones" de órdenes de compra
--    Devuelve SOLO lo que esa persona puede firmar AHORA. Aplica las cuatro reglas del motor,
--    para que la bandeja no ofrezca botones que después van a fallar:
--      a) el nivel está Pendiente y la orden está En aprobación (7);
--      b) la persona es aprobador elegible del nivel;
--      c) no es el creador de la orden, salvo que la empresa permita autoaprobación (D5);
--      d) no firmó ya otro nivel de la misma orden (separación de funciones).
--
--    `dias_en_espera` se cuenta desde el último movimiento real del flujo —la firma anterior si
--    la hubo, o el envío— y no desde la fecha del documento, que puede ser muy anterior.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_apr_oc_pendientes(
    p_company_id BIGINT,
    p_usuario    VARCHAR,
    p_roles      VARCHAR[]
)
RETURNS TABLE (
    documento_id      BIGINT,
    numero            VARCHAR,
    fecha             DATE,
    contraparte       VARCHAR,
    total             NUMERIC,
    nivel             SMALLINT,
    descripcion_nivel VARCHAR,
    creado_por        VARCHAR,
    dias_en_espera    INTEGER
)
LANGUAGE sql
STABLE
AS $$
    -- Los alias son obligatorios, no decorativos: sin ellos el ORDER BY de abajo no puede
    -- nombrar la columna calculada (`no existe la columna dias_en_espera`).
    SELECT o.id::BIGINT                                                        AS documento_id,
           to_char(o.numero, 'FM00000')::VARCHAR                               AS numero,
           o.fecha                                                             AS fecha,
           COALESCE(p.nombre, p.razon_social, o.cod_proveedor)::VARCHAR        AS contraparte,
           o.total                                                             AS total,
           f.nivel                                                             AS nivel,
           f.descripcion                                                       AS descripcion_nivel,
           o.usuariocreacion                                                   AS creado_por,
           GREATEST(0, (CURRENT_DATE - COALESCE(espera.desde, CURRENT_DATE)))::INTEGER
                                                                               AS dias_en_espera
      FROM public.alm_orden_compra_aprobacion f
      JOIN public.alm_orden_compra o
        ON o.company_id = f.company_id
       AND o.id         = f.orden_compra_id
      JOIN public.cfg_aprobacion_control c
        ON c.company_id = f.company_id
       AND c.documento  = 'COMPRAS_OC'
      LEFT JOIN public.prv_proveedores p
        ON p.company_id    = o.company_id
       AND p.cod_proveedor = o.cod_proveedor
      LEFT JOIN LATERAL (
           SELECT COALESCE(MAX(f2.fecha_firma), MIN(f2.fechacreacion))::DATE AS desde
             FROM public.alm_orden_compra_aprobacion f2
            WHERE f2.company_id      = f.company_id
              AND f2.orden_compra_id = f.orden_compra_id
      ) espera ON TRUE
     WHERE f.company_id = p_company_id
       AND f.estado     = 2                      -- nivel Pendiente
       AND o.estado     = 7                      -- orden En aprobación
       -- (b) elegibilidad por usuario o por rol
       AND public.fn_apr_es_aprobador(f.company_id, 'COMPRAS_OC', f.nivel, p_usuario, p_roles)
       -- (c) D5: nadie firma lo suyo, salvo que la empresa lo permita
       AND (
             c.permite_autoaprobacion
          OR lower(COALESCE(o.usuariocreacion, '')) <> lower(btrim(COALESCE(p_usuario, '')))
           )
       -- (d) una sola firma por persona y documento
       AND NOT EXISTS (
             SELECT 1
               FROM public.alm_orden_compra_aprobacion f3
              WHERE f3.company_id      = f.company_id
                AND f3.orden_compra_id = f.orden_compra_id
                AND lower(COALESCE(f3.usuario_firma, '')) = lower(btrim(COALESCE(p_usuario, '')))
           )
     ORDER BY dias_en_espera DESC, o.numero;
$$;

COMMENT ON FUNCTION public.fn_apr_oc_pendientes(BIGINT, VARCHAR, VARCHAR[]) IS
    'Bandeja Mis aprobaciones: órdenes de compra que esta persona puede firmar ahora. Aplica elegibilidad, autoaprobación (D5) y una-firma-por-persona, para no ofrecer acciones que fallarían.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
--
--   -- Las tres funciones existen:
--   SELECT proname FROM pg_proc
--    WHERE proname IN ('fn_apr_escalera', 'fn_apr_es_aprobador', 'fn_apr_oc_pendientes')
--    ORDER BY proname;
--
--   -- Sin escalera configurada, ningún monto exige nada (0 filas):
--   SELECT * FROM public.fn_apr_escalera(2, 'COMPRAS_OC', 999999);
--
--   -- Nadie es aprobador de un nivel que no existe (false):
--   SELECT public.fn_apr_es_aprobador(2, 'COMPRAS_OC', 1::smallint, 'quien@sea.com', ARRAY['Admin']::varchar[]);
--
--   -- La bandeja está vacía mientras no haya flujos abiertos (0 filas):
--   SELECT * FROM public.fn_apr_oc_pendientes(2, 'quien@sea.com', ARRAY['Admin']::varchar[]);
--
-- REVERSA
--   DROP FUNCTION IF EXISTS public.fn_apr_oc_pendientes(BIGINT, VARCHAR, VARCHAR[]);
--   DROP FUNCTION IF EXISTS public.fn_apr_es_aprobador(BIGINT, VARCHAR, SMALLINT, VARCHAR, VARCHAR[]);
--   DROP FUNCTION IF EXISTS public.fn_apr_escalera(BIGINT, VARCHAR, NUMERIC);
-- =============================================================================
