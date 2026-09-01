-- =============================================================================
-- Proveedores: cuentas por pagar UNIFICADAS (facturas de compra + compromisos)
-- Fecha: 2026-08-22
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- QUE ES
--   Las dos funciones de LECTURA que alimentan la pantalla unica de cuentas por
--   pagar (/proveedores/cuentas-por-pagar), donde una fila es un DOCUMENTO POR
--   PAGAR sin importar de que modulo nace:
--     origen 1 = factura de compra (alm_compra_cxp)
--     origen 2 = compromiso / orden de pago directo (prv_compromiso_hdr)
--
-- POR QUE NO SE REPITE LA UNION
--   fn_prv_estado_cuenta_documentos (2026-08-13) ya une las dos ramas y concentra
--   las reglas de vigencia (CxP anulada fuera, compromiso anulado fuera y la compat
--   legacy del compromiso procesado sin abonos = saldado). Esa funcion exige UN
--   proveedor; aqui se corre sobre TODOS via CROSS JOIN LATERAL, igual que hace
--   fn_prv_antiguedad_saldos (2026-08-14). Si manana cambia que es deuda viva,
--   cambia en un solo lugar y las tres pantallas se enteran.
--
-- LO QUE SI SE DECIDE AQUI (D1 del plan: vencimiento del compromiso)
--   El compromiso NO tiene fecha de vencimiento propia; la funcion base devuelve su
--   fecha de emision en fecha_vencimiento, con lo que nace "vencido" el mismo dia.
--   Aqui se devuelve fecha_vencimiento y dias_vencido en NULL para origen 2: en
--   pantalla se lee "sin plazo" y nunca entra al conteo de vencidos. La antiguedad
--   de saldos y el estado de cuenta NO cambian: siguen leyendo la funcion base.
--
-- SALDO
--   Derivado (monto - SUM(abonos vigentes)), como en el estado de cuenta, no el
--   saldo materializado de alm_compra_cxp: asi las tres pantallas cuadran entre si.
--
-- ADITIVO Y REVERSIBLE: solo CREATE OR REPLACE FUNCTION de lectura, con nombres
-- nuevos. No crea ni altera ninguna tabla, columna, indice, constraint ni dato, y
-- no reemplaza ninguna funcion existente. Re-ejecutable las veces que haga falta.
-- Se deshace con DROP FUNCTION de estos dos nombres.
-- Depende de: fn_prv_estado_cuenta_documentos [2026-08-13], alm_compra_cxp
--             [2026-08-12], prv_compromiso_hdr con company_id [2026-07-10],
--             prv_proveedores.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1. Documentos por pagar de TODOS los proveedores, con los filtros de la pantalla
--
--    p_origen          0 = ambos, 1 = solo compras, 2 = solo compromisos.
--    p_estado_id       escala EstadoCompraCxp: 1 Pendiente, 2 Parcial, 3 Pagada.
--    p_solo_vencidos   solo los que conservan saldo y ya pasaron su plazo (los
--                      compromisos, al no tener plazo, quedan siempre fuera).
--    p_incluir_pagados FALSE (defecto) = solo lo que conserva saldo (D2 del plan).
--    p_search          proveedor, codigo, numero de documento o concepto.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_prv_cxp_documentos(
    p_company_id      BIGINT,
    p_search          VARCHAR  DEFAULT NULL,
    p_origen          SMALLINT DEFAULT 0,
    p_estado_id       SMALLINT DEFAULT NULL,
    p_cod_proveedor   VARCHAR  DEFAULT NULL,
    p_solo_vencidos   BOOLEAN  DEFAULT FALSE,
    p_incluir_pagados BOOLEAN  DEFAULT FALSE
)
RETURNS TABLE (
    origen            SMALLINT,
    documento_id      BIGINT,
    numero_documento  TEXT,
    cod_proveedor     TEXT,
    proveedor         TEXT,
    fecha             DATE,
    fecha_vencimiento DATE,
    concepto          TEXT,
    monto             NUMERIC,
    abonado           NUMERIC,
    saldo             NUMERIC,
    dias_vencido      INTEGER,
    estado_id         SMALLINT,
    procesado         BOOLEAN
)
LANGUAGE sql
STABLE
AS $$
    WITH universo AS (
        -- Proveedores que tienen al menos un documento de las ramas pedidas. Se filtra
        -- aqui por proveedor para no pagar el LATERAL de los que no interesan.
        SELECT DISTINCT TRIM(u.cod_proveedor) AS cod_proveedor
        FROM (
            SELECT c.cod_proveedor
            FROM public.alm_compra_cxp c
            WHERE c.company_id = p_company_id
              AND c.estado_id <> 9
              AND (COALESCE(p_origen, 0) = 0 OR p_origen = 1)

            UNION ALL

            SELECT h.cod_proveedor
            FROM public.prv_compromiso_hdr h
            WHERE h.company_id = p_company_id
              AND h.anulado    = FALSE
              AND (COALESCE(p_origen, 0) = 0 OR p_origen = 2)
        ) u
        WHERE NULLIF(TRIM(u.cod_proveedor), '') IS NOT NULL
          AND (p_cod_proveedor IS NULL OR TRIM(u.cod_proveedor) = TRIM(p_cod_proveedor))
    ),
    docs AS (
        -- La funcion BASE pone las reglas de vigencia y el saldo; aqui solo se elige
        -- el universo y las ramas.
        SELECT u.cod_proveedor,
               d.origen,
               d.documento_id,
               d.numero_documento,
               d.fecha,
               d.fecha_vencimiento,
               d.concepto,
               d.monto,
               d.abonado,
               d.saldo,
               d.dias_vencido,
               d.estado_id
        FROM universo u
        CROSS JOIN LATERAL public.fn_prv_estado_cuenta_documentos(
                       p_company_id, u.cod_proveedor, NULL, NOT COALESCE(p_incluir_pagados, FALSE)) d
        WHERE (COALESCE(p_origen, 0) = 0 OR d.origen = p_origen)
    ),
    presentable AS (
        -- D1: el compromiso no tiene plazo propio -> vencimiento y dias en NULL.
        SELECT d.*,
               CASE WHEN d.origen = 1 THEN d.fecha_vencimiento END AS venc_efectivo,
               CASE WHEN d.origen = 1 THEN d.dias_vencido      END AS dias_efectivo
        FROM docs d
    )
    SELECT pr.origen,
           pr.documento_id,
           pr.numero_documento,
           pr.cod_proveedor::TEXT,
           COALESCE(NULLIF(TRIM(p.nombre), ''), pr.cod_proveedor)::TEXT AS proveedor,
           pr.fecha,
           pr.venc_efectivo                                             AS fecha_vencimiento,
           pr.concepto,
           pr.monto,
           pr.abonado,
           pr.saldo,
           pr.dias_efectivo                                             AS dias_vencido,
           pr.estado_id,
           -- Solo aplica al compromiso: ya paso por "emitir pago".
           COALESCE(h.status_transacc, FALSE)                           AS procesado
    FROM presentable pr
    -- prv_proveedores es keyless y su company_id es int4: cast explicito. LEFT JOIN
    -- para que un documento cuyo proveedor no este en el maestro igual aparezca.
    LEFT JOIN public.prv_proveedores p
           ON p.company_id           = p_company_id::INTEGER
          AND TRIM(p.cod_proveedor)  = pr.cod_proveedor
    LEFT JOIN public.prv_compromiso_hdr h
           ON pr.origen       = 2
          AND h.company_id    = p_company_id
          AND h.numero_orden  = pr.documento_id::INTEGER
    WHERE (p_estado_id IS NULL OR pr.estado_id = p_estado_id)
      AND (NOT COALESCE(p_solo_vencidos, FALSE)
           OR (pr.dias_efectivo > 0 AND pr.saldo > 0))
      AND (NULLIF(TRIM(COALESCE(p_search, '')), '') IS NULL
           OR pr.cod_proveedor    ILIKE '%' || TRIM(p_search) || '%'
           OR COALESCE(p.nombre, '')       ILIKE '%' || TRIM(p_search) || '%'
           OR pr.numero_documento ILIKE '%' || TRIM(p_search) || '%'
           OR pr.concepto         ILIKE '%' || TRIM(p_search) || '%')
    -- Primero lo que tiene plazo, por vencimiento; los sin plazo al final por fecha.
    ORDER BY (pr.venc_efectivo IS NULL), pr.venc_efectivo, pr.fecha, pr.origen, pr.documento_id;
$$;

COMMENT ON FUNCTION public.fn_prv_cxp_documentos(BIGINT, VARCHAR, SMALLINT, SMALLINT, VARCHAR, BOOLEAN, BOOLEAN) IS
    'Cuentas por pagar unificadas de todos los proveedores: facturas de compra (origen 1) y compromisos (origen 2) como un solo listado, con su abonado, saldo y estado. Reutiliza fn_prv_estado_cuenta_documentos via LATERAL (no duplica reglas de vigencia). El compromiso se devuelve SIN vencimiento (fecha_vencimiento y dias_vencido en NULL) porque no tiene plazo propio. estado_id 1 Pendiente/2 Parcial/3 Pagada.';

-- -----------------------------------------------------------------------------
-- 2. Resumen de la pantalla (KPIs), con los MISMOS filtros que el listado para que
--    el encabezado cuadre siempre con lo que se ve en el grid.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_prv_cxp_resumen(
    p_company_id      BIGINT,
    p_search          VARCHAR  DEFAULT NULL,
    p_origen          SMALLINT DEFAULT 0,
    p_estado_id       SMALLINT DEFAULT NULL,
    p_cod_proveedor   VARCHAR  DEFAULT NULL,
    p_solo_vencidos   BOOLEAN  DEFAULT FALSE,
    p_incluir_pagados BOOLEAN  DEFAULT FALSE
)
RETURNS TABLE (
    saldo_total            NUMERIC,
    saldo_vencido          NUMERIC,
    saldo_vence_7dias      NUMERIC,
    saldo_compras          NUMERIC,
    saldo_compromisos      NUMERIC,
    documentos_pendientes  INTEGER,
    compras_pendientes     INTEGER,
    compromisos_pendientes INTEGER,
    documentos_vencidos    INTEGER
)
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(SUM(d.saldo), 0)                                                          AS saldo_total,
           COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido > 0), 0)                        AS saldo_vencido,
           -- "vence pronto" solo tiene sentido en lo que tiene plazo: los compromisos
           -- llegan con dias_vencido NULL y quedan fuera por el propio FILTER.
           COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido BETWEEN -7 AND 0), 0)           AS saldo_vence_7dias,
           COALESCE(SUM(d.saldo) FILTER (WHERE d.origen = 1), 0)                              AS saldo_compras,
           COALESCE(SUM(d.saldo) FILTER (WHERE d.origen = 2), 0)                              AS saldo_compromisos,
           COUNT(*) FILTER (WHERE d.saldo > 0)::INTEGER                                       AS documentos_pendientes,
           COUNT(*) FILTER (WHERE d.saldo > 0 AND d.origen = 1)::INTEGER                      AS compras_pendientes,
           COUNT(*) FILTER (WHERE d.saldo > 0 AND d.origen = 2)::INTEGER                      AS compromisos_pendientes,
           COUNT(*) FILTER (WHERE d.saldo > 0 AND d.dias_vencido > 0)::INTEGER                AS documentos_vencidos
    FROM public.fn_prv_cxp_documentos(
             p_company_id, p_search, p_origen, p_estado_id, p_cod_proveedor,
             p_solo_vencidos, p_incluir_pagados) d;
$$;

COMMENT ON FUNCTION public.fn_prv_cxp_resumen(BIGINT, VARCHAR, SMALLINT, SMALLINT, VARCHAR, BOOLEAN, BOOLEAN) IS
    'Totales de la pantalla unificada de cuentas por pagar (saldo, vencido, vence en 7 dias y desglose compras/compromisos) sobre fn_prv_cxp_documentos con los mismos filtros.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar; company 2 = MERENDON)
-- =============================================================================
-- 1) Las funciones existen con la firma esperada:
-- SELECT proname, pg_get_function_identity_arguments(oid)
--   FROM pg_proc WHERE proname IN ('fn_prv_cxp_documentos', 'fn_prv_cxp_resumen');
--
-- 2) Listado unificado pendiente (lo que vera la pantalla al abrirla):
-- SELECT origen, numero_documento, proveedor, fecha, fecha_vencimiento, saldo, estado_id
--   FROM fn_prv_cxp_documentos(2) LIMIT 30;
--
-- 3) Los compromisos salen SIN plazo (D1): ambas columnas deben venir en NULL.
-- SELECT COUNT(*) AS compromisos, COUNT(fecha_vencimiento) AS con_vencimiento
--   FROM fn_prv_cxp_documentos(2, NULL, 2::SMALLINT);
--   -- esperado: con_vencimiento = 0
--
-- 4) El total unificado = compras + compromisos:
-- SELECT saldo_total, saldo_compras, saldo_compromisos,
--        saldo_total - (saldo_compras + saldo_compromisos) AS diferencia
--   FROM fn_prv_cxp_resumen(2);
--   -- esperado: diferencia = 0
--
-- 5) Cuadre con el estado de cuenta por proveedor (misma funcion base): para un
--    proveedor cualquiera, el saldo de las dos vistas debe coincidir.
-- WITH uno AS (SELECT cod_proveedor FROM fn_prv_cxp_documentos(2) LIMIT 1)
-- SELECT (SELECT SUM(saldo) FROM fn_prv_cxp_documentos(2, NULL, 0::SMALLINT, NULL,
--             (SELECT cod_proveedor FROM uno))) AS unificada,
--        (SELECT SUM(saldo) FROM fn_prv_estado_cuenta_documentos(2,
--             (SELECT cod_proveedor FROM uno), NULL, TRUE)) AS estado_cuenta;
--   -- esperado: iguales
--
-- 6) Los ~228 compromisos migrados de SIMAFI (procesados sin abonos) NO aparecen:
-- SELECT COUNT(*) FROM fn_prv_cxp_documentos(2, NULL, 2::SMALLINT) WHERE procesado;
--   -- esperado: solo los procesados que SI tienen abonos con saldo vivo (normalmente 0)
-- =============================================================================
