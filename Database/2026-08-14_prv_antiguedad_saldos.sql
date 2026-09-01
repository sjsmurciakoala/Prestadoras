-- =============================================================================
-- Proveedores: antiguedad de saldos (aging de cuentas por pagar) — F0
-- Fecha: 2026-08-14
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
-- Plan: docs/plans/2026-08-14-antiguedad-saldos-proveedor-plan.md
-- Prototipo: docs/prototipos/2026-08-14-antiguedad-saldos-proveedor.html
--
-- QUE ES
--   La funcion de LECTURA que consolida el saldo por pagar de TODOS los proveedores,
--   repartido por antiguedad de vencimiento a una fecha de corte, en 6 tramos:
--     por vencer  ·  1-30  ·  31-60  ·  61-90  ·  91-120  ·  mas de 120 dias
--   Es el reporte matriz (proveedor x tramo) del que sale la pantalla y el PDF.
--
-- POR QUE NO CREA NADA
--   El calculo por proveedor YA existe: fn_prv_estado_cuenta_resumen (2026-08-13) da
--   los tramos, pero corta en ">90" (5 tramos) y corre para UN solo proveedor. Aqui
--   solo hace falta (a) correrlo sobre todos y (b) abrir el ultimo tramo en 91-120 y
--   >120. No se necesitan tablas: los datos vivos ya estan en alm_compra_cxp y
--   prv_compromiso_hdr. Por eso F0 es UNA funcion, no un modelo de datos.
--
-- COMO NO DUPLICA REGLAS
--   Todas las reglas de vigencia (CxP anulada estado_id=9 fuera; compromiso anulado
--   fuera; ★ compat legacy del compromiso procesado sin abonos = saldado; abonos solo
--   estado 'V'; abono de compromiso al BRUTO) viven en la funcion BASE
--   fn_prv_estado_cuenta_documentos. Esta funcion la reutiliza con CROSS JOIN LATERAL
--   (una llamada por proveedor) y solo agrega por tramo. Si una regla cambia, cambia
--   en un solo lugar y este aging la hereda.
--
-- CUADRE (para un mismo proveedor y corte)
--   Con p_incluir_por_vencer = TRUE:
--     por_vencer                    == fn_prv_estado_cuenta_resumen.saldo_por_vencer
--     tramo_1_30/31_60/61_90        == antiguedad_30 / _60 / _90
--     tramo_91_120 + tramo_mas_120  == antiguedad_mas90     (aqui se abre en dos)
--     vencido                       == saldo_vencido
--     saldo_total                   == saldo_total
--
-- LO QUE NO CUBRE (igual que el estado de cuenta del proveedor)
--   El aging es de DOCUMENTOS VIVOS (facturas de compra + compromisos), no del mayor.
--   La cartera historica de SIMAFI (~L 101M al HABER en prv_proveedores.cuenta_contable)
--   no tiene documentos operativos: este saldo NO cuadra con la contabilidad, y no debe
--   presentarse como si lo hiciera.
--
-- ADITIVO Y REVERSIBLE: solo una funcion de lectura. No crea ni altera ninguna tabla,
-- columna, indice ni dato. Re-ejecutable. Depende de fn_prv_estado_cuenta_documentos
-- (2026-08-13), alm_compra_cxp (2026-08-12), prv_compromiso_hdr con company_id
-- (2026-07-10), prv_proveedores / prv_tipoproveedor.
-- =============================================================================
BEGIN;

-- DROP explicito: CREATE OR REPLACE no puede cambiar el tipo de retorno de una funcion
-- existente. Si en el futuro se agrega/quita una columna de salida, el DROP deja que el
-- script siga siendo re-ejecutable sin errores.
DROP FUNCTION IF EXISTS public.fn_prv_antiguedad_saldos(bigint, date, boolean, integer, integer);

CREATE OR REPLACE FUNCTION public.fn_prv_antiguedad_saldos(
    p_company_id         BIGINT,
    p_corte              DATE     DEFAULT NULL,   -- NULL = hoy (CURRENT_DATE en la base)
    p_incluir_por_vencer BOOLEAN  DEFAULT TRUE,   -- FALSE = solo lo vencido
    p_origen             INTEGER  DEFAULT 0,      -- 0 ambos, 1 solo compras, 2 solo compromisos
                                                  -- (INTEGER, no SMALLINT: los literales enteros no se
                                                  --  reducen a smallint al resolver la sobrecarga)
    p_cod_tipoproveedor  INTEGER  DEFAULT NULL    -- NULL = todos los tipos (cod_tipoproveedor es numerico)
)
RETURNS TABLE (
    cod_proveedor         TEXT,
    proveedor_nombre      TEXT,
    rtn                   TEXT,
    cod_tipoproveedor     INTEGER,
    tipo_nombre           TEXT,
    cuenta_contable       TEXT,
    por_vencer            NUMERIC,
    tramo_1_30            NUMERIC,
    tramo_31_60           NUMERIC,
    tramo_61_90           NUMERIC,
    tramo_91_120          NUMERIC,
    tramo_mas_120         NUMERIC,
    vencido               NUMERIC,
    saldo_total           NUMERIC,
    documentos_pendientes INTEGER
)
LANGUAGE sql
STABLE
AS $$
    -- 1) Universo: proveedores con al menos un documento NO anulado en el/los modulo(s)
    --    pedidos. Se toma el cod_proveedor CRUDO (sin TRIM) para que el match con la
    --    funcion base (que compara por igualdad exacta) no pierda filas.
    WITH universo AS (
        SELECT DISTINCT u.cod_proveedor
        FROM (
            SELECT c.cod_proveedor
            FROM public.alm_compra_cxp c
            WHERE c.company_id = p_company_id
              AND c.estado_id <> 9
              AND (p_origen = 0 OR p_origen = 1)

            UNION

            SELECT h.cod_proveedor
            FROM public.prv_compromiso_hdr h
            WHERE h.company_id = p_company_id
              AND h.anulado    = FALSE
              AND (p_origen = 0 OR p_origen = 2)
        ) u
        WHERE NULLIF(TRIM(u.cod_proveedor), '') IS NOT NULL
    ),
    -- 2) Por proveedor: se reutiliza la funcion BASE (solo_pendientes = TRUE) y se
    --    reparte el saldo por tramo desde dias_vencido. p_origen filtra las ramas.
    agregado AS (
        SELECT u.cod_proveedor,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido <= 0), 0)                 AS por_vencer,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido BETWEEN 1   AND 30 ), 0)  AS tramo_1_30,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido BETWEEN 31  AND 60 ), 0)  AS tramo_31_60,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido BETWEEN 61  AND 90 ), 0)  AS tramo_61_90,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido BETWEEN 91  AND 120), 0)  AS tramo_91_120,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido > 120), 0)                AS tramo_mas_120,
               COALESCE(SUM(d.saldo) FILTER (WHERE d.dias_vencido > 0), 0)                  AS vencido,
               COUNT(*) FILTER (WHERE d.dias_vencido > 0)  AS docs_vencidos,
               COUNT(*) FILTER (WHERE d.dias_vencido <= 0) AS docs_por_vencer
        FROM universo u
        CROSS JOIN LATERAL
            public.fn_prv_estado_cuenta_documentos(p_company_id, u.cod_proveedor, p_corte, TRUE) d
        WHERE (p_origen = 0 OR d.origen = p_origen)
        GROUP BY u.cod_proveedor
    )
    SELECT a.cod_proveedor::TEXT,
           COALESCE(NULLIF(TRIM(p.nombre), ''), a.cod_proveedor)::TEXT AS proveedor_nombre,
           NULLIF(TRIM(p.rtn), '')::TEXT                               AS rtn,
           p.cod_tipoproveedor::INTEGER                                AS cod_tipoproveedor,
           NULLIF(TRIM(t.nombre), '')::TEXT                            AS tipo_nombre,
           NULLIF(TRIM(p.cuenta_contable), '')::TEXT                   AS cuenta_contable,
           CASE WHEN p_incluir_por_vencer THEN a.por_vencer ELSE 0 END AS por_vencer,
           a.tramo_1_30,
           a.tramo_31_60,
           a.tramo_61_90,
           a.tramo_91_120,
           a.tramo_mas_120,
           a.vencido,
           a.vencido + CASE WHEN p_incluir_por_vencer THEN a.por_vencer ELSE 0 END         AS saldo_total,
           (a.docs_vencidos + CASE WHEN p_incluir_por_vencer THEN a.docs_por_vencer ELSE 0 END)::INTEGER
                                                                                           AS documentos_pendientes
    FROM agregado a
    -- prv_proveedores es keyless y su company_id es int4: cast explicito. Ligado por
    -- (company_id, cod_proveedor). LEFT JOIN: un documento cuyo proveedor no este en el
    -- maestro igual aparece, con el codigo como nombre.
    LEFT JOIN public.prv_proveedores p
           ON p.company_id       = p_company_id::INTEGER
          AND TRIM(p.cod_proveedor) = TRIM(a.cod_proveedor)
    LEFT JOIN public.prv_tipoproveedor t
           ON t.cod_tipoproveedor = p.cod_tipoproveedor
    WHERE (a.vencido + CASE WHEN p_incluir_por_vencer THEN a.por_vencer ELSE 0 END) > 0
      AND (p_cod_tipoproveedor IS NULL OR p.cod_tipoproveedor = p_cod_tipoproveedor)
    ORDER BY saldo_total DESC, proveedor_nombre;
$$;

COMMENT ON FUNCTION public.fn_prv_antiguedad_saldos(bigint, date, boolean, integer, integer) IS
    'Antiguedad de saldos por pagar de todos los proveedores a una fecha de corte, en 6 tramos (por vencer/1-30/31-60/61-90/91-120/+120). Reutiliza fn_prv_estado_cuenta_documentos via LATERAL: no duplica reglas de vigencia. p_origen 0 ambos/1 compras/2 compromisos; p_incluir_por_vencer FALSE = solo vencido.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar; company 2 = MERENDON)
-- =============================================================================
-- 1) La funcion existe con la firma esperada:
-- SELECT proname, pg_get_function_identity_arguments(oid)
--   FROM pg_proc WHERE proname = 'fn_prv_antiguedad_saldos';
--
-- 2) Matriz al dia de hoy (todos los proveedores con saldo):
-- SELECT * FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 0, NULL);
--
-- 3) Totales por tramo (el pie de la matriz):
-- SELECT SUM(por_vencer) por_vencer, SUM(tramo_1_30) t30, SUM(tramo_31_60) t60,
--        SUM(tramo_61_90) t90, SUM(tramo_91_120) t120, SUM(tramo_mas_120) tmas120,
--        SUM(vencido) vencido, SUM(saldo_total) total, COUNT(*) proveedores
--   FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 0, NULL);
--
-- 4) ★ CUADRE contra el estado de cuenta (debe dar TRUE para cualquier proveedor):
--    la suma de tramos del aging == saldo_total del resumen, y el tramo abierto
--    (91-120 + >120) == antiguedad_mas90 del resumen.
-- WITH ag AS (SELECT * FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 0, NULL) WHERE cod_proveedor = '0088'),
--      rs AS (SELECT * FROM fn_prv_estado_cuenta_resumen(2, '0088', NULL))
-- SELECT ag.saldo_total = rs.saldo_total                                   AS cuadra_total,
--        ag.por_vencer  = rs.saldo_por_vencer                              AS cuadra_por_vencer,
--        ag.vencido     = rs.saldo_vencido                                 AS cuadra_vencido,
--        (ag.tramo_91_120 + ag.tramo_mas_120) = rs.antiguedad_mas90        AS cuadra_tramo_abierto
--   FROM ag, rs;
--
-- 5) El total del aging (suma de todos los proveedores) debe igualar la suma de los
--    saldos pendientes de la funcion base sobre el universo (sin doble conteo):
-- SELECT (SELECT SUM(saldo_total) FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 0, NULL)) AS aging,
--        (SELECT SUM(d.saldo)
--           FROM (SELECT DISTINCT cod_proveedor FROM alm_compra_cxp WHERE company_id=2 AND estado_id<>9
--                 UNION SELECT DISTINCT cod_proveedor FROM prv_compromiso_hdr WHERE company_id=2 AND anulado=FALSE) u
--           CROSS JOIN LATERAL fn_prv_estado_cuenta_documentos(2, u.cod_proveedor, NULL, TRUE) d) AS base;
--
-- 6) Filtros: solo vencido / solo compras / por tipo.
-- SELECT * FROM fn_prv_antiguedad_saldos(2, NULL, FALSE, 0, NULL);   -- por_vencer = 0 en todas
-- SELECT * FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 1, NULL);    -- solo facturas de compra
-- =============================================================================
