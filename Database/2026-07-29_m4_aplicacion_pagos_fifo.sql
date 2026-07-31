-- =============================================================================
-- M4 — Pagos y su aplicación por FIFO
-- =============================================================================
-- Migración total SIMAFI. Ver docs/PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md
-- Requiere M3 completo (factura, factura_detalle y transaccion_abonado cargados).
--
-- POR QUÉ FIFO Y NO EL DATO DE ORIGEN
-- SIMAFI **no guarda** a qué factura se aplicó cada pago — verificado en M2:
--   * `docuaplicar` está vacío en 1,368,579 de 2,474,875 pagos (55%),
--     apunta al propio recibo en 1,106,188 (redundante), a un recibo inexistente
--     en 56,171, y solo 110 registros aportan un vínculo útil;
--   * el `recibo` propio del pago tampoco sirve: 633,431 recibos quedan
--     "pagados de más" porque al cobrar atrasos SIMAFI carga todo el pago contra
--     el recibo corriente.
-- Por eso la aplicación se reconstruye: el crédito más viejo paga el cargo más
-- viejo. Esto NO altera el saldo por cliente (que ya cuadra al centavo), solo
-- reparte ese saldo entre las líneas.
--
-- ALGORITMO — asignación por solapamiento de acumulados, sin cursores.
-- Por cliente, cada línea de cargo i ocupa el intervalo (Ca[i-1], Ca[i]] sobre el
-- eje de dinero acumulado, y cada crédito j ocupa (Cr[j-1], Cr[j]]. Lo que el
-- crédito j aplica al cargo i es la longitud del solapamiento de ambos
-- intervalos. En vez de cruzar todos los pares (que sería cuadrático), se corta
-- el eje en los puntos de ambas series: cada segmento resultante pertenece a
-- exactamente un cargo y un crédito, y se resuelve con dos búsquedas por índice.
--
-- El cálculo se hace sobre las tablas del PORTAL, no sobre staging, porque
-- `factura_detalle` no conserva el `ide` del movimiento original y no habría
-- forma de mapear el resultado de vuelta.
-- =============================================================================

\timing on
\set ON_ERROR_STOP on

SET work_mem = '1GB';
SET maintenance_work_mem = '1536MB';
SET synchronous_commit = off;
SET session_replication_role = replica;

\set company 2

-- ---------------------------------------------------------------------------
-- 0) Limpieza de corridas previas de este mismo script
-- ---------------------------------------------------------------------------
\echo '--- 0/6 limpiando corrida previa ---'
DELETE FROM public.adm_pago_aplicacion WHERE company_id = :company;
DELETE FROM public.adm_pago            WHERE company_id = :company;

UPDATE public.factura_detalle SET montovalor_saldo = montovalor
 WHERE company_id = :company AND montovalor_saldo IS DISTINCT FROM montovalor;

UPDATE public.factura SET estado = 'A', estado_id = 1
 WHERE company_id = :company AND (estado <> 'A' OR estado_id <> 1);

-- ---------------------------------------------------------------------------
-- 1) CARGOS con acumulado por cliente
-- ---------------------------------------------------------------------------
\echo '--- 1/6 acumulando cargos ---'
DROP TABLE IF EXISTS _m4_cargo;
CREATE UNLOGGED TABLE _m4_cargo AS
SELECT
    d.id                                    AS detalle_id,
    d.factura_id,
    f.clientecodigo                         AS cliente,
    d.montovalor                            AS monto,
    sum(d.montovalor) OVER w                AS hasta,
    sum(d.montovalor) OVER w - d.montovalor AS desde
FROM public.factura_detalle d
JOIN public.factura f ON f.id = d.factura_id AND f.company_id = :company
WHERE d.company_id = :company AND d.montovalor > 0
WINDOW w AS (PARTITION BY f.clientecodigo
             ORDER BY f.fechaemision, d.factura_id, d.id
             ROWS UNBOUNDED PRECEDING);

CREATE INDEX ON _m4_cargo (cliente, hasta);
ANALYZE _m4_cargo;

-- ---------------------------------------------------------------------------
-- 2) CRÉDITOS con acumulado por cliente
--    Todo movimiento con crédito: pagos (201), abonos (202) y notas (203/205).
--    M5 podrá reclasificar las notas; para el saldo por línea todas cuentan.
-- ---------------------------------------------------------------------------
\echo '--- 2/6 acumulando creditos ---'
DROP TABLE IF EXISTS _m4_credito;
CREATE UNLOGGED TABLE _m4_credito AS
SELECT
    t.ide,
    t.cliente_clave                     AS cliente,
    t.fecha_docu,
    t.creditos                          AS monto,
    t.tipotransaccion,
    t.tipo_transaccion_id,
    t.banco,
    t.docufuente,
    t.recibo,
    sum(t.creditos) OVER w              AS hasta,
    sum(t.creditos) OVER w - t.creditos AS desde
FROM public.transaccion_abonado t
WHERE t.company_id = :company AND t.creditos > 0
WINDOW w AS (PARTITION BY t.cliente_clave
             ORDER BY t.fecha_docu, t.ide
             ROWS UNBOUNDED PRECEDING);

CREATE INDEX ON _m4_credito (cliente, hasta);
ANALYZE _m4_credito;

-- ---------------------------------------------------------------------------
-- 3) SEGMENTOS — se corta el eje del dinero en los puntos de ambas series
-- ---------------------------------------------------------------------------
\echo '--- 3/6 construyendo segmentos ---'
DROP TABLE IF EXISTS _m4_seg;
CREATE UNLOGGED TABLE _m4_seg AS
WITH puntos AS (
    SELECT cliente, hasta AS p FROM _m4_cargo
    UNION
    SELECT cliente, hasta AS p FROM _m4_credito
)
SELECT cliente,
       COALESCE(lag(p) OVER (PARTITION BY cliente ORDER BY p), 0) AS lo,
       p                                                          AS hi
FROM puntos;

DELETE FROM _m4_seg WHERE hi <= lo;
CREATE INDEX ON _m4_seg (cliente, hi);
ANALYZE _m4_seg;

-- ---------------------------------------------------------------------------
-- 4) ASIGNACIÓN — cada segmento pertenece a un cargo y un crédito, o a ninguno
-- ---------------------------------------------------------------------------
\echo '--- 4/6 asignando ---'
DROP TABLE IF EXISTS _m4_aplic;
CREATE UNLOGGED TABLE _m4_aplic AS
SELECT ca.detalle_id, ca.factura_id, cr.ide AS credito_ide,
       round(s.hi - s.lo, 2) AS monto
FROM _m4_seg s
JOIN LATERAL (
    SELECT c.detalle_id, c.factura_id FROM _m4_cargo c
     WHERE c.cliente = s.cliente AND c.hasta >= s.hi
     ORDER BY c.hasta LIMIT 1
) ca ON true
JOIN LATERAL (
    SELECT c.ide FROM _m4_credito c
     WHERE c.cliente = s.cliente AND c.hasta >= s.hi
     ORDER BY c.hasta LIMIT 1
) cr ON true
WHERE round(s.hi - s.lo, 2) > 0;

CREATE INDEX ON _m4_aplic (credito_ide);
CREATE INDEX ON _m4_aplic (detalle_id);
ANALYZE _m4_aplic;

-- ---------------------------------------------------------------------------
-- 5) adm_pago + adm_pago_aplicacion
-- ---------------------------------------------------------------------------
\echo '--- 5/6 creando pagos y aplicaciones ---'
INSERT INTO public.adm_pago (
    company_id, numero_recibo, cliente_clave, fecha, canal_id,
    tipo_transaccion_id, estado_id, monto_total, forma_pago,
    referencia_externa, transaccion_abonado_ide, usuario, creado_en
)
SELECT
    :company,
    -- `uq_adm_pago_numero_recibo` exige unicidad por empresa y los números de
    -- recibo de SIMAFI se repiten entre pagos (al cobrar atrasos, varios pagos
    -- comparten el recibo corriente). Se usa el `ide` del movimiento, único por
    -- construcción; el recibo original va en `referencia_externa`.
    c.ide::text,
    c.cliente,
    c.fecha_docu,
    -- Canal 2 (Banco) para todo lo migrado: estos pagos no entraron por ningún
    -- canal del portal. No puede ser 1 (Caja) porque `ck_adm_pago_caja_sesion`
    -- exige una sesión de caja abierta, que para un histórico no existe y no
    -- tiene sentido inventar. El canal original de SIMAFI se conserva en
    -- `referencia_externa`.
    2,
    COALESCE(c.tipo_transaccion_id, 2),
    1,
    c.monto,
    CASE WHEN COALESCE(trim(c.banco), '') IN ('', '01') THEN 'EFECTIVO' ELSE 'BANCO' END,
    -- `uq_adm_pago_referencia_externa` es un índice único parcial: es el
    -- mecanismo de idempotencia del WS bancario. El recibo de SIMAFI se repite
    -- entre pagos, así que aquí va NULL (el índice no aplica a los nulos).
    -- El recibo original no se pierde: está en el movimiento enlazado por
    -- `transaccion_abonado_ide`.
    NULL,
    c.ide,
    'migracion_simafi',
    now()
FROM _m4_credito c;

CREATE INDEX IF NOT EXISTS ix_adm_pago_ta_ide
    ON public.adm_pago (company_id, transaccion_abonado_ide);
ANALYZE public.adm_pago;

INSERT INTO public.adm_pago_aplicacion (
    company_id, pago_id, documento_tipo, factura_id, factura_detalle_id, monto_aplicado
)
SELECT :company, p.pago_id, 1, a.factura_id, a.detalle_id, a.monto
FROM _m4_aplic a
JOIN public.adm_pago p ON p.company_id = :company
                      AND p.transaccion_abonado_ide = a.credito_ide;

-- ---------------------------------------------------------------------------
-- 6) Saldo por línea y estado de la factura
-- ---------------------------------------------------------------------------
\echo '--- 6/6 actualizando saldos y estados ---'
WITH ap AS (
    SELECT detalle_id, round(sum(monto), 2) aplicado FROM _m4_aplic GROUP BY 1
)
UPDATE public.factura_detalle d
   SET montovalor_saldo = GREATEST(round(d.montovalor - ap.aplicado, 2), 0)
  FROM ap
 WHERE d.id = ap.detalle_id AND d.company_id = :company;

WITH est AS (
    SELECT d.factura_id,
           round(sum(d.montovalor), 2)       AS total,
           round(sum(d.montovalor_saldo), 2) AS saldo
    FROM public.factura_detalle d
    WHERE d.company_id = :company
    GROUP BY 1
)
UPDATE public.factura f
   SET estado    = CASE WHEN est.saldo <= 0.004        THEN 'C'
                        WHEN est.saldo < est.total     THEN 'B'
                        ELSE 'A' END,
       estado_id = CASE WHEN est.saldo <= 0.004        THEN 2
                        WHEN est.saldo < est.total     THEN 4
                        ELSE 1 END
  FROM est
 WHERE f.id = est.factura_id AND f.company_id = :company;

RESET session_replication_role;

ANALYZE public.factura;
ANALYZE public.factura_detalle;
ANALYZE public.adm_pago;
ANALYZE public.adm_pago_aplicacion;

-- ---------------------------------------------------------------------------
-- CONTROLES
-- ---------------------------------------------------------------------------
\echo '--- totales ---'
SELECT (SELECT count(*) FROM public.adm_pago            WHERE company_id = :company) AS pagos,
       (SELECT count(*) FROM public.adm_pago_aplicacion WHERE company_id = :company) AS aplicaciones,
       (SELECT round(sum(monto_total),2)   FROM public.adm_pago            WHERE company_id = :company) AS monto_pagos,
       (SELECT round(sum(monto_aplicado),2) FROM public.adm_pago_aplicacion WHERE company_id = :company) AS monto_aplicado;

\echo '--- estados de factura ---'
SELECT estado, estado_id, count(*), round(sum(saldototal),2) AS emitido
FROM public.factura WHERE company_id = :company GROUP BY 1,2 ORDER BY 3 DESC;

\echo '--- CONTROL: saldo pendiente por linea == saldo del cliente en el libro ---'
WITH origen AS (
    SELECT trim(t.cliente) c, round(sum(t.debitos) - sum(t.creditos), 2) saldo
    FROM simafi_stg.transaccion_abonado t
    WHERE trim(COALESCE(t.cliente,'')) <> ''
    GROUP BY 1
),
lineas AS (
    SELECT f.clientecodigo c, round(sum(d.montovalor_saldo), 2) saldo
    FROM public.factura_detalle d
    JOIN public.factura f ON f.id = d.factura_id AND f.company_id = :company
    WHERE d.company_id = :company
    GROUP BY 1
)
SELECT count(*)                                                                AS clientes,
       count(*) FILTER (WHERE abs(GREATEST(o.saldo,0) - COALESCE(l.saldo,0)) < 0.005)  AS cuadran,
       count(*) FILTER (WHERE abs(GREATEST(o.saldo,0) - COALESCE(l.saldo,0)) >= 0.005) AS difieren,
       round(sum(GREATEST(o.saldo,0)), 2)                                      AS saldo_libro,
       round(sum(COALESCE(l.saldo,0)), 2)                                      AS saldo_lineas
FROM origen o
LEFT JOIN lineas l ON l.c = o.c;
