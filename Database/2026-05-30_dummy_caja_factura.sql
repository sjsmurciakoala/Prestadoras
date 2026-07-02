-- =============================================================
-- DUMMY: relación sesion_caja → transaccion_abonado → factura
-- Ejecutar en un entorno de desarrollo.
-- Todo corre dentro de BEGIN/ROLLBACK: no deja datos permanentes.
-- Ajustar company_id a la empresa del tenant de prueba.
-- =============================================================

BEGIN;

-- ── 0. Contexto ─────────────────────────────────────────────
DO $$ BEGIN
    RAISE NOTICE '>>> Iniciando dummy caja-factura. company_id = 2';
END $$;

-- ── 1. Abrir sesión de caja ─────────────────────────────────
INSERT INTO sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
VALUES (2, 'cajero_test', NOW(), 'ABIERTA')
RETURNING id, usuario_apertura, estado;

-- ── 2. Crear factura dummy ──────────────────────────────────
-- numrecibo es GENERATED ALWAYS — lo genera la BD y lo capturamos.
WITH factura_ins AS (
    INSERT INTO factura (
        company_id,
        numfactura,
        clientecodigo,
        tipofactura,
        ano,
        mes,
        fechaemision,
        fechavence,
        saldototal,
        estado,
        tipofacturacion
    )
    VALUES (
        2,
        'F-DUMMY-001',
        'CLI-TEST-001',
        'AGUA',
        '2026',
        '05',
        CURRENT_DATE,
        CURRENT_DATE + 30,
        350.00,
        'P',
        'MENSUAL'
    )
    RETURNING id, numrecibo, numfactura, clientecodigo, saldototal, estado
)
SELECT * FROM factura_ins;

-- ── 3. Detalle de la factura ────────────────────────────────
INSERT INTO factura_detalle (
    company_id,
    numrecibo,
    factura_id,
    codigo,
    tiposervicio,
    descripcion,
    montovalor,
    montovalor_saldo
)
SELECT
    2,
    f.numrecibo,
    f.id,
    'AGU-001',
    'AGUA',
    'Consumo agua mayo 2026',
    350.00,
    350.00
FROM factura f
WHERE f.numfactura = 'F-DUMMY-001'
  AND f.company_id = 2
RETURNING id, numrecibo, descripcion, montovalor;

-- ── 4. Registrar pago en transaccion_abonado ────────────────
-- caja_id apunta a sesion_caja.id (referencia libre, sin FK).
-- recibo   apunta a factura.numrecibo.
INSERT INTO transaccion_abonado (
    company_id,
    cliente_clave,
    recibo,
    tipotransaccion,
    docufuente,
    docufuente2,
    fecha_docu,
    tipo_partida,
    banco,
    descripcion,
    creditos,
    debitos,
    saldo,
    estado,
    fecha_registro,
    usuario,
    caja_id
)
SELECT
    2,
    'CLI-TEST-001',
    f.numrecibo,            -- → factura.numrecibo
    'PAG',
    f.numrecibo::numeric,
    f.numfactura,
    CURRENT_DATE,
    'CR',
    'BANCO ATLANTIDA',
    'Pago factura F-DUMMY-001 — consumo mayo 2026',
    350.00,
    0.00,
    0.00,
    'C',
    CURRENT_DATE,
    'cajero_test',
    sc.id                   -- → sesion_caja.id (referencia libre)
FROM factura f
CROSS JOIN (
    SELECT id FROM sesion_caja
    WHERE usuario_apertura = 'cajero_test'
      AND company_id = 2
      AND estado = 'ABIERTA'
    LIMIT 1
) sc
WHERE f.numfactura = 'F-DUMMY-001'
  AND f.company_id = 2
RETURNING ide, recibo, tipotransaccion, creditos, estado, caja_id;

-- ── 5. Verificación: relación completa ──────────────────────
SELECT
    sc.id                  AS sesion_id,
    sc.usuario_apertura,
    sc.estado              AS estado_caja,

    ta.ide                 AS transaccion_id,
    ta.tipotransaccion,
    ta.creditos,
    ta.caja_id,

    f.numfactura,
    f.clientecodigo,
    f.saldototal           AS monto_factura,
    f.estado               AS estado_factura,

    fd.descripcion,
    fd.montovalor
FROM sesion_caja sc
JOIN transaccion_abonado ta ON ta.caja_id = sc.id
JOIN factura f              ON f.numrecibo = ta.recibo::int
JOIN factura_detalle fd     ON fd.numrecibo = f.numrecibo
WHERE sc.company_id = 2
  AND sc.usuario_apertura = 'cajero_test'
  AND sc.estado = 'ABIERTA';

-- ── 6. Resumen de caja (lógica de CajaService.ObtenerResumenAsync) ──
SELECT
    ta.tipotransaccion          AS tipo,
    SUM(ta.creditos)            AS total_creditos,
    SUM(ta.debitos)             AS total_debitos,
    COUNT(*)                    AS cantidad
FROM transaccion_abonado ta
WHERE ta.caja_id = (
    SELECT id FROM sesion_caja
    WHERE usuario_apertura = 'cajero_test'
      AND company_id = 2
      AND estado = 'ABIERTA'
    LIMIT 1
)
  AND ta.estado <> 'N'
GROUP BY ta.tipotransaccion;

ROLLBACK;
