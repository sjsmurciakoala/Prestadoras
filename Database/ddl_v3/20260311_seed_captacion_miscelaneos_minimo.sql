-- cambia 3075090 por tu recibo
SELECT numrecibo, estado, recolectora, fechapago
FROM factura
WHERE numrecibo = 3075090;




SELECT COUNT(*) AS pagos_201
FROM transaccion_abonado
WHERE recibo = 3075090 AND tipotransaccion = '201';




SELECT poliza_id, module, status, document_id, created_at, posted_at
FROM con_partida_hdr
WHERE document_id IN (3075090, 1003075090, 2003075090)
ORDER BY poliza_id DESC;


UPDATE factura_detalle
SET tiposervicio = 'SRV001',
    codigo = 'SRV001'
WHERE factura_id = (SELECT id FROM factura WHERE numrecibo = 3075090 LIMIT 1);


-- Configura tipo transaccion bancario DEP (deposito) si no existe.
-- Toma tipo partida activo por empresa, priorizando codigo COB.
WITH company_target AS (
    SELECT c.company_id
    FROM cfg_company AS c
),
tipo_partida_resuelta AS (
    SELECT
        ct.company_id,
        COALESCE(
            (
                SELECT t.type_id
                FROM public.con_tipo_transaccion AS t
                WHERE t.company_id = ct.company_id
                  AND UPPER(COALESCE(t.code, '')) = 'COB'
                  AND (
                      t.status_id = 1
                      OR UPPER(COALESCE(t.status, '')) = 'ACTIVE'
                      OR t.status IS NULL
                  )
                ORDER BY t.type_id
                LIMIT 1
            ),
            (
                SELECT t.type_id
                FROM public.con_tipo_transaccion AS t
                WHERE t.company_id = ct.company_id
                  AND t.is_default = TRUE
                  AND (
                      t.status_id = 1
                      OR UPPER(COALESCE(t.status, '')) = 'ACTIVE'
                      OR t.status IS NULL
                  )
                ORDER BY t.type_id
                LIMIT 1
            ),
            (
                SELECT t.type_id
                FROM public.con_tipo_transaccion AS t
                WHERE t.company_id = ct.company_id
                  AND (
                      t.status_id = 1
                      OR UPPER(COALESCE(t.status, '')) = 'ACTIVE'
                      OR t.status IS NULL
                  )
                ORDER BY t.type_id
                LIMIT 1
            )
        ) AS type_id
    FROM company_target AS ct
)
INSERT INTO ban_tipos_transacciones
(
    company_id,
    tipo_transaccion,
    cod_tipopartida,
    correlativo,
    cod_centrocosto,
    cuenta_contable,
    destino,
    nombre,
    observaciones,
    entra_sale,
    del_sistema,
    emite_cheque,
    pad,
    pda,
    rel_empleados,
    filtro,
    cuenta_alterna,
    estado,
    created_at,
    created_by
)
SELECT
    t.company_id,
    'DEP',
    LPAD(t.type_id::text, 3, '0'),
    '000001',
    NULL,
    NULL,
    NULL,
    'Deposito captacion',
    'Generado por seed tecnico de captacion',
    'E',
    'N',
    'N',
    'N',
    'N',
    'N',
    0,
    FALSE,
    'ACTIVE',
    NOW(),
    'seed_captacion'
FROM tipo_partida_resuelta AS t
WHERE t.type_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM ban_tipos_transacciones AS b
      WHERE b.company_id = t.company_id
        AND UPPER(COALESCE(b.tipo_transaccion, '')) = 'DEP'
  );

-- Verificacion:
SELECT
    b.company_id,
    b.tipo_transaccion,
    b.nombre,
    b.cod_tipopartida,
    b.correlativo,
    b.entra_sale,
    b.estado
FROM ban_tipos_transacciones AS b
WHERE UPPER(COALESCE(b.tipo_transaccion, '')) = 'DEP'
ORDER BY b.company_id;
