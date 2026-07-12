-- =============================================================================
-- Migración Proveedores SIMAFI · Script 02/3 : TRANSFORM (staging -> prv_*)
--
-- Decisiones del usuario (2026-07-10), ver el doc de mapeo:
--   D2  catálogo completo (606); compromisos solo 2025-01-01..2026-12-31
--   D3  tipo `Sin clasificar` nuevo, asignado a los 606
--   D4  los 236 sin cuenta contable válida entran con cuenta_contable = ''
--   D5  `0519` se deduplica; en `0322` el orden alfabético decide: COMERCIAL
--       INDIRA conserva `0322`, GENERAL DE REPUESTOS (GERESA) -> `0322B`
--   D6  monto de cabecera = SUM(valorp)  [= SUM(debe) sobre líneas presupuestarias]
--   D7  las órdenes sin proveedor identificable cuelgan de `SINPROV`, con la
--       categoría del beneficiario anotada al inicio del concepto
--   D8  concepto ampliado a varchar(500) en el script 00
--   D9  las 4,297 órdenes son históricas -> status_transacc = TRUE (procesadas),
--       de modo que la pantalla de Órdenes de Pago Directo no las liste como
--       pendientes (el filtro por defecto es `status_transacc != true`)
--
-- Idempotente: borra lo suyo por lista explícita de códigos / números de orden
-- y reinserta. NO toca los proveedores capturados en el portal (001001, PRVGEN)
-- ni las 4 órdenes de prueba (numero_orden 1..4; los legacy van de 29,169 a 333,420).
-- Transaccional.
-- =============================================================================
BEGIN;

DROP TABLE IF EXISTS tmp_prov_params;
CREATE TEMP TABLE tmp_prov_params AS
SELECT 2::int AS company_id, 'migracion'::text AS usuario, 'SINPROV'::text AS cod_generico;

-- Guard: el staging debe estar cargado (evita borrar y no reinsertar)
DO $$
BEGIN
    IF (SELECT count(*) FROM public.stg_simafi_proveedor) = 0
       OR (SELECT count(*) FROM public.stg_simafi_ordenesp) = 0 THEN
        RAISE EXCEPTION 'Staging vacío: ejecute el 01_landing antes del 02_transform.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Tipo de proveedor `Sin clasificar` (D3)
--    El origen trae `tipoprov` NULL en las 606 filas. cod_tipoproveedor es
--    NOT NULL y no tiene default asociado -> se asigna max+1 a mano.
-- ---------------------------------------------------------------------------
INSERT INTO public.prv_tipoproveedor (cod_tipoproveedor, nombre, observaciones)
SELECT COALESCE((SELECT max(cod_tipoproveedor) FROM public.prv_tipoproveedor), 0) + 1,
       'Sin clasificar',
       'Proveedores migrados de SIMAFI. El origen (bdsimafi.proveedor) no trae tipo de proveedor.'
WHERE NOT EXISTS (
    SELECT 1 FROM public.prv_tipoproveedor WHERE lower(btrim(nombre)) = 'sin clasificar');

-- Mantener la secuencia por delante de los ids insertados a mano
SELECT setval('public.prv_tipoproveedor_cod_tipoproveedor_seq',
              (SELECT max(cod_tipoproveedor) FROM public.prv_tipoproveedor), true);

DROP TABLE IF EXISTS tmp_tipo;
CREATE TEMP TABLE tmp_tipo AS
SELECT cod_tipoproveedor::smallint AS cod_tipoproveedor
FROM public.prv_tipoproveedor WHERE lower(btrim(nombre)) = 'sin clasificar';

-- ---------------------------------------------------------------------------
-- 2) Catálogo canónico: TRIM, dedupe y resolución de colisiones (D5)
--    · Grupo con el MISMO nombre  -> duplicado real: se conserva la fila más
--      completa (RTN más largo; `0519` trae 05061988001731 vs 0506199001731).
--    · Grupo con nombres DISTINTOS -> colisión: orden alfabético por nombre;
--      el primero conserva el código, los siguientes reciben sufijo B, C, ...
--      (`0322`: COMERCIAL INDIRA < GENERAL DE REPUESTOS -> GERESA queda `0322B`)
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS tmp_prov_base;
CREATE TEMP TABLE tmp_prov_base AS
SELECT btrim(codigo)                                   AS cod_trim,
       btrim(coalesce(proveedor,''))                   AS nombre,
       btrim(coalesce(direccion,''))                   AS direccion,
       btrim(coalesce(rtn,''))                         AS rtn,
       btrim(coalesce(telefono,''))                    AS telefono,
       fecha,
       btrim(coalesce(razonsocial,''))                 AS razonsocial,
       btrim(coalesce(email,''))                       AS email,
       btrim(coalesce(fax,''))                         AS fax,
       btrim(coalesce(paginaweb,''))                   AS paginaweb,
       btrim(coalesce(percontacto,''))                 AS percontacto,
       nullif(regexp_replace(coalesce(contable,''), '\D', '', 'g'), '') AS contable_norm
FROM public.stg_simafi_proveedor
WHERE btrim(coalesce(codigo,'')) <> '';

-- Dedupe dentro de grupos de mismo (cod_trim, nombre): la fila más completa
DROP TABLE IF EXISTS tmp_prov_dedup;
CREATE TEMP TABLE tmp_prov_dedup AS
SELECT DISTINCT ON (cod_trim, nombre) *
FROM tmp_prov_base
ORDER BY cod_trim, nombre,
         length(rtn) DESC, length(direccion) DESC, length(razonsocial) DESC;

-- Resolver colisiones de código entre nombres distintos
DROP TABLE IF EXISTS tmp_prov_canon;
CREATE TEMP TABLE tmp_prov_canon AS
WITH rk AS (
    SELECT d.*, row_number() OVER (PARTITION BY cod_trim ORDER BY nombre) AS rn
    FROM tmp_prov_dedup d
)
SELECT CASE WHEN rn = 1 THEN cod_trim
            ELSE cod_trim || chr(64 + rn::int)   -- 2 -> 'B', 3 -> 'C', ...
       END AS cod_proveedor,
       cod_trim AS cod_legacy,
       rn,
       nombre, direccion, rtn, telefono, fecha, razonsocial, email, fax,
       paginaweb, percontacto, contable_norm
FROM rk;

-- Cuenta contable: solo si existe en el plan; si no, cadena vacía (D4)
DROP TABLE IF EXISTS tmp_prov_final;
CREATE TEMP TABLE tmp_prov_final AS
SELECT c.*,
       COALESCE(p.code, '') AS cuenta_contable,
       (c.contable_norm IS NOT NULL AND p.code IS NULL) AS contable_huerfana
FROM tmp_prov_canon c
LEFT JOIN public.con_plan_cuentas p
       ON p.code = c.contable_norm
      AND p.company_id = (SELECT company_id FROM tmp_prov_params);

-- ---------------------------------------------------------------------------
-- 3) Carga de prv_proveedores (idempotente por lista explícita de códigos)
-- ---------------------------------------------------------------------------
DELETE FROM public.prv_proveedores
WHERE company_id = (SELECT company_id FROM tmp_prov_params)
  AND (cod_proveedor IN (SELECT cod_proveedor FROM tmp_prov_final)
       OR cod_proveedor = (SELECT cod_generico FROM tmp_prov_params));

INSERT INTO public.prv_proveedores
    (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
     fecha_creacion, fecha_modificacion, status, cuenta_bancaria,
     razon_social, rtn, telefono, pagina_web, fax, email, nombre_contacto,
     company_id, usuario_creo, usuario_modifica, ultimo_correlativo_compromiso)
SELECT f.cod_proveedor,
       (SELECT cod_tipoproveedor FROM tmp_tipo),
       left(f.nombre, 150),
       f.cuenta_contable,
       left(f.direccion, 1000),
       COALESCE(f.fecha::timestamp, now()),
       NULL,
       TRUE,
       NULL,
       nullif(left(f.razonsocial, 150), ''),
       nullif(left(f.rtn, 20), ''),
       nullif(left(f.telefono, 20), ''),
       nullif(left(f.paginaweb, 150), ''),
       nullif(left(f.fax, 50), ''),
       nullif(left(f.email, 150), ''),
       nullif(left(f.percontacto, 150), ''),
       (SELECT company_id FROM tmp_prov_params),
       (SELECT usuario FROM tmp_prov_params),
       NULL,
       0
FROM tmp_prov_final f;

-- Proveedor genérico para las órdenes cuyo beneficiario no es un proveedor (D7)
INSERT INTO public.prv_proveedores
    (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
     fecha_creacion, status, company_id, usuario_creo, ultimo_correlativo_compromiso)
SELECT (SELECT cod_generico FROM tmp_prov_params),
       (SELECT cod_tipoproveedor FROM tmp_tipo),
       'Sin proveedor',
       '',
       'Beneficiarios de órdenes de pago que no son proveedores (bancos, SAR, IHSS, RAP, personas naturales).',
       now(), TRUE,
       (SELECT company_id FROM tmp_prov_params),
       (SELECT usuario FROM tmp_prov_params),
       0;

-- ---------------------------------------------------------------------------
-- 4) Enlace compromiso -> proveedor
--    Solo sirven las claves ÚNICAS. La cuenta contable NO identifica al
--    proveedor (21101010948 la comparten 8), por eso se exige n = 1.
--    Prioridad: NOMBRE único (identidad directa del beneficiario), luego
--    cuenta única (la CxP puede compartirse), luego SINPROV. Solo 3 órdenes
--    tienen ambas claves apuntando a proveedores distintos.
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS tmp_map_cuenta;
CREATE TEMP TABLE tmp_map_cuenta AS
SELECT contable_norm, min(cod_proveedor) AS cod_proveedor
FROM tmp_prov_final WHERE contable_norm IS NOT NULL
GROUP BY contable_norm HAVING count(*) = 1;

DROP TABLE IF EXISTS tmp_map_nombre;
CREATE TEMP TABLE tmp_map_nombre AS
SELECT upper(nombre) AS nombre_up, min(cod_proveedor) AS cod_proveedor
FROM tmp_prov_final
GROUP BY upper(nombre) HAVING count(*) = 1;

-- Líneas utilizables: descarta las 120 sin `ordenp` (no pueden agruparse)
DROP TABLE IF EXISTS tmp_ord_linea;
CREATE TEMP TABLE tmp_ord_linea AS
SELECT btrim(o.ordenp)::int                                        AS numero_orden,
       o.fecha,
       COALESCE(o.valorp, 0)                                       AS valorp,
       btrim(coalesce(o.beneficiar,''))                            AS beneficiar,
       nullif(regexp_replace(coalesce(o.cuentac,''),  '\D','','g'), '') AS cuentac_norm,
       nullif(regexp_replace(coalesce(o.contable,''), '\D','','g'), '') AS gasto_norm,
       btrim(coalesce(o.codproy,''))                               AS codproy,
       btrim(coalesce(o.concepto,''))                              AS concepto,
       btrim(coalesce(o.codigo,''))                                AS cod_presupuestario,
       btrim(coalesce(o.codpro,''))                                AS programa,
       btrim(coalesce(o.codact,''))                                AS actividad,
       btrim(coalesce(o.renglon,''))                               AS renglon
FROM public.stg_simafi_ordenesp o
WHERE btrim(coalesce(o.ordenp,'')) <> ''
  AND btrim(o.ordenp) ~ '^[0-9]+$';

-- Cuenta representativa de la orden: la MÁS FRECUENTE entre sus líneas.
-- 470 órdenes tienen más de un `cuentac`; elegir con max() sería arbitrario.
DROP TABLE IF EXISTS tmp_ord_cuenta;
CREATE TEMP TABLE tmp_ord_cuenta AS
SELECT DISTINCT ON (numero_orden) numero_orden, cuentac_norm
FROM (SELECT numero_orden, cuentac_norm, count(*) AS n
      FROM tmp_ord_linea WHERE cuentac_norm IS NOT NULL
      GROUP BY 1,2) t
ORDER BY numero_orden, n DESC, cuentac_norm;

-- Enlace por cuenta usando CUALQUIER línea de la orden, no solo la representativa
-- (recupera 48 órdenes frente a mirar una sola). Si dos líneas apuntan a
-- proveedores distintos, la orden queda ambigua y cae al enlace por nombre.
DROP TABLE IF EXISTS tmp_enl_cuenta;
CREATE TEMP TABLE tmp_enl_cuenta AS
SELECT l.numero_orden, min(mc.cod_proveedor) AS cod_proveedor
FROM tmp_ord_linea l
JOIN tmp_map_cuenta mc ON mc.contable_norm = l.cuentac_norm
GROUP BY l.numero_orden
HAVING count(DISTINCT mc.cod_proveedor) = 1;

-- Cabecera por orden
DROP TABLE IF EXISTS tmp_ord_hdr;
CREATE TEMP TABLE tmp_ord_hdr AS
WITH agg AS (
    SELECT l.numero_orden,
           min(l.fecha)                                 AS fecha,
           sum(l.valorp)                                AS monto,
           max(nullif(l.codproy,''))                    AS cod_proyecto,
           (array_agg(l.beneficiar ORDER BY length(l.beneficiar) DESC))[1] AS beneficiar,
           (array_agg(l.concepto   ORDER BY length(l.concepto)   DESC))[1] AS concepto
    FROM tmp_ord_linea l
    GROUP BY l.numero_orden
),
enl AS (
    SELECT a.*,
           oc.cuentac_norm                              AS cuenta_contable,
           COALESCE(mn.cod_proveedor, ec.cod_proveedor) AS cod_proveedor_real
    FROM agg a
    LEFT JOIN tmp_ord_cuenta oc ON oc.numero_orden = a.numero_orden
    LEFT JOIN tmp_enl_cuenta ec ON ec.numero_orden = a.numero_orden
    LEFT JOIN tmp_map_nombre mn ON mn.nombre_up     = upper(a.beneficiar)
),
cat AS (
    SELECT e.*,
           CASE
             WHEN e.cod_proveedor_real IS NOT NULL THEN NULL
             WHEN e.beneficiar ~* '^banco|davivienda|ficohsa|lafise|atlantida|cuscatl|occidente|promerica|banpais|(^| )bac( |$)|hsbc'
                  THEN 'BANCO'
             WHEN e.beneficiar ~* 'administracion de rentas|(^| )sar( |$)'         THEN 'SAR'
             WHEN e.beneficiar ~* 'seguridad social|(^| )ihss( |$)'                THEN 'IHSS'
             WHEN e.beneficiar ~* 'aportaciones privadas|(^| )rap( |$)'            THEN 'RAP'
             WHEN e.beneficiar <> '' AND e.beneficiar !~* 's\.? ?a\.?( |$)|s\.? de r\.? ?l|c\.? ?v\.?|cia|compa|empresa|inversion|comercial|instituto|municipalidad|corporacion|distribuidora|ferreteria|suplidora|servicios|asociacion|colegio|banco|fundacion'
                  THEN 'PERSONA NATURAL'
             ELSE 'OTRO'
           END AS categoria
    FROM enl e
)
SELECT c.numero_orden,
       c.fecha::timestamp                              AS fecha,
       c.monto,
       left(CASE WHEN c.categoria IS NULL THEN c.concepto
                 ELSE '[' || c.categoria || '] ' || c.concepto END, 500) AS concepto,
       COALESCE(c.cod_proveedor_real,
                (SELECT cod_generico FROM tmp_prov_params))              AS cod_proveedor,
       1                                               AS flag_proveedor,
       left(COALESCE(c.cuenta_contable, ''), 20)       AS cuenta_contable,
       left(COALESCE(c.cod_proyecto, ''), 20)          AS cod_proyecto,
       left(COALESCE(p.rtn, ''), 20)                   AS rtn,
       left(c.beneficiar, 100)                         AS pagar_a,
       TRUE                                            AS status_transacc,   -- históricas = procesadas (D9)
       left(c.beneficiar, 150)                         AS nombre_proveedor,
       FALSE                                           AS anulado,
       c.categoria,
       (c.cod_proveedor_real IS NOT NULL)              AS enlazada
FROM cat c
LEFT JOIN public.prv_proveedores p
       ON p.cod_proveedor = c.cod_proveedor_real
      AND p.company_id = (SELECT company_id FROM tmp_prov_params);

-- Correlativo por proveedor (la app lo continúa desde ultimo_correlativo_compromiso)
DROP TABLE IF EXISTS tmp_ord_hdr_num;
CREATE TEMP TABLE tmp_ord_hdr_num AS
SELECT h.*,
       row_number() OVER (PARTITION BY h.cod_proveedor ORDER BY h.numero_orden)::int AS correlativo_proveedor
FROM tmp_ord_hdr h;

-- ---------------------------------------------------------------------------
-- 5) Carga de compromisos (idempotente por número de orden)
-- ---------------------------------------------------------------------------
DELETE FROM public.prv_compromiso_dtl
WHERE company_id = (SELECT company_id FROM tmp_prov_params)
  AND numero_orden IN (SELECT numero_orden FROM tmp_ord_hdr_num);
DELETE FROM public.prv_compromiso_hdr
WHERE company_id = (SELECT company_id FROM tmp_prov_params)
  AND numero_orden IN (SELECT numero_orden FROM tmp_ord_hdr_num);

INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, correlativo_proveedor, fecha, monto, concepto, cod_proveedor,
     flag_proveedor, cuenta_contable, cod_proyecto, rtn, pagar_a, status_transacc,
     nombre_proveedor, anulado)
SELECT (SELECT company_id FROM tmp_prov_params),
       numero_orden, correlativo_proveedor, fecha, monto, concepto, cod_proveedor,
       flag_proveedor, cuenta_contable, cod_proyecto, nullif(rtn,''), pagar_a, status_transacc,
       nombre_proveedor, anulado
FROM tmp_ord_hdr_num;

-- Detalle: SOLO las líneas presupuestarias (las que traen renglón y `valorp`).
-- Las demás líneas de `ordenesp` son asientos de pago/retención contra la CxP.
INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad, objeto_gasto,
     cuenta_gasto, descripcion, monto, conceptodtl)
SELECT (SELECT company_id FROM tmp_prov_params),
       l.numero_orden,
       left(l.cod_presupuestario, 20),
       left(l.programa, 2),
       left(l.actividad, 2),
       left(l.renglon || COALESCE(' - ' || btrim(r.desrenglon), ''), 100),
       left(COALESCE(l.gasto_norm, ''), 20),
       left(l.concepto, 150),
       l.valorp,
       left(l.concepto, 100)
FROM tmp_ord_linea l
LEFT JOIN public.stg_simafi_renglon r ON btrim(r.codrenglon) = l.renglon
WHERE l.renglon <> ''
  AND l.numero_orden IN (SELECT numero_orden FROM tmp_ord_hdr_num);

-- ---------------------------------------------------------------------------
-- 6) Backfill del correlativo por proveedor
--    Sin esto, la primera orden creada desde la UI reusaría un correlativo
--    existente (OrdenesPagoDirectoService hace COALESCE(ultimo,0)+1).
-- ---------------------------------------------------------------------------
UPDATE public.prv_proveedores p
SET ultimo_correlativo_compromiso = x.max_corr
FROM (SELECT cod_proveedor, max(correlativo_proveedor) AS max_corr
      FROM tmp_ord_hdr_num GROUP BY cod_proveedor) x
WHERE p.cod_proveedor = x.cod_proveedor
  AND p.company_id = (SELECT company_id FROM tmp_prov_params);

-- ---------------------------------------------------------------------------
-- 7) Vistas de pendientes (lo que NO se cargó, y por qué)
-- ---------------------------------------------------------------------------
DROP VIEW IF EXISTS public.vw_stg_proveedores_pendientes;
CREATE VIEW public.vw_stg_proveedores_pendientes AS
SELECT btrim(s.codigo) AS cod_legacy,
       btrim(coalesce(s.proveedor,'')) AS nombre,
       btrim(coalesce(s.contable,''))  AS contable_legacy,
       CASE
         WHEN btrim(coalesce(s.contable,'')) = '' THEN 'sin cuenta contable en el origen'
         WHEN NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas p
                          WHERE p.company_id = 2
                            AND p.code = nullif(regexp_replace(s.contable,'\D','','g'),''))
              THEN 'cuenta contable inexistente en con_plan_cuentas'
         ELSE 'ok'
       END AS motivo
FROM public.stg_simafi_proveedor s
WHERE btrim(coalesce(s.codigo,'')) <> ''
  AND (btrim(coalesce(s.contable,'')) = ''
       OR NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas p
                      WHERE p.company_id = 2
                        AND p.code = nullif(regexp_replace(s.contable,'\D','','g'),'')));

DROP VIEW IF EXISTS public.vw_stg_compromisos_pendientes;
CREATE VIEW public.vw_stg_compromisos_pendientes AS
SELECT btrim(coalesce(o.ordenp,'')) AS ordenp,
       o.fecha,
       btrim(coalesce(o.beneficiar,'')) AS beneficiar,
       'línea sin número de orden (no agrupable en cabecera)'::text AS motivo
FROM public.stg_simafi_ordenesp o
WHERE btrim(coalesce(o.ordenp,'')) = '' OR btrim(coalesce(o.ordenp,'')) !~ '^[0-9]+$';

COMMIT;
