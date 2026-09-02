-- =============================================================================
-- Aprobación por niveles — de escalera acumulativa a LÍMITE DE AUTORIZACIÓN
-- Fecha: 2026-09-01
-- Diseño: docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md (§15)
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- La primera entrega implementó la decisión D1: escalera ACUMULATIVA, donde una orden de 75,000
-- exigía las firmas de los niveles 1, 2 y 3, en ese orden. El usuario pidió lo contrario
-- (2026-09-01): la aprobación NO es en cascada. Cada tramo tiene un MONTO MÁXIMO que puede
-- autorizar, y quien alcance el monto del documento lo aprueba DIRECTAMENTE, sin que los tramos
-- inferiores hayan firmado antes. Una orden de 75,000 la aprueba de una vez quien llegue a
-- 75,000, aunque existan tramos de 10,000 y 50,000.
--
-- QUÉ CAMBIA
--   1) cfg_aprobacion_nivel.monto_desde -> monto_hasta   (invierte el significado)
--      · NULL = sin tope (autoriza cualquier monto).
--   2) limite_utilizado en los dos flujos                (con qué límite se autorizó)
--   3) apr_bitacora: estado_anterior, estado_nuevo, limite_utilizado
--   4) Las tres funciones fn_apr_* se rehacen con la regla nueva.
--
-- QUÉ **NO** CAMBIA
--   · Las tablas, sus PK/FK/índices y el estado 7 de la orden de compra.
--   · Los aprobadores (usuario o rol) siguen colgando del tramo.
--   · El control sigue APAGADO por empresa: aplicar esto no cambia el comportamiento de nadie.
--
-- IMPACTO DE DATOS: NINGUNO. cfg_aprobacion_nivel y cfg_aprobacion_aprobador están VACÍAS, así
-- que el rename no reinterpreta ningún dato capturado; las columnas nuevas nacen NULL sobre las
-- filas de prueba que haya en el flujo y en la bitácora.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) El tramo pasa a tener LÍMITE MÁXIMO en vez de umbral de entrada
--    Bloque idempotente: solo renombra si la columna vieja todavía existe.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = 'public' AND table_name = 'cfg_aprobacion_nivel'
           AND column_name = 'monto_desde'
    ) THEN
        ALTER TABLE public.cfg_aprobacion_nivel RENAME COLUMN monto_desde TO monto_hasta;
    END IF;
END $$;

-- NULL = sin tope. Evita tener que escribir 999,999,999 para la gerencia general.
ALTER TABLE public.cfg_aprobacion_nivel ALTER COLUMN monto_hasta DROP NOT NULL;
ALTER TABLE public.cfg_aprobacion_nivel ALTER COLUMN monto_hasta DROP DEFAULT;

ALTER TABLE public.cfg_aprobacion_nivel DROP CONSTRAINT IF EXISTS ck_cfg_aprobacion_nivel_monto;
ALTER TABLE public.cfg_aprobacion_nivel ADD  CONSTRAINT ck_cfg_aprobacion_nivel_monto
    CHECK (monto_hasta IS NULL OR monto_hasta >= 0);

COMMENT ON TABLE  public.cfg_aprobacion_nivel IS 'Tramos de autorización por monto. Cada tramo dice CUÁNTO puede autorizar quien esté en él; NO es una escalera en cascada.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.nivel IS 'Orden del tramo, 1..9, de menor a mayor capacidad. Sirve para encontrar el tramo más bajo que cubre un monto; NO impone secuencia de firmas.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.monto_hasta IS 'Monto MÁXIMO que este tramo puede autorizar. NULL = sin tope. Un documento lo aprueba de una sola firma quien tenga un tramo con límite >= su total.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.descripcion IS 'Etiqueta del tramo (Jefatura, Gerencia). Se copia como snapshot al flujo cuando alguien autoriza.';

-- -----------------------------------------------------------------------------
-- 2) Con qué límite se autorizó — en los dos flujos
--    Es parte del registro que exige el requerimiento: no basta con saber quién firmó, hay que
--    saber con qué capacidad lo hizo (el tramo pudo cambiar de límite después).
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_orden_compra_aprobacion
    ADD COLUMN IF NOT EXISTS limite_utilizado NUMERIC(14,2) NULL;

ALTER TABLE public.alm_requisicion_aprobacion
    ADD COLUMN IF NOT EXISTS limite_utilizado NUMERIC(14,2) NULL;

COMMENT ON COLUMN public.alm_orden_compra_aprobacion.limite_utilizado IS 'Límite del tramo con el que se autorizó (NULL = tramo sin tope). Snapshot: cambiar el tramo después no reescribe la historia.';
COMMENT ON COLUMN public.alm_requisicion_aprobacion.limite_utilizado IS 'Límite del tramo con el que se autorizó (NULL = tramo sin tope).';
COMMENT ON COLUMN public.alm_orden_compra_aprobacion.nivel IS 'Tramo con el que se autorizó. NO implica que los tramos anteriores hayan firmado: la aprobación no es en cascada.';

-- -----------------------------------------------------------------------------
-- 3) La bitácora registra el cambio de estado y la capacidad usada
-- -----------------------------------------------------------------------------
ALTER TABLE public.apr_bitacora ADD COLUMN IF NOT EXISTS estado_anterior  SMALLINT      NULL;
ALTER TABLE public.apr_bitacora ADD COLUMN IF NOT EXISTS estado_nuevo     SMALLINT      NULL;
ALTER TABLE public.apr_bitacora ADD COLUMN IF NOT EXISTS limite_utilizado NUMERIC(14,2) NULL;

COMMENT ON COLUMN public.apr_bitacora.estado_anterior IS 'Estado del documento ANTES del evento. NULL en las filas anteriores a 2026-09-01.';
COMMENT ON COLUMN public.apr_bitacora.estado_nuevo IS 'Estado del documento DESPUÉS del evento.';
COMMENT ON COLUMN public.apr_bitacora.limite_utilizado IS 'Límite del tramo con el que se autorizó, cuando el evento es una aprobación.';

-- -----------------------------------------------------------------------------
-- 4) Funciones: la regla nueva
--    Las tres cambian de firma o de columnas devueltas, así que se recrean (CREATE OR REPLACE
--    no puede cambiar el tipo de retorno). Son de solo lectura y solo las llama este módulo.
-- -----------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.fn_apr_escalera(BIGINT, VARCHAR, NUMERIC);
DROP FUNCTION IF EXISTS public.fn_apr_es_aprobador(BIGINT, VARCHAR, SMALLINT, VARCHAR, VARCHAR[]);
DROP FUNCTION IF EXISTS public.fn_apr_oc_pendientes(BIGINT, VARCHAR, VARCHAR[]);

-- 4.1 Tramos capaces de autorizar un monto, del más bajo al más alto.
--     El PRIMERO es el tramo mínimo suficiente: el que el requerimiento llama "el primer nivel
--     superior cuya capacidad cubre el total". Vacío = nadie puede autorizar ese monto.
CREATE FUNCTION public.fn_apr_autorizadores(
    p_company_id BIGINT,
    p_documento  VARCHAR,
    p_total      NUMERIC
)
RETURNS TABLE (
    nivel             SMALLINT,
    descripcion       VARCHAR,
    monto_hasta       NUMERIC,
    tiene_aprobadores BOOLEAN
)
LANGUAGE sql
STABLE
AS $$
    SELECT n.nivel,
           n.descripcion,
           n.monto_hasta,
           EXISTS (
               SELECT 1
                 FROM public.cfg_aprobacion_aprobador a
                WHERE a.company_id = n.company_id
                  AND a.nivel_id   = n.id
                  AND a.activo
           ) AS tiene_aprobadores
      FROM public.cfg_aprobacion_nivel n
     WHERE n.company_id = p_company_id
       AND n.documento  = p_documento
       AND n.activo
       -- La capacidad del tramo alcanza el monto. NULL = sin tope: alcanza siempre.
       AND (n.monto_hasta IS NULL OR n.monto_hasta >= COALESCE(p_total, 0))
     ORDER BY n.monto_hasta NULLS LAST, n.nivel;
$$;

COMMENT ON FUNCTION public.fn_apr_autorizadores(BIGINT, VARCHAR, NUMERIC) IS
    'Tramos que pueden autorizar un monto, del límite más bajo al más alto (sin tope al final). Vacío = no hay aprobador con capacidad suficiente.';

-- 4.2 ¿Puede ESTA persona autorizar ESTE monto?
--     Una sola pregunta: ¿figura —por usuario o por rol— en algún tramo activo cuyo límite
--     alcance el total? No se consulta ninguna secuencia: la aprobación no es en cascada.
CREATE FUNCTION public.fn_apr_puede_autorizar(
    p_company_id BIGINT,
    p_documento  VARCHAR,
    p_total      NUMERIC,
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
           AND n.activo
           AND a.activo
           AND (n.monto_hasta IS NULL OR n.monto_hasta >= COALESCE(p_total, 0))
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

COMMENT ON FUNCTION public.fn_apr_puede_autorizar(BIGINT, VARCHAR, NUMERIC, VARCHAR, VARCHAR[]) IS
    'Si una persona puede autorizar un monto: figura en algún tramo activo (por usuario o por rol) cuyo límite lo cubre. Sin secuencia ni cascada.';

-- 4.3 El tramo con el que ESTA persona autorizaría ESTE monto: el más bajo que le alcanza.
--     Es lo que queda registrado como "límite utilizado".
CREATE FUNCTION public.fn_apr_tramo_de(
    p_company_id BIGINT,
    p_documento  VARCHAR,
    p_total      NUMERIC,
    p_usuario    VARCHAR,
    p_roles      VARCHAR[]
)
RETURNS TABLE (
    nivel       SMALLINT,
    descripcion VARCHAR,
    monto_hasta NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT n.nivel, n.descripcion, n.monto_hasta
      FROM public.cfg_aprobacion_nivel n
      JOIN public.cfg_aprobacion_aprobador a
        ON a.company_id = n.company_id
       AND a.nivel_id   = n.id
     WHERE n.company_id = p_company_id
       AND n.documento  = p_documento
       AND n.activo
       AND a.activo
       AND (n.monto_hasta IS NULL OR n.monto_hasta >= COALESCE(p_total, 0))
       AND (
             (    a.tipo = 1
              AND btrim(COALESCE(p_usuario, '')) <> ''
              AND lower(a.valor) = lower(btrim(p_usuario)) )
          OR (    a.tipo = 2
              AND p_roles IS NOT NULL
              AND EXISTS (SELECT 1 FROM unnest(p_roles) r WHERE lower(r) = lower(a.valor)) )
           )
     ORDER BY n.monto_hasta NULLS LAST, n.nivel
     LIMIT 1;
$$;

COMMENT ON FUNCTION public.fn_apr_tramo_de(BIGINT, VARCHAR, NUMERIC, VARCHAR, VARCHAR[]) IS
    'El tramo MÁS BAJO con el que esa persona puede autorizar ese monto. Es el que se registra como límite utilizado.';

-- 4.4 Bandeja: órdenes esperando autorización que ESTA persona puede dar.
--     Sin niveles pendientes ni bloqueos: basta con que su capacidad alcance el total.
CREATE FUNCTION public.fn_apr_oc_pendientes(
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
    SELECT o.id::BIGINT                                                 AS documento_id,
           to_char(o.numero, 'FM00000')::VARCHAR                        AS numero,
           o.fecha                                                      AS fecha,
           COALESCE(p.nombre, p.razon_social, o.cod_proveedor)::VARCHAR AS contraparte,
           o.total                                                      AS total,
           t.nivel                                                      AS nivel,
           t.descripcion                                                AS descripcion_nivel,
           o.usuariocreacion                                            AS creado_por,
           GREATEST(0, (CURRENT_DATE - COALESCE(o.fechamodificacion::DATE, o.fechacreacion::DATE, CURRENT_DATE)))::INTEGER
                                                                        AS dias_en_espera
      FROM public.alm_orden_compra o
      JOIN public.cfg_aprobacion_control c
        ON c.company_id = o.company_id
       AND c.documento  = 'COMPRAS_OC'
      -- El tramo con el que esta persona la autorizaría; sin fila, no puede y no la ve.
      JOIN LATERAL public.fn_apr_tramo_de(o.company_id, 'COMPRAS_OC', o.total, p_usuario, p_roles) t
        ON TRUE
      LEFT JOIN public.prv_proveedores p
        ON p.company_id    = o.company_id
       AND p.cod_proveedor = o.cod_proveedor
     WHERE o.company_id = p_company_id
       AND o.estado     = 7
       -- D5: nadie autoriza lo suyo, salvo que la empresa lo permita.
       AND (
             c.permite_autoaprobacion
          OR lower(COALESCE(o.usuariocreacion, '')) <> lower(btrim(COALESCE(p_usuario, '')))
           )
     ORDER BY dias_en_espera DESC, o.numero;
$$;

COMMENT ON FUNCTION public.fn_apr_oc_pendientes(BIGINT, VARCHAR, VARCHAR[]) IS
    'Bandeja Mis aprobaciones: órdenes en aprobación que esta persona puede autorizar por su límite. Una sola firma, sin cascada.';

-- 4.5 Capacidad por orden, para el listado: ¿hay alguien que pueda autorizarla?
--     Alimenta el aviso «no hay aprobador con límite suficiente», que es un estado real del
--     documento y no un error: la orden se queda esperando a que se configure a alguien.
CREATE FUNCTION public.fn_apr_oc_capacidad(p_company_id BIGINT)
RETURNS TABLE (
    documento_id            BIGINT,
    hay_aprobador_capaz     BOOLEAN,
    limite_minimo_suficiente NUMERIC,
    tramo_minimo            VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT o.id::BIGINT,
           (cap.nivel IS NOT NULL) AS hay_aprobador_capaz,
           cap.monto_hasta,
           cap.descripcion
      FROM public.alm_orden_compra o
      LEFT JOIN LATERAL (
           SELECT a.nivel, a.descripcion, a.monto_hasta
             FROM public.fn_apr_autorizadores(o.company_id, 'COMPRAS_OC', o.total) a
            WHERE a.tiene_aprobadores
            LIMIT 1
      ) cap ON TRUE
     WHERE o.company_id = p_company_id
       AND o.estado     = 7;
$$;

COMMENT ON FUNCTION public.fn_apr_oc_capacidad(BIGINT) IS
    'Por cada orden en aprobación: si existe un tramo CON aprobadores que cubra su total, y cuál es el más bajo. Vacío en hay_aprobador_capaz = nadie puede autorizarla.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
--
--   -- La columna se renombró y admite NULL:
--   SELECT column_name, is_nullable FROM information_schema.columns
--    WHERE table_name = 'cfg_aprobacion_nivel' AND column_name IN ('monto_desde', 'monto_hasta');
--   -- Esperado: una sola fila, monto_hasta, YES.
--
--   -- Las columnas nuevas del registro:
--   SELECT column_name FROM information_schema.columns
--    WHERE table_name = 'apr_bitacora'
--      AND column_name IN ('estado_anterior', 'estado_nuevo', 'limite_utilizado')
--    ORDER BY column_name;
--
--   -- Las cinco funciones de la regla nueva:
--   SELECT proname FROM pg_proc
--    WHERE proname IN ('fn_apr_autorizadores', 'fn_apr_puede_autorizar', 'fn_apr_tramo_de',
--                      'fn_apr_oc_pendientes', 'fn_apr_oc_capacidad')
--    ORDER BY proname;   -- esperado: 5
--
--   -- Y que la vieja ya no está (la escalera acumulativa):
--   SELECT count(*) AS debe_ser_cero FROM pg_proc WHERE proname = 'fn_apr_escalera';
--
--   -- Sin tramos configurados, nadie puede autorizar nada (0 filas):
--   SELECT * FROM public.fn_apr_autorizadores(2::bigint, 'COMPRAS_OC', 75000::numeric);
--
-- REVERSA (vuelve al modelo de escalera; solo válida si no se capturaron tramos)
--   ALTER TABLE public.cfg_aprobacion_nivel RENAME COLUMN monto_hasta TO monto_desde;
--   ALTER TABLE public.cfg_aprobacion_nivel ALTER COLUMN monto_desde SET DEFAULT 0;
--   UPDATE public.cfg_aprobacion_nivel SET monto_desde = 0 WHERE monto_desde IS NULL;
--   ALTER TABLE public.cfg_aprobacion_nivel ALTER COLUMN monto_desde SET NOT NULL;
--   -- y volver a crear fn_apr_escalera / fn_apr_es_aprobador / fn_apr_oc_pendientes
--   -- desde Database/2026-08-31_apr_niveles_02_funciones.sql
-- =============================================================================
