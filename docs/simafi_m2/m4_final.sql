\timing on
\set ON_ERROR_STOP on
SET work_mem = '1GB';
SET maintenance_work_mem = '1536MB';
SET synchronous_commit = off;
SET session_replication_role = replica;

-- M4 completo tras la corrección de cargos (M3d). Todo por reconstrucción:
-- en este disco los UPDATE masivos van a 200-400 filas/s y las CTAS 30-60x más.

\echo '=== 1/6 cargos acumulados ==='
DROP TABLE IF EXISTS _m4_cargo;
CREATE UNLOGGED TABLE _m4_cargo AS
SELECT d.id AS detalle_id, d.factura_id, f.clientecodigo AS cliente, d.montovalor AS monto,
       sum(d.montovalor) OVER w AS hasta, sum(d.montovalor) OVER w - d.montovalor AS desde
FROM public.factura_detalle d
JOIN public.factura f ON f.id = d.factura_id AND f.company_id = 2
WHERE d.company_id = 2 AND d.montovalor > 0
WINDOW w AS (PARTITION BY f.clientecodigo ORDER BY f.fechaemision, d.factura_id, d.id
             ROWS UNBOUNDED PRECEDING);
CREATE INDEX ON _m4_cargo (cliente, hasta);
ANALYZE _m4_cargo;

\echo '=== 2/6 creditos acumulados ==='
DROP TABLE IF EXISTS _m4_credito;
CREATE UNLOGGED TABLE _m4_credito AS
SELECT t.ide, t.cliente_clave AS cliente, t.fecha_docu, t.creditos AS monto,
       t.tipotransaccion, t.tipo_transaccion_id, t.banco, t.docufuente, t.recibo,
       sum(t.creditos) OVER w AS hasta, sum(t.creditos) OVER w - t.creditos AS desde
FROM public.transaccion_abonado t
WHERE t.company_id = 2 AND t.creditos > 0
WINDOW w AS (PARTITION BY t.cliente_clave ORDER BY t.fecha_docu, t.ide
             ROWS UNBOUNDED PRECEDING);
CREATE INDEX ON _m4_credito (cliente, hasta);
ANALYZE _m4_credito;

\echo '=== 3/6 segmentos ==='
DROP TABLE IF EXISTS _m4_seg;
CREATE UNLOGGED TABLE _m4_seg AS
WITH puntos AS (
    SELECT cliente, hasta AS p FROM _m4_cargo
    UNION
    SELECT cliente, hasta AS p FROM _m4_credito
)
SELECT cliente, COALESCE(lag(p) OVER (PARTITION BY cliente ORDER BY p), 0) AS lo, p AS hi
FROM puntos;
DELETE FROM _m4_seg WHERE hi <= lo;
CREATE INDEX ON _m4_seg (cliente, hi);
ANALYZE _m4_seg;

\echo '=== 4/6 asignacion FIFO ==='
DROP TABLE IF EXISTS _m4_aplic;
CREATE UNLOGGED TABLE _m4_aplic AS
SELECT ca.detalle_id, ca.factura_id, cr.ide AS credito_ide, round(s.hi - s.lo, 2) AS monto
FROM _m4_seg s
JOIN LATERAL (SELECT c.detalle_id, c.factura_id FROM _m4_cargo c
               WHERE c.cliente = s.cliente AND c.hasta >= s.hi
               ORDER BY c.hasta LIMIT 1) ca ON true
JOIN LATERAL (SELECT c.ide FROM _m4_credito c
               WHERE c.cliente = s.cliente AND c.hasta >= s.hi
               ORDER BY c.hasta LIMIT 1) cr ON true
WHERE round(s.hi - s.lo, 2) > 0;
CREATE INDEX ON _m4_aplic (detalle_id);
ANALYZE _m4_aplic;

\echo '=== 5/6 aplicaciones ==='
TRUNCATE public.adm_pago_aplicacion;
INSERT INTO public.adm_pago_aplicacion
    (company_id, pago_id, documento_tipo, factura_id, factura_detalle_id, monto_aplicado)
SELECT 2, p.pago_id, 1, a.factura_id, a.detalle_id, a.monto
FROM _m4_aplic a
JOIN public.adm_pago p ON p.company_id = 2 AND p.transaccion_abonado_ide = a.credito_ide;
ANALYZE public.adm_pago_aplicacion;

\echo '=== 6/6 saldo por linea y estado de factura (por reconstruccion) ==='
DROP TABLE IF EXISTS factura_detalle_new;
CREATE TABLE factura_detalle_new AS
SELECT d.id, d.numrecibo, d.codigo, d.tiposervicio, d.descripcion, d.montovalor, d.factura_id,
       GREATEST(round(d.montovalor - COALESCE(ap.aplicado,0), 2), 0) AS montovalor_saldo,
       d.company_id
FROM public.factura_detalle d
LEFT JOIN (SELECT detalle_id, round(sum(monto),2) AS aplicado FROM _m4_aplic GROUP BY 1) ap
       ON ap.detalle_id = d.id;

DROP TABLE IF EXISTS factura_estado_new;
CREATE TABLE factura_estado_new AS
SELECT factura_id, round(sum(montovalor),2) total, round(sum(montovalor_saldo),2) saldo
FROM factura_detalle_new GROUP BY factura_id;
CREATE UNIQUE INDEX ON factura_estado_new (factura_id);
ANALYZE factura_estado_new;

DROP TABLE IF EXISTS factura_new;
CREATE TABLE factura_new AS
SELECT f.id, f.numrecibo, f.numfactura, f.clientecodigo, f.tipofactura, f.ano, f.mes,
       f.fechaemision, f.fechavence, f.rtn, f.periodo, f.numdei, f.saldototal, f.usuario,
       f.identidad,
       CASE WHEN e.factura_id IS NULL THEN f.estado
            WHEN e.saldo <= 0.004 THEN 'C' WHEN e.saldo < e.total THEN 'B' ELSE 'A' END::varchar AS estado,
       f.recolectora, f.fechapago, f.tipofacturacion, f.referencia, f.establecimiento_id,
       f.rtn_emisor, f.razon_social_emisor, f.direccion_emisor, f.tipo_documento_fiscal_id,
       f.factura_origen_id, f.motivo_anulacion_id, f.leyenda_cai_rango, f.fecha_limite_cai,
       f.company_id,
       CASE WHEN e.factura_id IS NULL THEN f.estado_id
            WHEN e.saldo <= 0.004 THEN 2::smallint WHEN e.saldo < e.total THEN 4::smallint
            ELSE 1::smallint END AS estado_id,
       f.updated_at, f.categoria_servicio_id, f.con_medicion
FROM public.factura f LEFT JOIN factura_estado_new e ON e.factura_id = f.id;

BEGIN;
ALTER TABLE public.adm_pago_aplicacion    DROP CONSTRAINT adm_pago_aplicacion_factura_detalle_id_fkey;
ALTER TABLE public.adm_pago_aplicacion    DROP CONSTRAINT adm_pago_aplicacion_factura_id_fkey;
ALTER TABLE public.cln_plan_pago_traslado DROP CONSTRAINT cln_plan_pago_traslado_factura_detalle_id_fkey;
ALTER TABLE public.cln_plan_pago_traslado DROP CONSTRAINT cln_plan_pago_traslado_factura_id_fkey;
ALTER TABLE public.adm_nota_credito       DROP CONSTRAINT fk_adm_nota_credito_factura_origen;
ALTER TABLE public.adm_nota_debito        DROP CONSTRAINT fk_adm_nota_debito_factura_origen;
ALTER TABLE public.con_partida_factura    DROP CONSTRAINT fk_con_partida_factura_factura;
ALTER TABLE public.adm_recibo_banco_pendiente DROP CONSTRAINT adm_recibo_banco_pendiente_factura_id_fkey;
ALTER TABLE public.factura                DROP CONSTRAINT fk_factura_origen;

DROP TABLE public.factura_detalle;
DROP TABLE public.factura;
ALTER TABLE factura_detalle_new RENAME TO factura_detalle;
ALTER TABLE factura_new         RENAME TO factura;

ALTER TABLE public.factura_detalle
    ALTER COLUMN id SET NOT NULL, ALTER COLUMN company_id SET NOT NULL,
    ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY;
ALTER TABLE public.factura
    ALTER COLUMN id SET NOT NULL, ALTER COLUMN numrecibo SET NOT NULL,
    ALTER COLUMN company_id SET NOT NULL, ALTER COLUMN estado_id SET NOT NULL,
    ALTER COLUMN tipo_documento_fiscal_id SET NOT NULL,
    ALTER COLUMN estado_id SET DEFAULT 1, ALTER COLUMN tipo_documento_fiscal_id SET DEFAULT 1,
    ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY,
    ALTER COLUMN numrecibo ADD GENERATED ALWAYS AS IDENTITY;
COMMIT;

ALTER TABLE public.factura_detalle ADD CONSTRAINT factura_detalle_pkey PRIMARY KEY (id);
CREATE INDEX idx_factura_detalle_factura ON public.factura_detalle (factura_id, id);
CREATE INDEX ix_factura_detalle_company  ON public.factura_detalle (company_id);
ALTER TABLE public.factura ADD CONSTRAINT factura_pkey PRIMARY KEY (id);
ALTER TABLE public.factura ADD CONSTRAINT uq_factura_company_id UNIQUE (company_id, id);
CREATE INDEX ix_factura_company_recibo_cliente ON public.factura (company_id, numrecibo, clientecodigo);

SELECT setval(pg_get_serial_sequence('public.factura_detalle','id'), (SELECT max(id)+1000 FROM public.factura_detalle));
SELECT setval(pg_get_serial_sequence('public.factura','id'),         (SELECT max(id)+1000 FROM public.factura));
SELECT setval(pg_get_serial_sequence('public.factura','numrecibo'),  (SELECT max(numrecibo)+1000 FROM public.factura));

ALTER TABLE public.factura_detalle ADD CONSTRAINT fk_factura_detalle_company FOREIGN KEY (company_id) REFERENCES cfg_company(company_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_company FOREIGN KEY (company_id) REFERENCES cfg_company(company_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_estado FOREIGN KEY (estado_id) REFERENCES cfg_estado_documento_comercial(estado_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_tipo_doc FOREIGN KEY (tipo_documento_fiscal_id) REFERENCES cfg_tipo_documento_fiscal(tipo_documento_fiscal_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_categoria_servicio FOREIGN KEY (categoria_servicio_id) REFERENCES categoria_servicio(categoria_servicio_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_motivo_anulacion FOREIGN KEY (motivo_anulacion_id) REFERENCES cfg_motivo_anulacion(motivo_anulacion_id);
ALTER TABLE public.factura ADD CONSTRAINT fk_factura_origen FOREIGN KEY (factura_origen_id) REFERENCES factura(id);
ALTER TABLE public.adm_pago_aplicacion ADD CONSTRAINT adm_pago_aplicacion_factura_id_fkey FOREIGN KEY (factura_id) REFERENCES factura(id);
ALTER TABLE public.adm_pago_aplicacion ADD CONSTRAINT adm_pago_aplicacion_factura_detalle_id_fkey FOREIGN KEY (factura_detalle_id) REFERENCES factura_detalle(id);
ALTER TABLE public.cln_plan_pago_traslado ADD CONSTRAINT cln_plan_pago_traslado_factura_id_fkey FOREIGN KEY (factura_id) REFERENCES factura(id);
ALTER TABLE public.cln_plan_pago_traslado ADD CONSTRAINT cln_plan_pago_traslado_factura_detalle_id_fkey FOREIGN KEY (factura_detalle_id) REFERENCES factura_detalle(id);
ALTER TABLE public.adm_nota_credito ADD CONSTRAINT fk_adm_nota_credito_factura_origen FOREIGN KEY (factura_origen_id) REFERENCES factura(id);
ALTER TABLE public.adm_nota_debito ADD CONSTRAINT fk_adm_nota_debito_factura_origen FOREIGN KEY (factura_origen_id) REFERENCES factura(id);
ALTER TABLE public.con_partida_factura ADD CONSTRAINT fk_con_partida_factura_factura FOREIGN KEY (company_id, factura_id) REFERENCES factura(company_id, id);
ALTER TABLE public.adm_recibo_banco_pendiente ADD CONSTRAINT adm_recibo_banco_pendiente_factura_id_fkey FOREIGN KEY (factura_id) REFERENCES factura(id);

DROP TABLE IF EXISTS factura_estado_new;
RESET session_replication_role;
ANALYZE public.factura;
ANALYZE public.factura_detalle;

\echo '=== TOTALES ==='
SELECT (SELECT count(*) FROM public.factura WHERE company_id=2) facturas,
       (SELECT count(*) FROM public.factura_detalle WHERE company_id=2) lineas,
       (SELECT count(*) FROM public.adm_pago WHERE company_id=2) pagos,
       (SELECT count(*) FROM public.adm_pago_aplicacion WHERE company_id=2) aplicaciones;

\echo '=== estados ==='
SELECT estado, estado_id, count(*) FROM public.factura WHERE company_id=2 GROUP BY 1,2 ORDER BY 3 DESC;

\echo '=== CONTROL M4: saldo pendiente por linea vs saldo del cliente en el libro ==='
WITH origen AS (
    SELECT trim(t.cliente) c, round(sum(t.debitos)-sum(t.creditos),2) saldo
    FROM simafi_stg.transaccion_abonado t
    WHERE trim(COALESCE(t.cliente,'')) <> '' GROUP BY 1),
lineas AS (
    SELECT f.clientecodigo c, round(sum(d.montovalor_saldo),2) saldo
    FROM public.factura_detalle d
    JOIN public.factura f ON f.id=d.factura_id AND f.company_id=2
    WHERE d.company_id=2 GROUP BY 1)
SELECT count(*) clientes,
       count(*) FILTER (WHERE abs(GREATEST(o.saldo,0)-COALESCE(l.saldo,0)) < 0.005)  cuadran,
       count(*) FILTER (WHERE abs(GREATEST(o.saldo,0)-COALESCE(l.saldo,0)) >= 0.005) difieren,
       round(sum(GREATEST(o.saldo,0)),2) saldo_libro,
       round(sum(COALESCE(l.saldo,0)),2) saldo_lineas
FROM origen o LEFT JOIN lineas l ON l.c = o.c;
