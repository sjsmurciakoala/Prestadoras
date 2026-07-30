-- =============================================================================
-- M3d — Corrección del criterio de cargo + recálculo de la aplicación de pagos
-- =============================================================================
-- DEFECTO CORREGIDO
-- M3b tomaba como cargo facturable las filas con `tipo_partida = '01'`. El
-- criterio correcto es **`debitos > 0`**, simétrico al de créditos:
--   * 17,150 filas llevan `tipo_partida='02'` y DÉBITO (L 6,684,942.90, todas
--     `transaccion='105'`: notas de débito, reconexiones, cortes por mora).
--     Quedaron sin factura — faltan 17,148 documentos.
--   * 64,392 filas llevan `tipo_partida='01'` y débito CERO. Generaron 17,074
--     facturas y 64,392 líneas en cero que no deberían existir.
-- Comprobación: total correcto 1,414,578,353.51 − total cargado 1,407,893,410.61
--             = 6,684,942.90, exactamente los débitos mal filtrados.
--
-- El saldo POR CLIENTE nunca estuvo mal (el libro es copia fiel del origen y
-- cuadró 25,530/25,530). Lo que estaba mal era el reparto de ese saldo entre
-- facturas, y por lo tanto la aplicación de pagos, que se recalcula entera.
--
-- LECCIÓN: la verificación de M3 que debía cazar esto usaba el MISMO filtro en
-- ambos lados, así que comparaba el error contra sí mismo y daba cero
-- diferencias. Un control que comparte el supuesto con lo que controla no
-- controla nada — por eso el control válido es contra el saldo del libro.
--
-- Requiere `simafi_stg._m3_factura` reconstruida con el filtro corregido
-- (docs/simafi_m2/m3b_prep.sql).
-- =============================================================================

\timing on
\set ON_ERROR_STOP on
SET work_mem = '1GB';
SET maintenance_work_mem = '1536MB';
SET synchronous_commit = off;
SET session_replication_role = replica;

\echo '=== 1) las aplicaciones se recalculan enteras ==='
TRUNCATE public.adm_pago_aplicacion;

\echo '=== 2) fuera las lineas en cero y las facturas que quedan vacias ==='
DELETE FROM public.factura_detalle WHERE company_id = 2 AND montovalor <= 0;

DELETE FROM public.factura p
 WHERE p.company_id = 2
   AND NOT EXISTS (SELECT 1 FROM simafi_stg._m3_factura f
                    WHERE f.recibo::bigint = p.numrecibo AND f.cliente = p.clientecodigo);

\echo '=== 3) las facturas que faltaban ==='
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
    f.total, 'A', 1, f.con_medicion, cm.categoria_servicio_id,
    'migracion_simafi', 1, 2
FROM simafi_stg._m3_factura f
JOIN public.cliente_maestro cm   ON cm.company_id = 2
                                AND trim(cm.maestro_cliente_clave) = f.cliente
LEFT JOIN simafi_stg.facturas fa ON fa.recibo = f.recibo AND trim(fa.clave) = f.cliente
WHERE NOT EXISTS (SELECT 1 FROM public.factura p
                   WHERE p.company_id = 2
                     AND p.numrecibo = f.recibo::bigint
                     AND p.clientecodigo = f.cliente);

\echo '=== 4) las lineas que faltaban (los debitos con tipo_partida 02) ==='
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
JOIN public.factura fx ON fx.company_id = 2
                      AND fx.numrecibo = t.recibo::bigint
                      AND fx.clientecodigo = trim(t.cliente)
WHERE t.debitos > 0
  AND t.tipo_partida <> '01'          -- las de '01' ya están cargadas
  AND t.recibo IS NOT NULL AND t.recibo <> 0;

\echo '=== 5) totales de cabecera desalineados ==='
UPDATE public.factura p
   SET saldototal = f.total
  FROM simafi_stg._m3_factura f
 WHERE p.company_id = 2
   AND p.numrecibo = f.recibo::bigint
   AND p.clientecodigo = f.cliente
   AND round(p.saldototal, 2) <> round(f.total, 2);

\echo '=== 6) control de cargos ANTES de recalcular pagos ==='
SELECT (SELECT count(*) FROM public.factura WHERE company_id = 2)                     AS facturas,
       (SELECT round(sum(saldototal),2) FROM public.factura WHERE company_id = 2)     AS total_facturas,
       (SELECT count(*) FROM public.factura_detalle WHERE company_id = 2)             AS lineas,
       (SELECT round(sum(montovalor),2) FROM public.factura_detalle WHERE company_id = 2) AS total_lineas,
       (SELECT round(sum(debitos),2) FROM simafi_stg.transaccion_abonado
         WHERE debitos > 0 AND recibo IS NOT NULL AND recibo <> 0)                    AS total_origen;

RESET session_replication_role;
ANALYZE public.factura;
ANALYZE public.factura_detalle;
