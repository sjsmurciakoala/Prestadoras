-- =============================================================================
-- Migración Bancos SIMAFI  ·  Script 04/4 : KARDEX (visible en la UI)
-- Transforma ban_movimiento (landing) -> ban_kardex para que las transacciones
-- aparezcan en la pantalla "Transacciones Bancarias".
--
-- ALCANCE (decisión del usuario 2026-07-09):
--   · Solo cuentas NUEVAS (código SIMC%) — no toca el kardex vivo de las existentes.
--   · Solo movimientos de 2025 (fecha_movimiento >= 2025-01-01).
--   · monto CON SIGNO = mto_db - mto_cr (depósito +, cheque/ND −).
--   · saldo = running acumulado por cuenta ARRANCANDO EN 0.
--     OJO: NO es el saldo bancario real. El histórico migrado es solo egresos/cheques
--     (los depósitos/ingresos no están en estos datos legacy). Es un running de lo mostrado.
-- Idempotente: borra y recarga SOLO el kardex de las cuentas SIMC (que no tienen kardex ajeno).
-- =============================================================================
BEGIN;

DROP TABLE IF EXISTS tmp_kardex_params;
CREATE TEMP TABLE tmp_kardex_params AS
SELECT 2::bigint AS company_id, 'migracion'::text AS created_by,
       DATE '2025-01-01' AS fecha_desde,
       (SELECT ban_moneda_id FROM public.ban_moneda WHERE company_id=2 ORDER BY ban_moneda_id LIMIT 1) AS ban_moneda_id;

-- Guard: deben existir los movimientos landing
DO $$
BEGIN
    IF (SELECT count(*) FROM public.ban_movimiento
         WHERE company_id = 2 AND origen_legacy IS NOT NULL) = 0 THEN
        RAISE EXCEPTION 'No hay ban_movimiento migrados: ejecute el 02_transform antes del 04.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Asegurar tipos de transacción (sin disturbar los existentes)
-- ---------------------------------------------------------------------------
INSERT INTO public.ban_tipos_transacciones
    (company_id, tipo_transaccion, cod_tipopartida, correlativo, nombre, entra_sale, del_sistema, estado, created_by)
SELECT 2, v.code, v.partida, '000000', v.nombre, v.es, 'N', 'ACTIVE', 'migracion'
FROM (VALUES
    ('CHQ','5','Cheques','S'),
    ('DEP','2','Depósito','E'),
    ('NDB','1','Nota de débito','S'),
    ('NCR','1','Nota de crédito','E'),
    ('VOU','0','Voucher (libro banco)','S')
) v(code, partida, nombre, es)
WHERE NOT EXISTS (
    SELECT 1 FROM public.ban_tipos_transacciones t
     WHERE t.company_id = 2 AND upper(btrim(t.tipo_transaccion)) = v.code);

-- 2) Mapa: tipo legacy (ban_movimiento.tipo) -> id_tipo_transaccion
--    D# ("DEBITO") resultan ser depósitos (neto +) -> DEP.
DROP TABLE IF EXISTS tmp_tipo_map;
CREATE TEMP TABLE tmp_tipo_map AS
SELECT v.legacy,
       (SELECT t.ban_tipo_transaccion_id FROM public.ban_tipos_transacciones t
         WHERE t.company_id=2 AND upper(btrim(t.tipo_transaccion))=v.code
         ORDER BY t.ban_tipo_transaccion_id LIMIT 1) AS id_tipo_transaccion
FROM (VALUES
    ('CHEQUE','CHQ'), ('ND','NDB'), ('NC','NCR'),
    ('DEBITO','DEP'), ('VOUCHER','VOU'), ('OTRO','VOU')
) v(legacy, code);

-- ---------------------------------------------------------------------------
-- 3) Limpieza idempotente: SOLO kardex de cuentas SIMC (sin kardex ajeno)
-- ---------------------------------------------------------------------------
DELETE FROM public.ban_kardex
 WHERE company_id = 2
   AND banco_cuenta_id IN (
        SELECT banco_cuenta_id FROM public.ban_cuenta
         WHERE company_id = 2 AND code LIKE 'SIMC%');

-- ---------------------------------------------------------------------------
-- 4) Cargar kardex 2025 con saldo running (desde 0) por cuenta
-- ---------------------------------------------------------------------------
INSERT INTO public.ban_kardex
    (company_id, banco_cuenta_id, ban_banco_id, ban_moneda_id, id_tipo_transaccion,
     fecha_movimiento, fecha_registro, fecha_conciliacion, estado_conciliacion,
     descripcion, referencia, monto, saldo, estado, tasa_cambio,
     correlativo_t_transacc, created_at, created_by)
SELECT
    2,
    x.banco_cuenta_id,
    x.ban_banco_id,
    (SELECT ban_moneda_id FROM tmp_kardex_params),
    x.id_tipo_transaccion,
    x.fecha_movimiento,
    now(),
    NULL,
    'NOC',
    left(x.descripcion, 500),
    left(x.referencia, 100),
    x.monto,
    sum(x.monto) OVER (PARTITION BY x.banco_cuenta_id
                       ORDER BY x.fecha_movimiento, x.movimiento_id
                       ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    1,
    1,
    lpad(row_number() OVER (PARTITION BY x.banco_cuenta_id
                            ORDER BY x.fecha_movimiento, x.movimiento_id)::text, 6, '0'),
    now(),
    'migracion'
FROM (
    SELECT m.movimiento_id, m.banco_cuenta_id, c.ban_banco_id,
           tm.id_tipo_transaccion,
           m.fecha_movimiento,
           COALESCE(NULLIF(btrim(m.descripcion), ''),
                    CASE WHEN m.origen_legacy='detalleck' THEN 'Voucher libro banco'
                         ELSE m.tipo END)                        AS descripcion,
           COALESCE(NULLIF(btrim(m.documento), ''), btrim(m.c_refer)) AS referencia,
           (m.mto_db - m.mto_cr)                                 AS monto
    FROM public.ban_movimiento m
    JOIN public.ban_cuenta c ON c.banco_cuenta_id = m.banco_cuenta_id
    JOIN tmp_tipo_map tm ON tm.legacy = m.tipo
    WHERE m.company_id = 2
      AND m.origen_legacy IS NOT NULL
      AND c.code LIKE 'SIMC%'
      AND m.fecha_movimiento >= (SELECT fecha_desde FROM tmp_kardex_params)
) x;

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación (solo lectura)
-- ---------------------------------------------------------------------------
\echo '== Kardex cargado por cuenta (saldo running = NO es saldo real) =='
SELECT c.code, c.banco_nombre,
       count(*) AS movimientos_2025,
       min(k.fecha_movimiento) AS desde, max(k.fecha_movimiento) AS hasta,
       round((SELECT k2.saldo FROM public.ban_kardex k2
               WHERE k2.company_id=2 AND k2.banco_cuenta_id=k.banco_cuenta_id
               ORDER BY k2.fecha_movimiento DESC, k2.ban_kardex_id DESC LIMIT 1),2) AS saldo_running_final
FROM public.ban_kardex k
JOIN public.ban_cuenta c ON c.banco_cuenta_id = k.banco_cuenta_id
WHERE k.company_id=2 AND c.code LIKE 'SIMC%'
GROUP BY c.code, c.banco_nombre, k.banco_cuenta_id
ORDER BY c.code;

\echo '== Total cargado vs esperado (movimientos 2025 SIMC) =='
SELECT (SELECT count(*) FROM public.ban_kardex k JOIN public.ban_cuenta c ON c.banco_cuenta_id=k.banco_cuenta_id
         WHERE k.company_id=2 AND c.code LIKE 'SIMC%') AS kardex_cargado,
       (SELECT count(*) FROM public.ban_movimiento m JOIN public.ban_cuenta c ON c.banco_cuenta_id=m.banco_cuenta_id
         WHERE m.company_id=2 AND m.origen_legacy IS NOT NULL AND c.code LIKE 'SIMC%'
           AND m.fecha_movimiento >= DATE '2025-01-01') AS esperado_2025;
