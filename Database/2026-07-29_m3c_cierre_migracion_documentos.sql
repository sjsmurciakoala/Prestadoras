-- =============================================================================
-- M3c — Cierre de la migración de documentos: las dos brechas del control
-- =============================================================================
-- Tras correr M3a y M3b el control de saldo dio 24,559 de 25,531 clientes
-- exactos. Las 971 diferencias quedaron explicadas al 100% por dos causas:
--
--   847 clientes  +L 1,819,826.81  doble conteo con los documentos que el propio
--                                  portal emitió durante el piloto de 2 ciclos
--                                  (julio 2026). El importe coincide exactamente
--                                  con el neto de esos 1,192 movimientos.
--   124 clientes  -L   203,952.06  tienen historia en el libro de SIMAFI pero no
--                                  aparecen en el volcado de `maestrosep`, así que
--                                  se quedaron sin ficha y sin migrar.
--
-- Este script resuelve ambas.
--
-- ⚠️ El paso 1 BORRA documentos fiscales (122 de las 124 facturas del piloto
--    llevan número CAI). Decisión tomada el 2026-07-29: SIMAFI es el sistema de
--    registro hasta el cutover, así que esos documentos quedan superseded por la
--    migración. Revisar antes de correr esto donde los documentos importen.
-- =============================================================================

\timing on
\set ON_ERROR_STOP on

SET work_mem = '1GB';
SET synchronous_commit = off;
SET session_replication_role = replica;

-- Umbrales: lo que existía ANTES de la migración es el piloto.
\set max_factura_id 170
\set max_detalle_id 492
\set max_libro_ide 3076782

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Eliminar los documentos del piloto (superseded por la migración)
-- ---------------------------------------------------------------------------
\echo '--- 1/3 eliminando documentos del piloto ---'
DELETE FROM public.factura_detalle     WHERE company_id = 2 AND id  <= :max_detalle_id;
DELETE FROM public.transaccion_abonado WHERE company_id = 2 AND ide <= :max_libro_ide;
DELETE FROM public.factura             WHERE company_id = 2 AND id  <= :max_factura_id;

-- ---------------------------------------------------------------------------
-- 2) Fichas para los clientes que solo viven en el libro
--    El nombre se recupera de `facturacion` cuando existe; si no, queda marcado
--    para que se vea que falta, en vez de inventarlo.
-- ---------------------------------------------------------------------------
\echo '--- 2/3 creando fichas faltantes ---'
CREATE TEMP TABLE _sin_ficha ON COMMIT DROP AS
SELECT trim(t.cliente)                              AS clave,
       max(trim(COALESCE(t.ciclo, '')))             AS ciclo,
       max(trim(COALESCE(t.ruta, '')))              AS ruta,
       max(trim(COALESCE(t.secuencia, '')))         AS secuencia,
       bool_or(trim(COALESCE(t.tiene_med,'')) = 'S') AS tiene_med
FROM simafi_stg.transaccion_abonado t
WHERE trim(COALESCE(t.cliente, '')) <> ''
  AND NOT EXISTS (SELECT 1 FROM public.cliente_maestro m
                   WHERE m.company_id = 2
                     AND trim(m.maestro_cliente_clave) = trim(t.cliente))
GROUP BY trim(t.cliente);

CREATE UNIQUE INDEX ON _sin_ficha(clave);

SELECT count(*) AS fichas_a_crear FROM _sin_ficha;

INSERT INTO public.cliente_maestro (
    maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
    maestro_cliente_indicativo_ruta, maestro_cliente_secuencia,
    maestro_cliente_tiene_medidor, estado, ciclos_id,
    usuariocreacion, fechacreacion, company_id
)
SELECT
    s.clave,
    '',
    COALESCE(
        (SELECT trim(f.nombre) FROM simafi_stg.facturacion f
          WHERE trim(f.clave) = s.clave AND trim(COALESCE(f.nombre,'')) <> ''
          LIMIT 1),
        '(sin nombre en SIMAFI)'
    ),
    nullif(s.ruta, ''),
    nullif(s.secuencia, ''),
    s.tiene_med,
    false,                       -- sin ficha en el maestro de SIMAFI ⇒ inactivo
    (SELECT c.ciclos_id FROM public.ciclos c WHERE c.ciclos_id::text = s.ciclo),
    'migracion_simafi_sin_ficha',
    now(),
    2
FROM _sin_ficha s;

-- ---------------------------------------------------------------------------
-- 3) Migrar los documentos de esos clientes
-- ---------------------------------------------------------------------------
\echo '--- 3/3 migrando sus documentos ---'

INSERT INTO public.factura (
    numrecibo, clientecodigo, tipofactura, ano, mes,
    fechaemision, fechavence, periodo, saldototal,
    estado, estado_id, con_medicion, categoria_servicio_id,
    usuario, tipo_documento_fiscal_id, company_id
)
OVERRIDING SYSTEM VALUE
SELECT
    f.recibo::bigint, f.cliente, 'F',
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN split_part(f.periodo,'/',1)
         ELSE to_char(f.fecha_emision,'YYYY') END,
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN ltrim(split_part(f.periodo,'/',2),'0')
         ELSE to_char(f.fecha_emision,'FMMM') END,
    f.fecha_emision,
    COALESCE(fa.vence, f.fecha_emision + 15),
    CASE WHEN f.periodo ~ '^\d{4}/\d{1,2}$' THEN f.periodo
         ELSE to_char(f.fecha_emision,'YYYY/MM') END,
    f.total, 'A', 1, f.con_medicion, NULL,
    'migracion_simafi', 1, 2
FROM simafi_stg._m3_factura f
JOIN _sin_ficha s ON s.clave = f.cliente
LEFT JOIN simafi_stg.facturas fa ON fa.recibo = f.recibo AND trim(fa.clave) = f.cliente;

INSERT INTO public.factura_detalle (
    numrecibo, codigo, tiposervicio, descripcion,
    montovalor, factura_id, montovalor_saldo, company_id
)
SELECT
    t.recibo::bigint, trim(t.transaccion),
    CASE trim(t.transaccion)
        WHEN '101' THEN 'AGUA_POTABLE'   WHEN '102' THEN 'ALCANTARILLADO'
        WHEN '103' THEN 'TASA_AMBIENTAL' WHEN '104' THEN 'TASA_SVA_ERSAPS'
        WHEN '11'  THEN 'CORTE_RECONEXION' WHEN '111' THEN 'CORTE_RECONEXION'
        WHEN '105' THEN CASE WHEN COALESCE(t.agua,0) <> 0 THEN 'AGUA_POTABLE'
                             WHEN COALESCE(t.alcantarillado,0) <> 0 THEN 'ALCANTARILLADO'
                             WHEN COALESCE(t.ambiental,0) <> 0 THEN 'TASA_AMBIENTAL'
                             ELSE 'OTROS_COLATERALES' END
        ELSE 'OTROS_COLATERALES'
    END,
    left(trim(COALESCE(t.descripcion,'')), 250),
    t.debitos, fx.id, t.debitos, 2
FROM simafi_stg.transaccion_abonado t
JOIN _sin_ficha s      ON s.clave = trim(t.cliente)
JOIN public.factura fx ON fx.company_id = 2
                      AND fx.numrecibo = t.recibo::bigint
                      AND fx.clientecodigo = trim(t.cliente)
WHERE t.tipo_partida = '01' AND t.recibo IS NOT NULL AND t.recibo <> 0;

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
    t.fecha_docu, t.tipo_partida, trim(t.banco), left(trim(COALESCE(t.descripcion,'')),250), t.plazo,
    t.docuaplicar, trim(t.trans_aplicar), t.debitos, t.creditos,
    trim(t.tipo_servicio), trim(t.aplicar_alca), trim(t.periodo), trim(t.tasa),
    trim(t.estado), t.fecha_registro,
    trim(t.ciclo), trim(t.ruta), trim(t.secuencia), trim(t.tiene_med),
    trim(t.codigoplan), trim(t.motivo), trim(t.usuario),
    CASE trim(t.transaccion)
        WHEN '201' THEN CASE WHEN trim(COALESCE(t.banco,'')) = '01' THEN 2 ELSE 3 END
        WHEN '202' THEN 4 WHEN '203' THEN 5 WHEN '205' THEN 5 WHEN '105' THEN 6
        ELSE 1
    END,
    1, 2
FROM simafi_stg.transaccion_abonado t
JOIN _sin_ficha s ON s.clave = trim(t.cliente);

COMMIT;

RESET session_replication_role;

ANALYZE public.factura;
ANALYZE public.factura_detalle;
ANALYZE public.transaccion_abonado;

-- ---------------------------------------------------------------------------
-- CONTROL FINAL — criterio de aceptación de M6
-- ---------------------------------------------------------------------------
\echo '--- totales ---'
SELECT
    (SELECT count(*) FROM public.cliente_maestro     WHERE company_id = 2) AS clientes,
    (SELECT count(*) FROM public.factura             WHERE company_id = 2) AS facturas,
    (SELECT count(*) FROM public.factura_detalle     WHERE company_id = 2) AS detalle,
    (SELECT count(*) FROM public.transaccion_abonado WHERE company_id = 2) AS libro;

\echo '--- control de saldo por cliente: portal vs SIMAFI ---'
WITH origen AS (
    SELECT trim(t.cliente) c, round(sum(t.debitos) - sum(t.creditos), 2) saldo
    FROM simafi_stg.transaccion_abonado t
    WHERE trim(COALESCE(t.cliente,'')) <> ''
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
