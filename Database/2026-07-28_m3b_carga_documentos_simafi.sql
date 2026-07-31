-- =============================================================================
-- M3b — Carga de documentos SIMAFI → factura + factura_detalle + libro
-- =============================================================================
-- Migración total SIMAFI. Ver docs/PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md
--
-- Requiere: simafi_stg (M1), M3a ya corrido, y la tabla de trabajo
--           simafi_stg._m3_factura (cabeceras por cliente+recibo, ver
--           docs/simafi_m2/m3b_prep.sql).
--
-- Uso (carga completa, ~30-60 min):
--     psql ... -f Database/2026-07-28_m3b_carga_documentos_simafi.sql
--
-- FUENTE PRIMARIA: el libro `transaccion_abonado`, no `facturas` (ver plan §5.M3).
--   - `facturas` solo enriquece `fechavence` donde existe (2,875,588 de 3,827,204
--     recibos; el resto fue archivado por SIMAFI).
--   - La clave natural es (cliente, recibo): el número de recibo NO es único
--     por cliente — 38,457 están compartidos.
--
-- Las facturas entran como 'A' (pendiente) con el saldo completo por línea.
-- La aplicación de pagos y el estado real se resuelven en M4 por FIFO,
-- porque SIMAFI no guarda a qué factura se aplicó cada pago.
--
-- ---------------------------------------------------------------------------
-- ESTRATEGIA DE CARGA MASIVA — no cambiar a la ligera
-- ---------------------------------------------------------------------------
-- Un primer intento con `NOT EXISTS` por fila + FKs + índices activos corría a
-- ~79 filas/s y proyectaba DÍAS. Este script en cambio:
--   * reconstruye desde cero en vez de ser idempotente fila por fila
--     (borra lo migrado por los umbrales de id previos a la migración),
--   * desactiva los disparadores de FK con session_replication_role = replica,
--   * crea el índice de apoyo DESPUÉS de poblar `factura`, no antes,
--   * usa synchronous_commit = off.
-- Es reejecutable: siempre parte de limpiar lo suyo.
-- =============================================================================

\timing on
\set ON_ERROR_STOP on

SET work_mem = '1GB';
SET maintenance_work_mem = '1536MB';
SET synchronous_commit = off;
SET session_replication_role = replica;   -- desactiva verificación de FK

-- Umbrales: máximos existentes ANTES de la migración (datos del piloto que se
-- conservan intactos). Todo lo que esté por encima es carga migrada.
\set max_factura_id 170
\set max_detalle_id 492
\set max_libro_ide 3076782

-- ---------------------------------------------------------------------------
-- 0) LIMPIEZA de cargas previas de este mismo script
-- ---------------------------------------------------------------------------
\echo '--- limpiando carga previa ---'
DELETE FROM public.factura_detalle     WHERE id  > :max_detalle_id;
DELETE FROM public.transaccion_abonado WHERE ide > :max_libro_ide;
DELETE FROM public.factura             WHERE id  > :max_factura_id;

DROP INDEX IF EXISTS public.ix_factura_company_recibo_cliente;
DROP INDEX IF EXISTS public.ix_ta_company_cliente;

-- ---------------------------------------------------------------------------
-- 1) CABECERAS DE FACTURA
--    `numrecibo` es identidad GENERATED ALWAYS, pero el criterio del proyecto es
--    conservar la numeración original de SIMAFI: se fuerza con OVERRIDING SYSTEM
--    VALUE. `id` sí se deja generar. La secuencia se reposiciona al final.
-- ---------------------------------------------------------------------------
\echo '--- 1/3 cabeceras de factura ---'
INSERT INTO public.factura (
    numrecibo, clientecodigo, tipofactura, ano, mes,
    fechaemision, fechavence, periodo, saldototal,
    estado, estado_id, con_medicion, categoria_servicio_id,
    usuario, tipo_documento_fiscal_id, company_id
)
OVERRIDING SYSTEM VALUE
SELECT
    f.recibo::bigint,
    f.cliente,
    'F',
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN split_part(f.periodo, '/', 1)
         ELSE to_char(f.fecha_emision, 'YYYY') END,
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN ltrim(split_part(f.periodo, '/', 2), '0')
         ELSE to_char(f.fecha_emision, 'FMMM') END,
    f.fecha_emision,
    COALESCE(fa.vence, f.fecha_emision + 15),
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN f.periodo
         ELSE to_char(f.fecha_emision, 'YYYY/MM') END,
    f.total,
    'A',
    1,
    f.con_medicion,
    cm.categoria_servicio_id,
    'migracion_simafi',
    1,
    2
FROM simafi_stg._m3_factura f
JOIN public.cliente_maestro cm    ON cm.company_id = 2
                                 AND trim(cm.maestro_cliente_clave) = f.cliente
LEFT JOIN simafi_stg.facturas fa  ON fa.recibo = f.recibo
                                 AND trim(fa.clave) = f.cliente;

\echo '--- índice de apoyo (después de poblar, no antes) ---'
CREATE INDEX ix_factura_company_recibo_cliente
    ON public.factura (company_id, numrecibo, clientecodigo);
ANALYZE public.factura;

-- ---------------------------------------------------------------------------
-- 2) DETALLE — una línea por cargo del libro
-- ---------------------------------------------------------------------------
\echo '--- 2/3 detalle de factura ---'
INSERT INTO public.factura_detalle (
    numrecibo, codigo, tiposervicio, descripcion,
    montovalor, factura_id, montovalor_saldo, company_id
)
SELECT
    t.recibo::bigint,
    trim(t.transaccion),
    CASE trim(t.transaccion)
        WHEN '101' THEN 'AGUA_POTABLE'
        WHEN '102' THEN 'ALCANTARILLADO'
        WHEN '103' THEN 'TASA_AMBIENTAL'
        WHEN '104' THEN 'TASA_SVA_ERSAPS'
        WHEN '11'  THEN 'CORTE_RECONEXION'
        WHEN '111' THEN 'CORTE_RECONEXION'
        WHEN '105' THEN CASE
                            WHEN COALESCE(t.agua, 0)           <> 0 THEN 'AGUA_POTABLE'
                            WHEN COALESCE(t.alcantarillado, 0) <> 0 THEN 'ALCANTARILLADO'
                            WHEN COALESCE(t.ambiental, 0)      <> 0 THEN 'TASA_AMBIENTAL'
                            ELSE 'OTROS_COLATERALES'
                        END
        ELSE 'OTROS_COLATERALES'
    END,
    left(trim(COALESCE(t.descripcion, '')), 250),
    t.debitos,
    fx.id,
    t.debitos,            -- saldo completo: los pagos se aplican en M4
    2
FROM simafi_stg.transaccion_abonado t
JOIN public.factura fx ON fx.company_id = 2
                      AND fx.numrecibo = t.recibo::bigint
                      AND fx.clientecodigo = trim(t.cliente)
-- ⚠️ El criterio es `debitos > 0`, NO `tipo_partida = '01'`.
-- `tipo_partida` es casi siempre 01=cargo / 02=abono, pero hay **17,150 filas
-- con tipo_partida='02' y débito** (L 6,684,942.90, todas transaccion='105':
-- notas de débito, reconexiones y cortes por mora). Filtrar por tipo_partida
-- las deja sin línea de factura aunque el saldo del cliente sí las incluya.
WHERE t.debitos > 0
  AND t.recibo IS NOT NULL AND t.recibo <> 0;

-- ---------------------------------------------------------------------------
-- 3) LIBRO — copia fiel del movimiento original
-- ---------------------------------------------------------------------------
\echo '--- 3/3 libro de movimientos ---'
INSERT INTO public.transaccion_abonado (
    cliente_clave, recibo, tipotransaccion, docufuente, docufuente2,
    fecha_docu, tipo_partida, banco, descripcion, plazo,
    docuaplicar, trans_aplicar, debitos, creditos,
    tipo_servicio, aplicar_alca, periodo, tasa, estado, fecha_registro,
    ciclo, ruta, secuencia, tiene_med, codigoplan, motivo, usuario,
    tipo_transaccion_id, estado_id, company_id
)
SELECT
    trim(t.cliente), t.recibo, trim(t.transaccion), t.docufuente, trim(t.docufuente2),
    t.fecha_docu, t.tipo_partida, trim(t.banco), left(trim(COALESCE(t.descripcion,'')), 250), t.plazo,
    t.docuaplicar, trim(t.trans_aplicar), t.debitos, t.creditos,
    trim(t.tipo_servicio), trim(t.aplicar_alca), trim(t.periodo), trim(t.tasa),
    trim(t.estado), t.fecha_registro,
    trim(t.ciclo), trim(t.ruta), trim(t.secuencia), trim(t.tiene_med),
    trim(t.codigoplan), trim(t.motivo), trim(t.usuario),
    CASE trim(t.transaccion)
        WHEN '201' THEN CASE WHEN trim(COALESCE(t.banco,'')) = '01' THEN 2 ELSE 3 END
        WHEN '202' THEN 4
        WHEN '203' THEN 5
        WHEN '205' THEN 5
        WHEN '105' THEN 6
        ELSE 1
    END,
    1,
    2
FROM simafi_stg.transaccion_abonado t
JOIN public.cliente_maestro cm ON cm.company_id = 2
                              AND trim(cm.maestro_cliente_clave) = trim(t.cliente);

-- ---------------------------------------------------------------------------
-- 4) RECONSTRUIR índices y estadísticas; reponer la secuencia
-- ---------------------------------------------------------------------------
\echo '--- reconstruyendo índices ---'
CREATE INDEX ix_ta_company_cliente
    ON public.transaccion_abonado (company_id, cliente_clave);

ANALYZE public.factura;
ANALYZE public.factura_detalle;
ANALYZE public.transaccion_abonado;

SELECT setval(
    pg_get_serial_sequence('public.factura', 'numrecibo'),
    GREATEST(
        (SELECT max(numrecibo) FROM public.factura),
        (SELECT max(recibo)::bigint FROM simafi_stg._m3_factura)
    ) + 1000
);

RESET session_replication_role;

\echo '--- totales ---'
SELECT
    (SELECT count(*) FROM public.factura             WHERE company_id = 2) AS facturas,
    (SELECT count(*) FROM public.factura_detalle     WHERE company_id = 2) AS detalle,
    (SELECT count(*) FROM public.transaccion_abonado WHERE company_id = 2) AS libro;

-- ---------------------------------------------------------------------------
-- CONTROL: el saldo del portal debe reproducir el del libro de SIMAFI
--          (criterio de aceptación de M6)
-- ---------------------------------------------------------------------------
\echo '--- control de saldo por cliente: portal vs SIMAFI ---'
WITH origen AS (
    SELECT trim(t.cliente) c, round(sum(t.debitos) - sum(t.creditos), 2) saldo
    FROM simafi_stg.transaccion_abonado t
    GROUP BY 1
),
portal AS (
    SELECT p.cliente_clave c, round(sum(p.debitos) - sum(p.creditos), 2) saldo
    FROM public.transaccion_abonado p
    WHERE p.company_id = 2
    GROUP BY 1
)
SELECT
    count(*)                                                             AS clientes,
    count(*) FILTER (WHERE abs(o.saldo - COALESCE(pt.saldo,0)) < 0.005)  AS cuadran,
    count(*) FILTER (WHERE abs(o.saldo - COALESCE(pt.saldo,0)) >= 0.005) AS difieren,
    round(sum(o.saldo), 2)                                               AS saldo_simafi,
    round(sum(COALESCE(pt.saldo,0)), 2)                                  AS saldo_portal
FROM origen o
LEFT JOIN portal pt ON pt.c = o.c;
