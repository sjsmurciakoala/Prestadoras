-- ============================================================
-- Matriz de Evidencia para Demo 2026-03-16
-- Contabilidad Automatica en Captacion + Bancos
-- ============================================================
--
-- Ejecutar DESPUES de las pruebas funcionales E2E.
-- Resumen:
--   A) Matriz por flujo (compositor, posteador, fuente config, multiempresa, reversa)
--   B) Evidencia de polizas generadas por captacion
--   C) Cuadre de polizas (debe = haber)
--   D) No doble posteo
--   E) Reversa exacta
--   F) Estado de saldos
--   G) Periodo contable
-- ============================================================

-- ============================================================
-- A) MATRIZ POR FLUJO
-- ============================================================
-- (Consulta informativa - la tabla se presenta como resultado)

SELECT
    flujo,
    compositor,
    posteador,
    fuente_config,
    multiempresa,
    reversa,
    estado
FROM (VALUES
    (
        'Captacion Lectora',
        'sp_con_generar_comprobante',
        'sp_con_postear_poliza (via sp_con_generar_comprobante)',
        'con_plantilla_partida_hdr/dtl',
        'SI (company_id del tenant)',
        'sp_con_revertir_poliza',
        'ACTIVO'
    ),
    (
        'Captacion Manual',
        'sp_con_generar_comprobante',
        'sp_con_postear_poliza (via sp_con_generar_comprobante)',
        'con_plantilla_partida_hdr/dtl',
        'SI (company_id del tenant)',
        'sp_con_revertir_poliza',
        'ACTIVO'
    ),
    (
        'Captacion Miscelaneos',
        'sp_con_generar_comprobante',
        'sp_con_postear_poliza (via sp_con_generar_comprobante)',
        'con_plantilla_partida_hdr/dtl',
        'SI (company_id del tenant)',
        'sp_con_revertir_poliza',
        'ACTIVO'
    ),
    (
        'Captacion Bancaria (Lectora/Manual/Misc)',
        'sp_registrar_partida_contable (via BanTransaccionesService)',
        'sp_con_postear_poliza',
        'Configuracion bancaria + servicios.cont_account_id',
        'SI (company_id del tenant)',
        'AnularMovimientoAsync (reversa bancaria)',
        'ACTIVO'
    ),
    (
        'Bancos (servicio directo)',
        'sp_registrar_partida_contable',
        'sp_con_postear_poliza',
        'Configuracion bancaria + contracuentas',
        'SI (company_id del tenant)',
        'sp_ban_kardex_anular_movimiento_recalcular',
        'ACTIVO'
    ),
    (
        'Lectura Movil / WebService',
        'sp_lectura_v2',
        'sp_con_postear_poliza',
        'con_regla_integracion + servicios.cont_account_id',
        'SI (company_id del tenant)',
        'N/A (sin reversa en lectura movil)',
        'EXCEPCION TEMPORAL'
    )
) AS t(flujo, compositor, posteador, fuente_config, multiempresa, reversa, estado);

-- ============================================================
-- B) EVIDENCIA DE POLIZAS GENERADAS POR CAPTACION
-- ============================================================
-- Polizas del modulo VENTAS (captacion) recientes.

SELECT
    h.poliza_id,
    h.company_id,
    h.module,
    h.document_type,
    h.document_id,
    h.document_number,
    h.status,
    CASE h.status WHEN 1 THEN 'POSTED' WHEN 0 THEN 'DRAFT' ELSE 'UNKNOWN' END AS estado_poliza,
    h.total_debit,
    h.total_credit,
    ABS(h.total_debit - h.total_credit) AS diferencia,
    h.poliza_date,
    h.posted_at,
    h.posted_by,
    h.created_at
FROM public.con_partida_hdr h
WHERE h.module = 'VENTAS'
ORDER BY h.poliza_id DESC
LIMIT 50;

-- ============================================================
-- C) CUADRE DE POLIZAS: DEBE = HABER
-- ============================================================
-- Debe retornar 0 filas si todo esta cuadrado.

SELECT
    h.poliza_id,
    h.document_number,
    h.total_debit,
    h.total_credit,
    ABS(h.total_debit - h.total_credit) AS diferencia
FROM public.con_partida_hdr h
WHERE h.module = 'VENTAS'
  AND h.status = 1
  AND ABS(h.total_debit - h.total_credit) > 0.01;

-- ============================================================
-- D) NO DOBLE POSTEO
-- ============================================================
-- Verifica que no existen document_id duplicados con status=1
-- para el mismo modulo/company. Debe retornar 0 filas.

SELECT
    h.company_id,
    h.module,
    h.document_type,
    h.document_id,
    COUNT(*) AS polizas_posteadas
FROM public.con_partida_hdr h
WHERE h.module = 'VENTAS'
  AND h.status = 1
GROUP BY h.company_id, h.module, h.document_type, h.document_id
HAVING COUNT(*) > 1;

-- ============================================================
-- E) REVERSA EXACTA
-- ============================================================
-- Polizas que fueron revertidas (status=0 despues de haber sido
-- posteadas). Deben tener posted_at pero status=0.

SELECT
    h.poliza_id,
    h.document_number,
    h.status,
    h.total_debit,
    h.total_credit,
    h.posted_at,
    h.posted_by,
    h.created_at
FROM public.con_partida_hdr h
WHERE h.module = 'VENTAS'
  AND h.status = 0
  AND h.posted_at IS NOT NULL
ORDER BY h.poliza_id DESC
LIMIT 20;

-- Verificar que las cuentas de polizas revertidas no tienen
-- saldo residual en con_saldo_cuenta.
-- (Comparar detalle de polizas DRAFT que fueron revertidas)

SELECT
    d.poliza_id,
    d.account_id,
    a.code AS cuenta_codigo,
    d.debit_amount AS debit,
    d.credit_amount AS credit,
    COALESCE(s.debitos, 0) AS saldo_debit,
    COALESCE(s.creditos, 0) AS saldo_credit
FROM public.con_partida_dtl d
JOIN public.con_partida_hdr h ON h.poliza_id = d.poliza_id AND h.company_id = d.company_id
JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
LEFT JOIN public.con_saldo_cuenta s ON s.company_id = d.company_id AND s.codigo_cuenta = a.code AND s.periodo_id = h.period_id
WHERE h.module = 'VENTAS'
  AND h.status = 0
  AND h.posted_at IS NOT NULL
ORDER BY d.poliza_id, d.line_number
LIMIT 30;

-- ============================================================
-- F) ESTADO DE SALDOS (con_saldo_cuenta)
-- ============================================================
-- Saldos actuales de las cuentas usadas en captacion.

SELECT
    s.company_id,
    s.codigo_cuenta,
    a.name AS cuenta_nombre,
    s.periodo_id,
    p.code AS periodo_codigo,
    p.name AS periodo_nombre,
    s.debitos AS total_debitos,
    s.creditos AS total_creditos,
    (s.debitos - s.creditos) AS saldo_neto
FROM public.con_saldo_cuenta s
JOIN public.con_plan_cuentas a ON a.code = s.codigo_cuenta AND a.company_id = s.company_id
JOIN public.con_periodo_contable p ON p.period_id = s.periodo_id
WHERE s.codigo_cuenta IN (
    SELECT DISTINCT a2.code
    FROM public.con_partida_dtl d
    JOIN public.con_partida_hdr h ON h.poliza_id = d.poliza_id AND h.company_id = d.company_id
    JOIN public.con_plan_cuentas a2 ON a2.account_id = d.account_id
    WHERE h.module = 'VENTAS'
)
ORDER BY s.company_id, s.codigo_cuenta, p.start_date;

-- ============================================================
-- G) PERIODO CONTABLE ABIERTO
-- ============================================================

SELECT
    p.period_id,
    p.company_id,
    p.code AS periodo_codigo,
    p.name AS periodo_nombre,
    p.start_date,
    p.end_date,
    p.status,
    p.status_id,
    CASE COALESCE(p.status_id, 2) WHEN 0 THEN 'ABIERTO' WHEN 1 THEN 'PRECIERRE' WHEN 2 THEN 'CERRADO' ELSE 'DESCONOCIDO' END AS estado_periodo
FROM public.con_periodo_contable p
WHERE COALESCE(p.status_id, 2) = 0
ORDER BY p.start_date DESC
LIMIT 5;

-- ============================================================
-- H) RESUMEN FINAL
-- ============================================================

SELECT 'Polizas VENTAS total' AS metrica,
       COUNT(*)::text AS valor
FROM public.con_partida_hdr WHERE module = 'VENTAS'

UNION ALL
SELECT 'Polizas VENTAS POSTED',
       COUNT(*)::text
FROM public.con_partida_hdr WHERE module = 'VENTAS' AND status = 1

UNION ALL
SELECT 'Polizas VENTAS DRAFT (revertidas)',
       COUNT(*)::text
FROM public.con_partida_hdr WHERE module = 'VENTAS' AND status = 0

UNION ALL
SELECT 'Polizas descuadradas (debe <> haber)',
       COUNT(*)::text
FROM public.con_partida_hdr WHERE module = 'VENTAS' AND status = 1 AND ABS(total_debit - total_credit) > 0.01

UNION ALL
SELECT 'Document IDs duplicados con posteo',
       COUNT(*)::text
FROM (
    SELECT document_id
    FROM public.con_partida_hdr
    WHERE module = 'VENTAS' AND status = 1
    GROUP BY company_id, module, document_type, document_id
    HAVING COUNT(*) > 1
) x

UNION ALL
SELECT 'Periodos Abiertos',
       COUNT(*)::text
FROM public.con_periodo_contable
WHERE COALESCE(status_id, 2) = 0;
