-- =============================================================================
-- Proveedores: estado de cuenta (resumen / documentos / movimientos)
-- Fecha: 2026-08-13
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- QUE ES
--   Las tres funciones de LECTURA que alimentan la pantalla de estado de cuenta del
--   proveedor (F0 del plan docs/plans/2026-08-13-proveedor-estado-cuenta-plan.md).
--   No existia NADA equivalente para proveedores: todo lo que hay de "estado de
--   cuenta"/"saldo"/"antiguedad" en esta base es de CLIENTES.
--
-- DE DONDE SALE LA DEUDA
--   La deuda viva del proveedor vive en DOS modulos separados que no se hablan:
--     (1) alm_compra_cxp  + alm_compra_cxp_abono   -> facturas de compra (CxP)
--     (2) prv_compromiso_hdr + prv_compromiso_abono -> compromisos / ordenes de pago directo
--   Ambos se ligan al maestro por cod_proveedor VARCHAR, SIN FK (prv_proveedores es keyless).
--   saldo(proveedor) = SUM(cargos vigentes) - SUM(abonos vigentes)
--
-- REGLAS DE VIGENCIA (viven aqui, en un solo lugar)
--   · CxP:         se excluye estado_id = 9 (anulada). Las prepagadas no generan CxP.
--   · Compromiso:  se excluye anulado = TRUE.
--   · ★ COMPAT LEGACY: un compromiso con status_transacc = TRUE y CERO filas de abono
--     esta SALDADO (regla de OrdenesPagoDirectoService.cs:289). Son los ~228 migrados de
--     SIMAFI, L 6.8M. Si no se excluyeran, el estado de cuenta inventaria esa deuda.
--   · Abonos: solo estado = 'V'. El abono de compromiso es BRUTO (con retencion, al banco
--     sale el neto pero el saldo baja por el bruto) — aqui se usa el bruto, que es el que
--     cancela la deuda.
--
-- LO QUE NO CUBRE
--   La cartera historica de SIMAFI (~L 101M al HABER en prv_proveedores.cuenta_contable)
--   no tiene documentos operativos en el portal, asi que este saldo NO cuadra con el mayor.
--   Las columnas prv_proveedores.saldo_actual / saldo_anterior / compras_acum estan MUERTAS
--   (ProveedoresService las inserta en NULL y nadie las actualiza): no se leen aqui.
--   prv_kardex y ops_compromiso estan vacias y sin uso: quedan fuera.
--
-- ADITIVO Y REVERSIBLE: solo CREATE OR REPLACE FUNCTION de lectura. No crea ni altera
-- ninguna tabla, columna, indice ni dato. Re-ejecutable las veces que haga falta.
-- Depende de: alm_compra_cxp (+abono) [2026-08-12], prv_compromiso_abono [2026-07-17],
--             prv_compromiso_hdr con company_id [2026-07-10].
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1. Documentos del proveedor (funcion BASE: aqui viven las reglas de vigencia)
--
--    p_corte NULL  = sin corte de fecha (todos los documentos y todos los abonos
--                    vigentes). Los dias de vencimiento se cuentan contra CURRENT_DATE.
--    p_corte fecha = fotografia a esa fecha: solo documentos y abonos con fecha <= corte.
--    p_solo_pendientes TRUE = unicamente los que conservan saldo > 0.
--
--    origen: 1 = factura de compra, 2 = compromiso (codigos numericos; la etiqueta la
--    pone el C#, nunca se muestra el codigo).
--    estado_id sigue la escala de EstadoCompraCxp: 1 Pendiente, 2 Parcial, 3 Pagada.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_documentos(
    p_company_id      BIGINT,
    p_cod_proveedor   VARCHAR,
    p_corte           DATE    DEFAULT NULL,
    p_solo_pendientes BOOLEAN DEFAULT TRUE
)
RETURNS TABLE (
    origen            SMALLINT,
    documento_id      BIGINT,
    numero_documento  TEXT,
    fecha             DATE,
    fecha_vencimiento DATE,
    concepto          TEXT,
    monto             NUMERIC,
    abonado           NUMERIC,
    saldo             NUMERIC,
    dias_vencido      INTEGER,
    estado_id         SMALLINT
)
LANGUAGE sql
STABLE
AS $$
    WITH cargos AS (
        -- (1) Facturas de compra: la CxP es 1:1 con la factura.
        SELECT 1::SMALLINT                                                     AS origen,
               c.id::BIGINT                                                    AS documento_id,
               COALESCE(NULLIF(TRIM(c.numero_factura_sar), ''),
                        'FAC-' || c.compra_hdr_id::TEXT)                       AS numero_documento,
               c.fecha                                                         AS fecha,
               c.fecha_vencimiento                                             AS fecha_vencimiento,
               COALESCE(NULLIF(TRIM(hd.observaciones), ''), 'Factura de compra') AS concepto,
               c.monto                                                         AS monto
        FROM public.alm_compra_cxp c
        LEFT JOIN public.alm_compra_hdr hd
               ON hd.company_id = c.company_id
              AND hd.id         = c.compra_hdr_id
        WHERE c.company_id    = p_company_id
          AND c.cod_proveedor = p_cod_proveedor
          AND c.estado_id    <> 9                                  -- anulada fuera
          AND (p_corte IS NULL OR c.fecha <= p_corte)

        UNION ALL

        -- (2) Compromisos / ordenes de pago directo.
        -- OJO con el relleno del correlativo: LPAD(x, 5, '0') a secas TRUNCA cuando el número
        -- tiene más de 5 dígitos, y los numero_orden reales llegan a 6 → 333423 salía '33342' y
        -- colisionaba con 333426 (dos documentos distintos con el mismo número en pantalla).
        -- TO_CHAR(...,'FM00000') tampoco sirve: desborda a '#####'. El GREATEST rellena a 5 los
        -- cortos y respeta enteros los largos.
        SELECT 2::SMALLINT,
               h.numero_orden::BIGINT,
               'OPD-' || LPAD(h.numero_orden::TEXT, GREATEST(5, LENGTH(h.numero_orden::TEXT)), '0'),
               h.fecha::DATE,
               h.fecha::DATE,          -- no tiene vencimiento propio (D2 del plan)
               COALESCE(NULLIF(TRIM(h.concepto), ''), 'Compromiso de pago'),
               h.monto
        FROM public.prv_compromiso_hdr h
        WHERE h.company_id    = p_company_id
          AND h.cod_proveedor = p_cod_proveedor
          AND h.anulado       = FALSE                              -- anulado fuera
          AND (p_corte IS NULL OR h.fecha::DATE <= p_corte)
          -- ★ compat legacy: procesado sin abonos = saldado, no arrastra deuda.
          AND NOT (COALESCE(h.status_transacc, FALSE) = TRUE
                   AND NOT EXISTS (SELECT 1
                                     FROM public.prv_compromiso_abono a
                                    WHERE a.company_id   = h.company_id
                                      AND a.numero_orden = h.numero_orden))
    ),
    abonos AS (
        -- Abonos vigentes de facturas de compra.
        SELECT 1::SMALLINT AS origen,
               ab.cxp_id::BIGINT AS documento_id,
               SUM(ab.monto)     AS abonado
        FROM public.alm_compra_cxp_abono ab
        WHERE ab.company_id = p_company_id
          AND ab.estado     = 'V'
          AND (p_corte IS NULL OR ab.fecha <= p_corte)
        GROUP BY ab.cxp_id

        UNION ALL

        -- Abonos vigentes de compromisos (monto BRUTO).
        SELECT 2::SMALLINT,
               ab.numero_orden::BIGINT,
               SUM(ab.monto)
        FROM public.prv_compromiso_abono ab
        WHERE ab.company_id = p_company_id
          AND ab.estado     = 'V'
          AND (p_corte IS NULL OR ab.fecha::DATE <= p_corte)
        GROUP BY ab.numero_orden
    )
    SELECT c.origen,
           c.documento_id,
           c.numero_documento,
           c.fecha,
           c.fecha_vencimiento,
           c.concepto,
           c.monto,
           COALESCE(a.abonado, 0)                        AS abonado,
           c.monto - COALESCE(a.abonado, 0)              AS saldo,
           (COALESCE(p_corte, CURRENT_DATE) - c.fecha_vencimiento)::INTEGER AS dias_vencido,
           CASE
               WHEN c.monto - COALESCE(a.abonado, 0) <= 0 THEN 3::SMALLINT   -- Pagada
               WHEN COALESCE(a.abonado, 0) > 0            THEN 2::SMALLINT   -- Parcial
               ELSE 1::SMALLINT                                              -- Pendiente
           END                                           AS estado_id
    FROM cargos c
    LEFT JOIN abonos a
           ON a.origen       = c.origen
          AND a.documento_id = c.documento_id
    WHERE p_solo_pendientes IS NOT TRUE
       OR c.monto - COALESCE(a.abonado, 0) > 0
    ORDER BY c.fecha_vencimiento, c.fecha, c.origen, c.documento_id;
$$;

COMMENT ON FUNCTION public.fn_prv_estado_cuenta_documentos(BIGINT, VARCHAR, DATE, BOOLEAN) IS
    'Documentos por pagar de un proveedor (facturas de compra + compromisos), con su abonado y saldo. Funcion BASE: concentra las reglas de vigencia, incluida la compat legacy del compromiso procesado sin abonos. origen 1=compra 2=compromiso; estado_id 1 Pendiente/2 Parcial/3 Pagada.';

-- -----------------------------------------------------------------------------
-- 2. Resumen: saldo, vencido, por vencer, antiguedad y ultimo pago.
--    Se apoya en la funcion base para no duplicar las reglas de vigencia.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_resumen(
    p_company_id    BIGINT,
    p_cod_proveedor VARCHAR,
    p_corte         DATE DEFAULT NULL
)
RETURNS TABLE (
    saldo_total           NUMERIC,
    saldo_vencido         NUMERIC,
    saldo_por_vencer      NUMERIC,
    saldo_vence_7dias     NUMERIC,
    documentos_pendientes INTEGER,
    documento_mas_antiguo DATE,
    ultimo_pago_monto     NUMERIC,
    ultimo_pago_fecha     DATE,
    antiguedad_corriente  NUMERIC,
    antiguedad_30         NUMERIC,
    antiguedad_60         NUMERIC,
    antiguedad_90         NUMERIC,
    antiguedad_mas90      NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH corte AS (
        SELECT COALESCE(p_corte, CURRENT_DATE) AS dia
    ),
    pend AS (
        SELECT d.*
        FROM public.fn_prv_estado_cuenta_documentos(p_company_id, p_cod_proveedor, p_corte, TRUE) d
    ),
    pago AS (
        -- Ultimo pago vigente al proveedor, venga de la rama que venga.
        SELECT p.fecha, p.monto
        FROM (
            SELECT ab.fecha AS fecha, ab.monto AS monto
            FROM public.alm_compra_cxp_abono ab
            JOIN public.alm_compra_cxp c
              ON c.company_id = ab.company_id
             AND c.id         = ab.cxp_id
            WHERE ab.company_id    = p_company_id
              AND ab.estado        = 'V'
              AND c.cod_proveedor  = p_cod_proveedor
              AND c.estado_id     <> 9
              AND (p_corte IS NULL OR ab.fecha <= p_corte)

            UNION ALL

            SELECT ab.fecha::DATE, ab.monto
            FROM public.prv_compromiso_abono ab
            JOIN public.prv_compromiso_hdr h
              ON h.company_id   = ab.company_id
             AND h.numero_orden = ab.numero_orden
            WHERE ab.company_id   = p_company_id
              AND ab.estado       = 'V'
              AND h.cod_proveedor = p_cod_proveedor
              AND h.anulado       = FALSE
              AND (p_corte IS NULL OR ab.fecha::DATE <= p_corte)
        ) p
        ORDER BY p.fecha DESC
        LIMIT 1
    )
    SELECT COALESCE(SUM(pend.saldo), 0)                                                      AS saldo_total,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido > 0), 0)                 AS saldo_vencido,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido <= 0), 0)                AS saldo_por_vencer,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido <= 0
                                              AND pend.dias_vencido >= -7), 0)               AS saldo_vence_7dias,
           COUNT(*)::INTEGER                                                                 AS documentos_pendientes,
           MIN(pend.fecha)                                                                   AS documento_mas_antiguo,
           (SELECT monto FROM pago)                                                          AS ultimo_pago_monto,
           (SELECT fecha FROM pago)                                                          AS ultimo_pago_fecha,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido <= 0), 0)                AS antiguedad_corriente,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido BETWEEN 1 AND 30), 0)    AS antiguedad_30,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido BETWEEN 31 AND 60), 0)   AS antiguedad_60,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido BETWEEN 61 AND 90), 0)   AS antiguedad_90,
           COALESCE(SUM(pend.saldo) FILTER (WHERE pend.dias_vencido > 90), 0)                AS antiguedad_mas90
    FROM pend;
$$;

COMMENT ON FUNCTION public.fn_prv_estado_cuenta_resumen(BIGINT, VARCHAR, DATE) IS
    'Resumen del estado de cuenta de un proveedor: saldo total, vencido, por vencer, tramos de antiguedad (corriente/1-30/31-60/61-90/+90) y ultimo pago. Se apoya en fn_prv_estado_cuenta_documentos.';

-- -----------------------------------------------------------------------------
-- 3. Movimientos: libro de cargos y abonos con saldo corrido.
--
--    El saldo corrido se calcula sobre TODA la historia y el rango de fechas se
--    aplica DESPUES: el saldo de una fila es el acumulado real del proveedor, no
--    el del rango filtrado (mismo criterio que el estado de cuenta de clientes).
--    tipo: 1 = cargo, 2 = abono.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_prv_estado_cuenta_movimientos(
    p_company_id    BIGINT,
    p_cod_proveedor VARCHAR,
    p_desde         DATE DEFAULT NULL,
    p_hasta         DATE DEFAULT NULL
)
RETURNS TABLE (
    fecha            DATE,
    origen           SMALLINT,
    tipo             SMALLINT,
    numero_documento TEXT,
    referencia       TEXT,
    cargo            NUMERIC,
    abono            NUMERIC,
    saldo_corrido    NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH movs AS (
        -- Cargos: se reusa la funcion base (sin corte, todos los documentos vigentes)
        -- para que las reglas de vigencia no se dupliquen ni se desincronicen.
        SELECT d.fecha,
               d.origen,
               1::SMALLINT      AS tipo,
               d.numero_documento,
               d.concepto       AS referencia,
               d.monto          AS cargo,
               0::NUMERIC       AS abono,
               d.documento_id,
               0                AS desempate
        FROM public.fn_prv_estado_cuenta_documentos(p_company_id, p_cod_proveedor, NULL, FALSE) d

        UNION ALL

        -- Abonos de facturas de compra.
        SELECT ab.fecha,
               1::SMALLINT,
               2::SMALLINT,
               COALESCE(NULLIF(TRIM(c.numero_factura_sar), ''), 'FAC-' || c.compra_hdr_id::TEXT)
                   || ' · abono ' || ab.numero_abono::TEXT,
               COALESCE(NULLIF(TRIM(ab.metodo_pago), ''), 'Pago')
                   || COALESCE(' · cheque ' || NULLIF(TRIM(ab.num_cheque), ''), ''),
               0::NUMERIC,
               ab.monto,
               c.id::BIGINT,
               ab.numero_abono
        FROM public.alm_compra_cxp_abono ab
        JOIN public.alm_compra_cxp c
          ON c.company_id = ab.company_id
         AND c.id         = ab.cxp_id
        WHERE ab.company_id   = p_company_id
          AND ab.estado       = 'V'
          AND c.cod_proveedor = p_cod_proveedor
          AND c.estado_id    <> 9

        UNION ALL

        -- Abonos de compromisos (monto BRUTO: es el que cancela la deuda).
        SELECT ab.fecha::DATE,
               2::SMALLINT,
               2::SMALLINT,
               'OPD-' || LPAD(ab.numero_orden::TEXT, GREATEST(5, LENGTH(ab.numero_orden::TEXT)), '0')
                   || ' · abono ' || ab.numero_abono::TEXT,
               COALESCE(NULLIF(TRIM(ab.metodo_pago), ''), 'Pago'),
               0::NUMERIC,
               ab.monto,
               ab.numero_orden::BIGINT,
               ab.numero_abono
        FROM public.prv_compromiso_abono ab
        JOIN public.prv_compromiso_hdr h
          ON h.company_id   = ab.company_id
         AND h.numero_orden = ab.numero_orden
        WHERE ab.company_id   = p_company_id
          AND ab.estado       = 'V'
          AND h.cod_proveedor = p_cod_proveedor
          AND h.anulado       = FALSE
    ),
    corrido AS (
        SELECT m.*,
               SUM(m.cargo - m.abono) OVER (
                   ORDER BY m.fecha, m.tipo, m.origen, m.documento_id, m.desempate
                   ROWS UNBOUNDED PRECEDING
               ) AS saldo_corrido
        FROM movs m
    )
    SELECT corrido.fecha,
           corrido.origen,
           corrido.tipo,
           corrido.numero_documento,
           corrido.referencia,
           corrido.cargo,
           corrido.abono,
           corrido.saldo_corrido
    FROM corrido
    WHERE (p_desde IS NULL OR corrido.fecha >= p_desde)
      AND (p_hasta IS NULL OR corrido.fecha <= p_hasta)
    ORDER BY corrido.fecha, corrido.tipo, corrido.origen, corrido.documento_id, corrido.desempate;
$$;

COMMENT ON FUNCTION public.fn_prv_estado_cuenta_movimientos(BIGINT, VARCHAR, DATE, DATE) IS
    'Libro de movimientos del proveedor (cargos y abonos) con saldo corrido historico. El rango de fechas filtra las filas visibles, NO el acumulado. tipo 1=cargo 2=abono; origen 1=compra 2=compromiso.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar; sustituir el codigo de proveedor)
-- =============================================================================
-- 1) Las tres funciones existen:
-- SELECT proname, pg_get_function_identity_arguments(oid)
--   FROM pg_proc WHERE proname LIKE 'fn_prv_estado_cuenta%' ORDER BY proname;
--
-- 2) Proveedor con mas movimiento (para tener con que probar):
-- SELECT cod_proveedor, COUNT(*), SUM(saldo) FROM alm_compra_cxp
--  WHERE company_id = 2 AND estado_id <> 9 GROUP BY 1 ORDER BY 3 DESC NULLS LAST LIMIT 5;
--
-- 3) Resumen y documentos:
-- SELECT * FROM fn_prv_estado_cuenta_resumen(2, '0088', NULL);
-- SELECT * FROM fn_prv_estado_cuenta_documentos(2, '0088', NULL, TRUE);
-- SELECT * FROM fn_prv_estado_cuenta_movimientos(2, '0088', NULL, NULL);
--
-- 4) CUADRE (debe dar TRUE): cargos - abonos del libro == saldo total del resumen.
--    OJO: NO comparar contra "la ultima fila" del libro con un ORDER BY parcial — el saldo
--    corrido se ordena por (fecha, tipo, origen, documento_id, desempate) y un ORDER BY
--    fecha/tipo solamente puede caer en una fila intermedia cuando hay empates de fecha.
-- SELECT (SELECT SUM(cargo) - SUM(abono) FROM fn_prv_estado_cuenta_movimientos(2, '0088', NULL, NULL))
--        = (SELECT saldo_total FROM fn_prv_estado_cuenta_resumen(2, '0088', NULL)) AS cuadra;
--
-- 4b) Contraste util: "todos" incluye los documentos ya pagados (netean a 0), "pendientes" no.
--     Los TRES netos deben ser identicos.
-- SELECT 'movimientos' AS fuente, SUM(cargo) AS cargos, SUM(abono) AS abonos, SUM(cargo)-SUM(abono) AS neto
--   FROM fn_prv_estado_cuenta_movimientos(2, '0088', NULL, NULL)
-- UNION ALL SELECT 'documentos(todos)', SUM(monto), SUM(abonado), SUM(saldo)
--   FROM fn_prv_estado_cuenta_documentos(2, '0088', NULL, FALSE)
-- UNION ALL SELECT 'documentos(pendientes)', SUM(monto), SUM(abonado), SUM(saldo)
--   FROM fn_prv_estado_cuenta_documentos(2, '0088', NULL, TRUE);
--
-- 5) ★ La deuda fantasma NO aparece: los compromisos legacy (status_transacc = TRUE,
--    cero abonos) deben quedar fuera. Este SELECT debe devolver 0 filas.
-- SELECT h.numero_orden, h.monto
--   FROM prv_compromiso_hdr h
--   JOIN fn_prv_estado_cuenta_documentos(2, h.cod_proveedor, NULL, FALSE) d
--     ON d.origen = 2 AND d.documento_id = h.numero_orden
--  WHERE h.company_id = 2 AND COALESCE(h.status_transacc, FALSE) = TRUE
--    AND NOT EXISTS (SELECT 1 FROM prv_compromiso_abono a
--                     WHERE a.company_id = h.company_id AND a.numero_orden = h.numero_orden);
-- =============================================================================
