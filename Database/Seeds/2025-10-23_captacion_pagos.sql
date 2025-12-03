-- Seeds para Captacion de Pagos (2025-10-23)
-- Nota: ajustar nombres de columnas segun el esquema real antes de ejecutar en produccion.

BEGIN;

-- 1) Caja abierta
INSERT INTO catalogo_cajas (id, nombre, estado, fecha_apertura, usuario)
VALUES (1001, 'Caja Demo', 'A', now(), 'devuser')
ON CONFLICT (id) DO NOTHING;

-- 2) Pago registrado (encabezado)
INSERT INTO pagos_hdr (numfactura, cliente_clave, fecha, total, estado, caja_id)
VALUES ('FD-20251023-0001','CL001', now(), 150.00, 'C', 1001)
ON CONFLICT (numfactura) DO NOTHING;

-- 3) Pago detalle (aplicacion a tarifas)
DO $$
DECLARE
    monto_cols TEXT[];
    dyn_sql TEXT;
    values_sql TEXT;
    col TEXT;
BEGIN
    SELECT array_agg(column_name ORDER BY CASE column_name WHEN 'monto' THEN 0 WHEN 'montovalor' THEN 1 WHEN 'monto_valor' THEN 2 ELSE 3 END)
      INTO monto_cols
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name = 'pagos_dtl'
       AND column_name IN ('monto', 'montovalor', 'monto_valor');

    IF monto_cols IS NULL OR array_length(monto_cols, 1) = 0 THEN
        EXECUTE 'ALTER TABLE IF EXISTS pagos_dtl ADD COLUMN IF NOT EXISTS montovalor numeric(18,2)';
        monto_cols := ARRAY['montovalor'];
    END IF;

    dyn_sql := format(
        'INSERT INTO pagos_dtl (numfactura, linea, servicio%s) VALUES ($1, $2, $3%s) ON CONFLICT DO NOTHING',
        (
            SELECT string_agg(format(', %I', column_name), '')
            FROM unnest(monto_cols) AS column_name
        ),
        (
            SELECT string_agg(', $4', '')
            FROM unnest(monto_cols) AS column_name
        ));
    
    EXECUTE dyn_sql USING 'FD-20251023-0001', 1, 'Servicio Agua', 150.00;
END
$$;

-- 4) Recibo miscelaneo ejemplo
INSERT INTO pagos_miscelaneos (recibo, cliente, fecha, total, estado)
VALUES (9001, 'CL002', now(), 75.00, 'C')
ON CONFLICT (recibo) DO NOTHING;

COMMIT;

-- Para ejecutar:
-- psql "host=HOST dbname=bdnes user=USER password=PW" -f Database/Seeds/2025-10-23_captacion_pagos.sql
