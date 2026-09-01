-- =============================================================================
-- Control presupuestario con COMPROMISO en la O/C — funciones auxiliares
-- Fecha: 2026-08-27
-- Fase F1 (2 de 4). Requiere: 2026-08-27_pst_compromiso_01_estructura.sql
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- Los procedimientos del script 03 necesitan cuatro respuestas que conviene tener aisladas,
-- probables por separado y reutilizables desde la UI:
--   1) ¿cuál es la partida presupuestaria vigente de esta cuenta a esta fecha?
--   2) ¿cuánto disponible tiene? (lectura SIN lock, para el panel previo de la pantalla)
--   3) ¿cómo se reparte esta O/C entre partidas?
--   4) ¿cómo se reparte esta FACTURA entre partidas? (debe coincidir con el DEBE del asiento)
--
-- Todas son de LECTURA salvo fn_pst_recalcular_cabecera. Ninguna reemplaza objetos existentes:
-- los cinco nombres son nuevos.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) fn_pst_resolver_partida — qué partida rige para una cuenta a una fecha
--    Criterio IDÉNTICO al que ya usa fn_pst_afectar_saldo_real_credito (créditos bancarios):
--    el presupuesto vigente más reciente. No se inventa un segundo modelo mental.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_resolver_partida(
    p_company_id        BIGINT,
    p_cuenta            VARCHAR,
    p_fecha             DATE,
    p_requiere_aprobado BOOLEAN DEFAULT TRUE
)
RETURNS TABLE (id_presupuesto VARCHAR, con_cuenta_code VARCHAR)
LANGUAGE sql
STABLE
AS $$
    SELECT d.id_presupuesto, d.con_cuenta_code
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.company_id = d.company_id
       AND h.id_presupuesto = d.id_presupuesto
     WHERE d.company_id = p_company_id
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(p_cuenta))
       AND p_fecha BETWEEN h.fecha_inicia AND h.fecha_finaliza
       AND (NOT p_requiere_aprobado OR h.estado_aprobado)
     ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC
     LIMIT 1;
$$;

COMMENT ON FUNCTION public.fn_pst_resolver_partida(BIGINT, VARCHAR, DATE, BOOLEAN) IS
    'Partida presupuestaria vigente de una cuenta a una fecha. Sin filas = no hay presupuesto para esa cuenta. Mismo criterio que fn_pst_afectar_saldo_real_credito.';

-- -----------------------------------------------------------------------------
-- 2) fn_pst_disponible — cuánto queda (lectura, SIN lock)
--    La usa la UI para mostrar el disponible ANTES de aprobar. Ver el tope después del
--    rechazo es la peor forma de enterarse.
--    Devuelve NULL si no hay partida: distingue "sin presupuesto" de "presupuesto agotado".
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_disponible(
    p_company_id BIGINT,
    p_cuenta     VARCHAR,
    p_fecha      DATE
)
RETURNS NUMERIC
LANGUAGE sql
STABLE
AS $$
    SELECT GREATEST(
               COALESCE(d.valor_proyeccion, 0)
             - COALESCE(d.valor_comprometido, 0)
             - COALESCE(d.valor_real, 0), 0)
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.company_id = d.company_id
       AND h.id_presupuesto = d.id_presupuesto
     WHERE d.company_id = p_company_id
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(p_cuenta))
       AND p_fecha BETWEEN h.fecha_inicia AND h.fecha_finaliza
     ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC
     LIMIT 1;
$$;

COMMENT ON FUNCTION public.fn_pst_disponible(BIGINT, VARCHAR, DATE) IS
    'Disponible = proyeccion - comprometido - real. Lectura sin lock, para el panel previo de la UI. NULL = la cuenta no tiene partida vigente.';

-- -----------------------------------------------------------------------------
-- 3) fn_pst_recalcular_cabecera — mantiene los totales del encabezado
--    OJO con la inconsistencia PREEXISTENTE que este script NO corrige: la cabecera se calcula
--    contra valor_global mientras el detalle lo hace contra valor_proyeccion. Son bases
--    distintas y pueden no cuadrar si valor_global <> SUMA(valor_proyeccion).
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_pst_recalcular_cabecera(
    p_company_id     BIGINT,
    p_id_presupuesto VARCHAR
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
BEGIN
    -- El alias es "ejecutado", no "real": REAL es un nombre de tipo en PostgreSQL y como alias
    -- de columna se presta a sorpresas al referenciarlo.
    UPDATE public.pst_config_presupuesto_hdr h
       SET valor_comprometido = COALESCE(s.comprometido, 0),
           valor_disponible   = GREATEST(h.valor_global - COALESCE(s.comprometido, 0) - COALESCE(s.ejecutado, 0), 0)
      FROM (
            SELECT SUM(d.valor_comprometido) AS comprometido,
                   SUM(d.valor_real)         AS ejecutado
              FROM public.pst_config_presupuesto_dtl d
             WHERE d.company_id = p_company_id
               AND d.id_presupuesto = p_id_presupuesto
           ) s
     WHERE h.company_id = p_company_id
       AND h.id_presupuesto = p_id_presupuesto;
END;
$$;

COMMENT ON FUNCTION public.fn_pst_recalcular_cabecera(BIGINT, VARCHAR) IS
    'Recalcula valor_comprometido y valor_disponible del encabezado a partir de sus detalles.';

-- -----------------------------------------------------------------------------
-- 4) fn_alm_oc_distribucion_partidas — cómo se reparte una O/C entre partidas
--
--    Regla (replica la del asiento de la compra):
--      base del renglón = cantidad_pedida * costo_unitario
--      remanente        = total de la O/C - SUMA(bases)   [ISV, otros gastos, descuento global]
--      el remanente se capitaliza en la cuenta de MAYOR valor, y dentro de ella en su renglón
--      de mayor base, de modo que SUMA(líneas) = alm_orden_compra.total
--
--    Cuenta del renglón: la congelada en cuenta_presupuestaria; si aún no se capturó, se propone
--    la del tipo de artículo. Un renglón sin cuenta resoluble se devuelve con con_cuenta_code en
--    NULL A PROPÓSITO: el procedimiento decide qué hacer (error en modo Bloqueo, aviso en modo
--    Advertencia), en vez de desaparecer en silencio y comprometer de menos.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_alm_oc_distribucion_partidas(
    p_company_id      BIGINT,
    p_orden_compra_id BIGINT
)
RETURNS SETOF public.pst_linea_afectacion
LANGUAGE sql
STABLE
AS $$
    WITH cab AS (
        SELECT o.id, COALESCE(o.total, 0) AS total
          FROM public.alm_orden_compra o
         WHERE o.company_id = p_company_id
           AND o.id = p_orden_compra_id
    ),
    base AS (
        SELECT d.id AS detalle_id,
               NULLIF(upper(btrim(COALESCE(d.cuenta_presupuestaria, t.cuenta_inventario))), '') AS cuenta,
               d.centro_costo_id,
               COALESCE(d.cantidad_pedida, 0) * COALESCE(d.costo_unitario, 0) AS monto
          FROM public.alm_orden_compra_detalle d
          LEFT JOIN public.alm_articulo a
                 ON a.company_id = d.company_id AND a.id = d.articulo_id
          LEFT JOIN public.alm_tipo_articulo t
                 ON t.company_id = a.company_id AND t.id = a.tipo_articulo_id
         WHERE d.company_id = p_company_id
           AND d.orden_compra_id = p_orden_compra_id
    ),
    positivos AS (
        SELECT * FROM base WHERE monto > 0
    ),
    cuenta_mayor AS (
        SELECT cuenta
          FROM positivos
         WHERE cuenta IS NOT NULL
         GROUP BY cuenta
         ORDER BY SUM(monto) DESC, cuenta
         LIMIT 1
    ),
    destino AS (
        SELECT p.detalle_id
          FROM positivos p
         WHERE p.cuenta = (SELECT cuenta FROM cuenta_mayor)
         ORDER BY p.monto DESC, p.detalle_id
         LIMIT 1
    ),
    remanente AS (
        SELECT round((SELECT total FROM cab) - COALESCE((SELECT SUM(monto) FROM positivos), 0), 2) AS r
    )
    SELECT p.cuenta::VARCHAR(20),
           p.centro_costo_id::BIGINT,
           p.detalle_id::BIGINT,
           round(p.monto + CASE WHEN p.detalle_id = (SELECT detalle_id FROM destino)
                                THEN (SELECT r FROM remanente) ELSE 0 END, 4)::NUMERIC(18,4)
      FROM positivos p
     ORDER BY p.cuenta NULLS LAST, p.detalle_id;
$$;

COMMENT ON FUNCTION public.fn_alm_oc_distribucion_partidas(BIGINT, BIGINT) IS
    'Distribuye una O/C entre partidas presupuestarias. SUMA(montos) = alm_orden_compra.total. Un renglón sin cuenta resoluble sale con con_cuenta_code NULL para que el SP lo reporte.';

-- -----------------------------------------------------------------------------
-- 5) fn_alm_compra_distribucion_partidas — lo mismo para la FACTURA
--
--    Debe devolver EXACTAMENTE el DEBE del asiento de CompraContabilidad. La regla del asiento
--    (CompraContabilidad.cs) es:
--      base del renglón = cantidad * precio_unitario + (impuesto si el ISV se capitaliza)
--      remanente        = total de la factura - SUMA(bases)   [flete, otros gastos, descuento]
--      remanente a la cuenta de mayor valor
--
--    Capitalización del ISV: alm_compra_hdr.detallar_isv manda cuando no es NULL; si es NULL se
--    resuelve por cfg_compra_isv.tratamiento de la empresa (COSTO capitaliza, FISCAL no).
--    Es la misma cascada de RecepcionCompraService.ResolverSiCapitalizaIsvAsync.
--
--    La cuenta sale del renglón de la O/C cuando la factura viene enlazada: así el DEVENGO
--    muerde la MISMA partida que se comprometió. Sin enlace (compra directa), la del tipo.
--
--    ⚠️ Duplicación deliberada de la regla en dos lugares (aquí y en CompraContabilidad) hasta
--    la fase F8, cubierta por el test de equivalencia (caso 29 del diseño).
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_alm_compra_distribucion_partidas(
    p_company_id    BIGINT,
    p_compra_hdr_id BIGINT
)
RETURNS SETOF public.pst_linea_afectacion
LANGUAGE sql
STABLE
AS $$
    WITH cab AS (
        SELECT h.id,
               COALESCE(h.total, 0) AS total,
               CASE
                   WHEN h.detallar_isv IS NOT NULL THEN NOT h.detallar_isv
                   ELSE COALESCE(
                          (SELECT upper(btrim(ci.tratamiento))
                             FROM public.cfg_compra_isv ci
                            WHERE ci.company_id = p_company_id
                            LIMIT 1), 'COSTO') = 'COSTO'
               END AS capitaliza_isv
          FROM public.alm_compra_hdr h
         WHERE h.company_id = p_company_id
           AND h.id = p_compra_hdr_id
    ),
    base AS (
        SELECT c.id AS detalle_id,
               NULLIF(upper(btrim(COALESCE(ocd.cuenta_presupuestaria, t.cuenta_inventario))), '') AS cuenta,
               ocd.centro_costo_id,
               COALESCE(c.cantidad, 0) * COALESCE(c.precio_unitario, 0)
                 + CASE WHEN (SELECT capitaliza_isv FROM cab) THEN COALESCE(c.impuesto, 0) ELSE 0 END AS monto
          FROM public.alm_compra c
          LEFT JOIN public.alm_articulo a
                 ON a.company_id = c.company_id AND a.id = c.articulo_id
          LEFT JOIN public.alm_tipo_articulo t
                 ON t.company_id = a.company_id AND t.id = a.tipo_articulo_id
          LEFT JOIN public.alm_orden_compra_detalle ocd
                 ON ocd.company_id = c.company_id AND ocd.id = c.orden_compra_detalle_id
         WHERE c.company_id = p_company_id
           AND c.compra_hdr_id = p_compra_hdr_id
    ),
    positivos AS (
        SELECT * FROM base WHERE monto > 0
    ),
    cuenta_mayor AS (
        SELECT cuenta
          FROM positivos
         WHERE cuenta IS NOT NULL
         GROUP BY cuenta
         ORDER BY SUM(monto) DESC, cuenta
         LIMIT 1
    ),
    destino AS (
        SELECT p.detalle_id
          FROM positivos p
         WHERE p.cuenta = (SELECT cuenta FROM cuenta_mayor)
         ORDER BY p.monto DESC, p.detalle_id
         LIMIT 1
    ),
    remanente AS (
        SELECT round((SELECT total FROM cab) - COALESCE((SELECT SUM(monto) FROM positivos), 0), 2) AS r
    )
    SELECT p.cuenta::VARCHAR(20),
           p.centro_costo_id::BIGINT,
           p.detalle_id::BIGINT,
           round(p.monto + CASE WHEN p.detalle_id = (SELECT detalle_id FROM destino)
                                THEN (SELECT r FROM remanente) ELSE 0 END, 4)::NUMERIC(18,4)
      FROM positivos p
     ORDER BY p.cuenta NULLS LAST, p.detalle_id;
$$;

COMMENT ON FUNCTION public.fn_alm_compra_distribucion_partidas(BIGINT, BIGINT) IS
    'Distribuye una factura de compra entre partidas. Debe coincidir con el DEBE del asiento de CompraContabilidad (test de equivalencia). SUMA(montos) = alm_compra_hdr.total.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT; empresa 2 = MERENDON)
-- =============================================================================
-- a) Las 5 funciones existen
-- SELECT proname, pg_get_function_identity_arguments(oid)
--   FROM pg_proc
--  WHERE proname IN ('fn_pst_resolver_partida', 'fn_pst_disponible', 'fn_pst_recalcular_cabecera',
--                    'fn_alm_oc_distribucion_partidas', 'fn_alm_compra_distribucion_partidas')
--  ORDER BY proname;
-- Esperado: 5 filas.
--
-- b) Resuelve una partida real y su disponible (tomando una cuenta presupuestada cualquiera)
-- WITH una AS (
--   SELECT d.con_cuenta_code, h.fecha_inicia
--     FROM public.pst_config_presupuesto_dtl d
--     JOIN public.pst_config_presupuesto_hdr h
--       ON h.company_id = d.company_id AND h.id_presupuesto = d.id_presupuesto
--    WHERE d.company_id = 2 AND h.estado_aprobado AND d.valor_proyeccion > 0
--    LIMIT 1)
-- SELECT u.con_cuenta_code,
--        (SELECT id_presupuesto FROM public.fn_pst_resolver_partida(2, u.con_cuenta_code, u.fecha_inicia)) AS partida,
--        public.fn_pst_disponible(2, u.con_cuenta_code, u.fecha_inicia) AS disponible
--   FROM una u;
--
-- c) La distribución de una O/C cuadra contra su total (diferencia debe ser 0.00)
-- WITH oc AS (SELECT id, total FROM public.alm_orden_compra WHERE company_id = 2 AND total > 0 LIMIT 1)
-- SELECT o.id, o.total,
--        (SELECT SUM(monto) FROM public.fn_alm_oc_distribucion_partidas(2, o.id)) AS distribuido,
--        o.total - (SELECT SUM(monto) FROM public.fn_alm_oc_distribucion_partidas(2, o.id)) AS diferencia,
--        (SELECT count(*) FROM public.fn_alm_oc_distribucion_partidas(2, o.id) WHERE con_cuenta_code IS NULL) AS renglones_sin_cuenta
--   FROM oc o;
--
-- d) Lo mismo para una factura de compra
-- WITH f AS (SELECT id, total FROM public.alm_compra_hdr WHERE company_id = 2 AND total > 0 AND estado = 1 LIMIT 1)
-- SELECT f.id, f.total,
--        (SELECT SUM(monto) FROM public.fn_alm_compra_distribucion_partidas(2, f.id)) AS distribuido,
--        f.total - (SELECT SUM(monto) FROM public.fn_alm_compra_distribucion_partidas(2, f.id)) AS diferencia
--   FROM f;
--
-- ⚠️ En (c) y (d), renglones_sin_cuenta > 0 significa que hay artículos cuyo tipo no tiene
--    cuenta_inventario configurada. No es un fallo del script: es configuración pendiente que
--    el control reportará al encenderse.
--
-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- DROP FUNCTION IF EXISTS public.fn_alm_compra_distribucion_partidas(BIGINT, BIGINT);
-- DROP FUNCTION IF EXISTS public.fn_alm_oc_distribucion_partidas(BIGINT, BIGINT);
-- DROP FUNCTION IF EXISTS public.fn_pst_recalcular_cabecera(BIGINT, VARCHAR);
-- DROP FUNCTION IF EXISTS public.fn_pst_disponible(BIGINT, VARCHAR, DATE);
-- DROP FUNCTION IF EXISTS public.fn_pst_resolver_partida(BIGINT, VARCHAR, DATE, BOOLEAN);
