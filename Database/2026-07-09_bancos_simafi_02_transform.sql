-- =============================================================================
-- Migración Bancos SIMAFI  ·  Script 02/4 : TRANSFORM + carga en ban_*
-- Landing fiel: ctacheques/saldobancos → ban_banco/ban_cuenta ;
--               maestroche → ban_movimiento ; detalleck → ban_movimiento(+_detalle)
-- INTEGRAR SIN DUPLICAR: reutiliza bancos (por nombre) y cuentas (por número)
--   ya existentes en company 2; crea sólo lo que falta (código namespaced SIM*).
-- Idempotente: borra sólo filas SIM*/origen_legacy y reinserta.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- Parámetros
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS tmp_bancos_params;
CREATE TEMP TABLE tmp_bancos_params AS
SELECT 2::bigint AS company_id, 'migracion'::text AS created_by, 'LPS'::text AS currency_code;
-- currency_code 'LPS' para consistencia con las cuentas existentes y ban_moneda (company 2).

-- Guard: abortar si el staging está vacío, para NO borrar una migración previa sin recargar.
DO $$
BEGIN
    IF (SELECT count(*) FROM public.stg_simafi_ctacheques) = 0
       OR (SELECT count(*) FROM public.stg_simafi_maestroche) = 0 THEN
        RAISE EXCEPTION 'Staging vacío: ejecute el 01_landing antes del 02_transform.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Limpieza idempotente (SOLO filas de esta migración; nunca lo existente)
-- ---------------------------------------------------------------------------
DELETE FROM public.ban_movimiento
 WHERE company_id = (SELECT company_id FROM tmp_bancos_params)
   AND origen_legacy IN ('maestroche','detalleck');   -- cascada a ban_movimiento_detalle
DELETE FROM public.ban_cuenta
 WHERE company_id = (SELECT company_id FROM tmp_bancos_params)
   AND code LIKE 'SIM%';
DELETE FROM public.ban_banco
 WHERE company_id = (SELECT company_id FROM tmp_bancos_params)
   AND code LIKE 'SIMB%';

-- ---------------------------------------------------------------------------
-- 2) BANCOS  (ban_banco): reutilizar existentes por nombre normalizado; crear nuevos
-- ---------------------------------------------------------------------------
-- 2a) canónicos referenciados por cuentas que migran (ctacheques reales + saldobancos)
DROP TABLE IF EXISTS tmp_banco_canon;
CREATE TEMP TABLE tmp_banco_canon AS
SELECT DISTINCT COALESCE(m.canonico, btrim(src.banco)) AS canonico
FROM (
    SELECT banco FROM public.stg_simafi_ctacheques WHERE btrim(contable) LIKE '111-02%'
    UNION ALL
    SELECT banco FROM public.stg_simafi_saldobancos
) src
LEFT JOIN public.stg_simafi_banco_map m ON btrim(m.nombre_libre) = btrim(src.banco)
WHERE btrim(COALESCE(src.banco,'')) <> '';

-- 2b) resolver a ban_banco_id existente por nombre normalizado (quita ' S. A.', colapsa espacios)
DROP TABLE IF EXISTS tmp_banco_res;
CREATE TEMP TABLE tmp_banco_res AS
SELECT c.canonico, b.ban_banco_id AS existing_id
FROM tmp_banco_canon c
LEFT JOIN LATERAL (
    SELECT bb.ban_banco_id FROM public.ban_banco bb
    WHERE bb.company_id = (SELECT company_id FROM tmp_bancos_params)
      AND upper(regexp_replace(regexp_replace(btrim(bb.nombre),'\s+',' ','g'),'\s+S\.?\s*A\.?\s*$','','g'))
        = upper(regexp_replace(regexp_replace(btrim(c.canonico),'\s+',' ','g'),'\s+S\.?\s*A\.?\s*$','','g'))
    ORDER BY bb.ban_banco_id LIMIT 1
) b ON true;

-- 2c) insertar canónicos nuevos (sin match) con código SIMB###
WITH nuevos AS (
    SELECT canonico, row_number() OVER (ORDER BY canonico) AS rn
    FROM tmp_banco_res WHERE existing_id IS NULL
)
INSERT INTO public.ban_banco (company_id, code, nombre, activo, created_by)
SELECT (SELECT company_id FROM tmp_bancos_params),
       'SIMB' || lpad(rn::text, 3, '0'), left(canonico, 60), true,
       (SELECT created_by FROM tmp_bancos_params)
FROM nuevos;

-- 2d) mapa final canónico -> ban_banco_id
DROP TABLE IF EXISTS tmp_banco_id;
CREATE TEMP TABLE tmp_banco_id AS
SELECT r.canonico,
       COALESCE(r.existing_id, bb.ban_banco_id) AS ban_banco_id
FROM tmp_banco_res r
LEFT JOIN public.ban_banco bb
       ON bb.company_id = (SELECT company_id FROM tmp_bancos_params)
      AND bb.nombre = left(r.canonico, 60)
      AND bb.code LIKE 'SIMB%';

-- ---------------------------------------------------------------------------
-- 3) CUENTAS  (ban_cuenta): reutilizar existentes por número; crear las que falten
-- ---------------------------------------------------------------------------
-- 3a) lista unificada de cuentas legacy (ctacheques reales 111-02 + saldobancos)
DROP TABLE IF EXISTS tmp_cuenta_src;
CREATE TEMP TABLE tmp_cuenta_src AS
SELECT 'SIMC' || btrim(numero)          AS code,
       btrim(numero)                    AS legacy_numero,
       'ctacheques'                     AS fuente,
       btrim(cuenta)                    AS numero_cuenta_raw,
       btrim(banco)                     AS banco_libre,
       COALESCE((SELECT canonico FROM public.stg_simafi_banco_map WHERE btrim(nombre_libre)=btrim(c.banco)), btrim(c.banco)) AS canonico,
       btrim(contable)                  AS contable,
       COALESCE(activa,0)               AS activa,
       COALESCE(saldobancoant,0)        AS saldo_inicial,
       0::numeric                       AS saldo_actual,
       COALESCE(ncheque,0)              AS proximo_cheque,
       COALESCE(ndebito,0)              AS proximo_nddb
FROM public.stg_simafi_ctacheques c
WHERE btrim(contable) LIKE '111-02%'
UNION ALL
SELECT 'SIMS' || btrim(numero),
       NULL, 'saldobancos', btrim(cuenta), btrim(banco),
       COALESCE((SELECT canonico FROM public.stg_simafi_banco_map WHERE btrim(nombre_libre)=btrim(s.banco)), btrim(s.banco)),
       NULL, COALESCE(activa,0), COALESCE(saldobancoant,0), COALESCE(saldoactual,0), 0, 0
FROM public.stg_simafi_saldobancos s;

-- 3b) resolver contra cuentas existentes por número + cross-walk contable
DROP TABLE IF EXISTS tmp_cuenta_res;
CREATE TEMP TABLE tmp_cuenta_res AS
SELECT s.*,
       ex.banco_cuenta_id AS existing_id,
       cw.account_id      AS cont_account_id
FROM tmp_cuenta_src s
LEFT JOIN public.ban_cuenta ex
       ON ex.company_id = (SELECT company_id FROM tmp_bancos_params)
      AND btrim(ex.numero_cuenta) = s.numero_cuenta_raw
LEFT JOIN public.con_plan_cuentas cw
       ON cw.company_id = (SELECT company_id FROM tmp_bancos_params)
      AND s.contable IS NOT NULL
      AND cw.code = replace(s.contable, '-', '');

-- 3c) insertar cuentas legacy nuevas (sin match existente); sufijo si número duplicado interno
WITH to_ins AS (
    SELECT r.*, count(*) OVER (PARTITION BY r.numero_cuenta_raw) AS raw_cnt
    FROM tmp_cuenta_res r
    WHERE r.existing_id IS NULL
)
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, banco_nombre, ban_banco_id, tipo, currency_code,
     numero_cuenta, cont_account_id, saldo_inicial, saldo_actual, estado, activo,
     proximo_cheque, proximo_nddb, created_by)
SELECT
    (SELECT company_id FROM tmp_bancos_params),
    ti.code,
    left('CTA ' || COALESCE(ti.legacy_numero, ti.code) || ' ' || COALESCE(ti.banco_libre,''), 150),
    left(ti.banco_libre, 150),
    bid.ban_banco_id,
    'CHEQUES',
    (SELECT currency_code FROM tmp_bancos_params),
    left(CASE WHEN ti.raw_cnt > 1
              THEN ti.numero_cuenta_raw || ' (' || ti.code || ')'
              ELSE ti.numero_cuenta_raw END, 50),
    ti.cont_account_id,
    ti.saldo_inicial,
    ti.saldo_actual,
    CASE WHEN ti.activa = 1 THEN 'ACTIVE' ELSE 'INACTIVE' END,
    (ti.activa = 1),
    ti.proximo_cheque,
    ti.proximo_nddb,
    (SELECT created_by FROM tmp_bancos_params)
FROM to_ins ti
LEFT JOIN tmp_banco_id bid ON bid.canonico = ti.canonico;

-- 3d) mapa final: cuenta legacy -> banco_cuenta_id (existente reutilizada o nueva SIM)
DROP TABLE IF EXISTS tmp_cuenta_id;
CREATE TEMP TABLE tmp_cuenta_id AS
SELECT r.code, r.legacy_numero, r.fuente, r.contable,
       COALESCE(r.existing_id, bc.banco_cuenta_id) AS banco_cuenta_id
FROM tmp_cuenta_res r
LEFT JOIN public.ban_cuenta bc
       ON bc.company_id = (SELECT company_id FROM tmp_bancos_params)
      AND bc.code = r.code;

-- 3e) mapas de resolución para movimientos
DROP TABLE IF EXISTS tmp_res_numero;
CREATE TEMP TABLE tmp_res_numero AS
SELECT legacy_numero, banco_cuenta_id
FROM tmp_cuenta_id
WHERE fuente='ctacheques' AND legacy_numero IS NOT NULL AND banco_cuenta_id IS NOT NULL;

DROP TABLE IF EXISTS tmp_res_contable;
CREATE TEMP TABLE tmp_res_contable AS
SELECT DISTINCT ON (contable) contable, banco_cuenta_id
FROM tmp_cuenta_id
WHERE fuente='ctacheques' AND contable LIKE '111-02%' AND banco_cuenta_id IS NOT NULL
ORDER BY contable, banco_cuenta_id;

-- ---------------------------------------------------------------------------
-- 4) MOVIMIENTOS desde maestroche → ban_movimiento (grano documento)
-- ---------------------------------------------------------------------------
INSERT INTO public.ban_movimiento
    (company_id, banco_cuenta_id, tipo, fecha_movimiento, currency_code,
     monto, mto_db, mto_cr, descripcion, documento, cod_bene, bene_origen,
     fecha_lib, estado, obcp, origen_legacy, created_by)
SELECT
    (SELECT company_id FROM tmp_bancos_params),
    rn.banco_cuenta_id,
    CASE
        WHEN upper(left(btrim(mc.docu),2)) = 'CK' THEN 'CHEQUE'
        WHEN upper(left(btrim(mc.docu),2)) = 'ND' THEN 'ND'
        WHEN upper(left(btrim(mc.docu),2)) = 'NC' THEN 'NC'
        WHEN upper(left(btrim(mc.docu),1)) = 'D'  THEN 'DEBITO'
        ELSE 'OTRO' END,
    mc.fecha,
    (SELECT currency_code FROM tmp_bancos_params),
    COALESCE(mc.debe,0) + COALESCE(mc.haber,0),
    COALESCE(mc.debe,0), COALESCE(mc.haber,0),
    left(btrim(mc.concepto), 300),
    left(btrim(mc.docu), 25),
    left(btrim(mc.beneficiar), 30),
    left(btrim(mc.beneficiar), 40),
    mc.fechaent,
    CASE WHEN upper(btrim(mc.borr)) = 'X' THEN 'VOID' ELSE 'POSTED' END,
    left(COALESCE(btrim(mc.entregado),''), 1),
    'maestroche',
    (SELECT created_by FROM tmp_bancos_params)
FROM public.stg_simafi_maestroche mc
JOIN tmp_res_numero rn ON rn.legacy_numero = btrim(mc.cuenta)
WHERE mc.fecha IS NOT NULL;

-- ---------------------------------------------------------------------------
-- 5) MOVIMIENTOS desde detalleck → ban_movimiento (cabecera sintética) + _detalle
--    UNA cabecera por (voucher, cuenta banco): evita inflar/mal-atribuir el monto
--    cuando un voucher toca 2+ cuentas (p.ej. traslado entre cuentas propias).
--    empre se guarda en cod_sucu para un enlace cabecera↔detalle robusto.
-- ---------------------------------------------------------------------------
-- 5a) cabeceras: una por (empre,docu,fecha,cuenta banco); monto = SÓLO esa cuenta.
--     rk=1 marca la cabecera "primaria" del voucher (recibe las líneas contrapartida).
DROP TABLE IF EXISTS tmp_dck_header;
CREATE TEMP TABLE tmp_dck_header AS
WITH bank_lines AS (
    SELECT d.empre, btrim(d.docu) AS docu, d.fecha, btrim(d.vou) AS vou,
           rc.banco_cuenta_id, COALESCE(d.debe,0) AS debe, COALESCE(d.haber,0) AS haber
    FROM public.stg_simafi_detalleck d
    JOIN tmp_res_contable rc ON rc.contable = btrim(d.cuenta)
    WHERE btrim(d.cuenta) LIKE '111-02%' AND d.fecha IS NOT NULL
),
agg AS (
    SELECT empre, docu, fecha, banco_cuenta_id,
           max(vou) AS vou, SUM(debe) AS debe, SUM(haber) AS haber
    FROM bank_lines
    GROUP BY empre, docu, fecha, banco_cuenta_id
)
SELECT empre, docu, fecha, banco_cuenta_id, vou, debe, haber,
       row_number() OVER (PARTITION BY empre, docu, fecha
                          ORDER BY (debe+haber) DESC, banco_cuenta_id) AS rk
FROM agg;

-- 5b) insertar cabeceras (monto por cuenta; empre → cod_sucu para trazabilidad/enlace)
INSERT INTO public.ban_movimiento
    (company_id, banco_cuenta_id, tipo, fecha_movimiento, currency_code,
     monto, mto_db, mto_cr, documento, c_refer, cod_sucu, estado, origen_legacy, created_by)
SELECT
    (SELECT company_id FROM tmp_bancos_params),
    h.banco_cuenta_id, 'VOUCHER', h.fecha,
    (SELECT currency_code FROM tmp_bancos_params),
    h.debe + h.haber, h.debe, h.haber,
    left(h.docu, 25), left(COALESCE(h.vou,''), 35), left(h.empre, 5),
    'POSTED', 'detalleck',
    (SELECT created_by FROM tmp_bancos_params)
FROM tmp_dck_header h;

-- 5c) mapa (empre,docu,fecha,cuenta) → movimiento_id recién insertado
DROP TABLE IF EXISTS tmp_dck_movid;
CREATE TEMP TABLE tmp_dck_movid AS
SELECT h.empre, h.docu, h.fecha, h.banco_cuenta_id, h.rk, m.movimiento_id
FROM tmp_dck_header h
JOIN public.ban_movimiento m
  ON m.company_id = (SELECT company_id FROM tmp_bancos_params)
 AND m.origen_legacy = 'detalleck'
 AND m.cod_sucu = left(h.empre, 5)
 AND m.documento = left(h.docu, 25)
 AND m.fecha_movimiento = h.fecha
 AND m.banco_cuenta_id = h.banco_cuenta_id;

-- 5d) detalle: cada línea del voucher a la cabecera correcta
--     línea banco (111-02) → cabecera de SU cuenta ; contrapartida → cabecera primaria (rk=1)
INSERT INTO public.ban_movimiento_detalle
    (company_id, movimiento_id, linea_num, cod_cta, mto_db, mto_cr, monto, dh,
     descripcion, fecha, cod_usua, origen)
SELECT
    (SELECT company_id FROM tmp_bancos_params),
    tgt.movimiento_id,
    row_number() OVER (PARTITION BY tgt.movimiento_id
                       ORDER BY d.debe DESC, d.haber DESC, btrim(d.cuenta)),
    left(btrim(d.cuenta), 30),
    COALESCE(d.debe,0), COALESCE(d.haber,0),
    COALESCE(d.debe,0)+COALESCE(d.haber,0),
    CASE WHEN COALESCE(d.debe,0) > 0 THEN 1 ELSE 2 END,
    left(btrim(d.concepto), 60),
    d.fecha,
    left(btrim(d.usuario), 30),
    'detalleck'
FROM public.stg_simafi_detalleck d
JOIN LATERAL (
    SELECT mv.movimiento_id
    FROM tmp_dck_movid mv
    WHERE mv.empre = d.empre AND mv.docu = btrim(d.docu) AND mv.fecha = d.fecha
      AND (
          (btrim(d.cuenta) LIKE '111-02%'
             AND mv.banco_cuenta_id = (SELECT rc.banco_cuenta_id
                                         FROM tmp_res_contable rc
                                        WHERE rc.contable = btrim(d.cuenta)))
          OR
          (btrim(d.cuenta) NOT LIKE '111-02%' AND mv.rk = 1)
      )
    LIMIT 1
) tgt ON true
WHERE d.fecha IS NOT NULL;

-- ---------------------------------------------------------------------------
-- 6) Vista de pendientes: TODO lo del origen que NO se cargó, con su motivo.
--    (para que siempre cuadre origen = cargado + pendientes)
-- ---------------------------------------------------------------------------
DROP VIEW IF EXISTS public.vw_stg_bancos_pendientes;
CREATE VIEW public.vw_stg_bancos_pendientes AS
-- detalleck: vouchers sin ninguna línea de cuenta banco (111-02)
SELECT 'detalleck_sin_cuenta_banco'::text AS motivo, d.empre, btrim(d.docu) AS docu,
       d.fecha::text AS ref, count(*) AS lineas
FROM public.stg_simafi_detalleck d
GROUP BY d.empre, btrim(d.docu), d.fecha
HAVING sum(CASE WHEN btrim(d.cuenta) LIKE '111-02%' THEN 1 ELSE 0 END) = 0
UNION ALL
-- detalleck: líneas con fecha NULL (descartadas por NOT NULL)
SELECT 'detalleck_fecha_null', d.empre, btrim(d.docu), NULL, count(*)
FROM public.stg_simafi_detalleck d
WHERE d.fecha IS NULL
GROUP BY d.empre, btrim(d.docu)
UNION ALL
-- maestroche: cuenta vacía/NULL o inexistente en ctacheques
SELECT 'maestroche_cuenta_sin_match', NULL,
       COALESCE(NULLIF(btrim(mc.cuenta), ''), '(vacío/NULL)'), NULL, count(*)
FROM public.stg_simafi_maestroche mc
WHERE NOT EXISTS (SELECT 1 FROM public.stg_simafi_ctacheques c
                   WHERE btrim(c.numero) = btrim(mc.cuenta))
GROUP BY COALESCE(NULLIF(btrim(mc.cuenta), ''), '(vacío/NULL)')
UNION ALL
-- maestroche: cuenta existe pero es transitoria (contable 115/211, excluida de ban_cuenta)
SELECT 'maestroche_cuenta_transitoria', NULL, btrim(mc.cuenta), NULL, count(*)
FROM public.stg_simafi_maestroche mc
WHERE EXISTS (SELECT 1 FROM public.stg_simafi_ctacheques c
               WHERE btrim(c.numero) = btrim(mc.cuenta)
                 AND btrim(c.contable) NOT LIKE '111-02%')
GROUP BY btrim(mc.cuenta)
UNION ALL
-- maestroche: fecha NULL (descartada por NOT NULL) con cuenta válida
SELECT 'maestroche_fecha_null', NULL, btrim(mc.cuenta), NULL, count(*)
FROM public.stg_simafi_maestroche mc
WHERE mc.fecha IS NULL
  AND EXISTS (SELECT 1 FROM public.stg_simafi_ctacheques c
               WHERE btrim(c.numero) = btrim(mc.cuenta)
                 AND btrim(c.contable) LIKE '111-02%')
GROUP BY btrim(mc.cuenta)
UNION ALL
-- saldobancos: saldo real NO aplicado porque la cuenta reutilizó una ya existente
--   (decisión "integrar sin duplicar" = no pisar lo existente; queda auditable aquí)
SELECT 'saldobancos_saldo_no_aplicado_cuenta_existente', NULL, btrim(s.cuenta),
       round(s.saldoactual, 2)::text, 1
FROM public.stg_simafi_saldobancos s
WHERE EXISTS (SELECT 1 FROM public.ban_cuenta ex
               WHERE ex.company_id = 2
                 AND btrim(ex.numero_cuenta) = btrim(s.cuenta)
                 AND ex.code NOT LIKE 'SIM%');

COMMIT;
