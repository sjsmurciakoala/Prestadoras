-- =============================================================================
-- Integración Contable ↔ Comercial — Fase 6: con_saldo_cuenta oficial
--                                            + balance de comprobación híbrido
-- Fecha: 2026-07-04
-- Plan: docs/plans/2026-07-02-plan-integracion-contable-comercial.md §5 Fase 6
--       (D1: solo sp_con_postear_poliza / sp_con_revertir_poliza — vía
--        sp_con_actualizar_saldos_por_poliza — escriben con_saldo_cuenta en
--        operación normal. La reconstrucción de este script NO es un tercer
--        escritor: es un rebuild desde el libro (con_partida_dtl) para
--        ventanas de mantenimiento/remigración.)
--
-- Contenido:
--   1. con_saldo_cuenta_backup_hist — respaldo histórico de cada rebuild
--      (misma forma que dejó 20260310_reconciliacion_con_saldo_cuenta.sql
--      donde ese script corrió; aquí se crea idempotente con forma explícita)
--      + índice ix_con_partida_hdr_company_status_period (soporta la
--      agregación del libro del rebuild/verificador y el reporte híbrido).
--   1b. fn_con_saldo_libro(company, period?, cuenta?) — agregación CANÓNICA
--      del libro (pólizas POSTED por período/cuenta). Fuente única consumida
--      por el rebuild y el verificador: por diseño ambos DEBEN calcular
--      idéntico (reconstruir ⇒ 0 divergencias).
--   2. sp_con_reconstruir_saldo_cuenta(company, user) — reconstrucción TOTAL
--      del caché (bucket mes=13/tipo_transaccion=0, el único que escribe el
--      motor) desde fn_con_saldo_libro. Multitenant (solo la empresa pedida),
--      idempotente (re-correr produce el mismo estado), respalda antes de
--      borrar y preserva `presupuesto`. Corrige la inconsistencia acumulada
--      por remigraciones+cierres SIMAFI.
--   3. fn_con_verificar_saldo_cuenta(company, period?, cuenta?) — reconciliación
--      caché vs fn_con_saldo_libro; devuelve las divergencias por período/
--      cuenta (SOLO_CACHE / SOLO_LIBRO / MONTOS / CONTEOS) más el diagnóstico
--      FECHA_FUERA_PERIODO (pólizas POSTED cuya poliza_date cae fuera del
--      rango de su período — rompen la equivalencia caché=vivo del reporte
--      híbrido; el motor lo impide al postear, así que >0 = data legacy a
--      corregir). Consumida por el endpoint api/contabilidad/saldos y la
--      pantalla de Períodos.
--   4. rep_balance_comprobacion v2 (misma firma y columnas — el ESF la llama
--      dos veces): períodos CERRADOS (status_id=2) leen del caché
--      con_saldo_cuenta; abiertos/precierre y rangos que parten un período
--      siguen calculando en vivo desde con_partida_dtl.
--   5. Reconstrucción automática del caché de TODAS las empresas con pólizas
--      POSTED al aplicar este script: desde la sección 4 los períodos
--      cerrados REPORTAN desde el caché, así que no puede existir ventana
--      con caché divergente entre aplicar el DDL y el primer rebuild manual.
--
-- REGLA OPERATIVA (documentada aquí a pedido del plan §5 F6.4):
--   Cualquier remigración SIMAFI futura (re-carga de stg_simafi_* /
--   re-posteo masivo / cierres anuales re-corridos) DEBE terminar ejecutando
--       SELECT * FROM public.sp_con_reconstruir_saldo_cuenta(<company_id>, '<usuario>');
--   y verificando 0 filas en
--       SELECT * FROM public.fn_con_verificar_saldo_cuenta(<company_id>);
--   Desde esta fase los períodos cerrados REPORTAN desde el caché: un caché
--   divergente produce balances de comprobación/ESF incorrectos.
--
-- Prerequisitos: motor único (20260122 + hotfixes 20260310), scripts F1–F5.
-- Idempotente. Producción: aplicar SOLO en ventana de deploy acordada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Respaldo histórico de rebuilds
-- -----------------------------------------------------------------------------
-- Donde corrió 20260310_reconciliacion_con_saldo_cuenta.sql la tabla ya existe
-- con esta misma forma (CREATE TABLE ... AS SELECT now(), ''::text, s.* WHERE false).
CREATE TABLE IF NOT EXISTS public.con_saldo_cuenta_backup_hist (
    backup_at          timestamptz,
    backup_tag         text,
    saldo_id           bigint,
    company_id         bigint,
    periodo_id         bigint,
    codigo_cuenta      varchar(30),
    mes                smallint,
    tipo_transaccion   smallint,
    debitos            numeric(28,2),
    creditos           numeric(28,2),
    cantidad_debitos   integer,
    cantidad_creditos  integer,
    presupuesto        numeric(28,2),
    created_at         timestamptz,
    updated_at         timestamptz
);

CREATE INDEX IF NOT EXISTS ix_con_saldo_cuenta_backup_hist_tag
    ON public.con_saldo_cuenta_backup_hist (company_id, backup_tag);

COMMENT ON TABLE public.con_saldo_cuenta_backup_hist IS
'Respaldo histórico de con_saldo_cuenta previo a cada reconstrucción (sp_con_reconstruir_saldo_cuenta, F6). backup_tag identifica la corrida.';

-- Soporta la agregación del libro (fn_con_saldo_libro: company+status+period)
-- del rebuild, el verificador y el anti-join del reporte híbrido. Sin él,
-- cada verificación hace seq scan de con_partida_hdr completo.
CREATE INDEX IF NOT EXISTS ix_con_partida_hdr_company_status_period
    ON public.con_partida_hdr (company_id, status, period_id);

-- -----------------------------------------------------------------------------
-- 1b. fn_con_saldo_libro — agregación canónica del libro por período/cuenta
-- -----------------------------------------------------------------------------
-- ÚNICA definición de "lo que el caché debería contener": pólizas POSTED
-- (status=1) con período, agregadas por (period_id, código de cuenta) con la
-- MISMA semántica que sp_con_actualizar_saldos_por_poliza (motor único):
-- suma de débitos/créditos + conteo de líneas con monto > 0. La consumen el
-- rebuild (sección 2) y el verificador (sección 3) — mantenerla como fuente
-- única es lo que garantiza que reconstruir deje 0 divergencias.
CREATE OR REPLACE FUNCTION public.fn_con_saldo_libro(
    p_company_id bigint,
    p_period_id bigint DEFAULT NULL,
    p_codigo_cuenta varchar DEFAULT NULL
) RETURNS TABLE(
    period_id bigint,
    codigo_cuenta varchar,
    debitos numeric,
    creditos numeric,
    cantidad_debitos integer,
    cantidad_creditos integer
)
LANGUAGE sql
STABLE
AS $function$
    SELECT
        h.period_id,
        a.code AS codigo_cuenta,
        ROUND(SUM(COALESCE(d.debit_amount, 0)), 2) AS debitos,
        ROUND(SUM(COALESCE(d.credit_amount, 0)), 2) AS creditos,
        SUM(CASE WHEN COALESCE(d.debit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cantidad_debitos,
        SUM(CASE WHEN COALESCE(d.credit_amount, 0) > 0 THEN 1 ELSE 0 END)::int AS cantidad_creditos
    FROM public.con_partida_hdr h
    JOIN public.con_partida_dtl d
      ON d.company_id = h.company_id
     AND d.poliza_id = h.poliza_id
    JOIN public.con_plan_cuentas a
      ON a.account_id = d.account_id
    WHERE h.company_id = p_company_id
      AND h.status = 1
      AND h.period_id IS NOT NULL
      AND (p_period_id IS NULL OR h.period_id = p_period_id)
      AND (p_codigo_cuenta IS NULL OR a.code = p_codigo_cuenta)
    GROUP BY h.period_id, a.code
$function$;

COMMENT ON FUNCTION public.fn_con_saldo_libro(bigint, bigint, varchar) IS
'F6: agregación canónica del libro (con_partida_dtl POSTED) por período/cuenta, con la misma semántica que el motor. Fuente única del rebuild y el verificador.';

-- -----------------------------------------------------------------------------
-- 2. sp_con_reconstruir_saldo_cuenta — reconstrucción total desde el libro
-- -----------------------------------------------------------------------------
-- Reconstruye el bucket (mes=13, tipo_transaccion=0) — el ÚNICO que escribe
-- sp_con_actualizar_saldos_por_poliza — de UNA empresa, desde las pólizas
-- POSTED. No toca otros buckets ni otras empresas. `presupuesto` se preserva
-- desde el respaldo de la misma corrida (el motor nunca lo escribe); las filas
-- con presupuesto y sin movimientos posteados también se conservan.
-- Pólizas POSTED con period_id NULL no son representables en el caché
-- (periodo_id NOT NULL): se cuentan en polizas_sin_periodo y generan WARNING —
-- el motor siempre resuelve período al postear, así que >0 indica datos rotos.
CREATE OR REPLACE FUNCTION public.sp_con_reconstruir_saldo_cuenta(
    p_company_id bigint,
    p_user text DEFAULT 'system'
) RETURNS TABLE(
    backup_tag text,
    filas_respaldadas integer,
    filas_eliminadas integer,
    filas_insertadas integer,
    periodos integer,
    cuentas integer,
    polizas_sin_periodo integer
)
LANGUAGE plpgsql
VOLATILE
AS $function$
DECLARE
    v_tag text;
    v_respaldadas integer := 0;
    v_eliminadas integer := 0;
    v_insertadas integer := 0;
    v_periodos integer := 0;
    v_cuentas integer := 0;
    v_sin_periodo integer := 0;
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'sp_con_reconstruir_saldo_cuenta: p_company_id es obligatorio.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.cfg_company c WHERE c.company_id = p_company_id) THEN
        RAISE EXCEPTION 'sp_con_reconstruir_saldo_cuenta: no existe cfg_company.company_id=%.', p_company_id;
    END IF;

    v_tag := 'rebuild_' || to_char(clock_timestamp(), 'YYYYMMDD_HH24MISS_MS')
             || '_c' || p_company_id || '_' || COALESCE(NULLIF(trim(p_user), ''), 'system');

    -- Bloquea escrituras concurrentes del motor mientras dura el rebuild
    -- (mismo lock que usaba el script manual 20260310). OJO: es lock de
    -- TABLA — durante el rebuild ninguna empresa puede postear/revertir;
    -- por eso la reconstrucción es de ventana de mantenimiento.
    LOCK TABLE public.con_saldo_cuenta IN SHARE ROW EXCLUSIVE MODE;

    SELECT COUNT(*) INTO v_sin_periodo
    FROM public.con_partida_hdr h
    WHERE h.company_id = p_company_id
      AND h.status = 1
      AND h.period_id IS NULL;

    IF v_sin_periodo > 0 THEN
        RAISE WARNING 'sp_con_reconstruir_saldo_cuenta: % pólizas POSTED sin period_id en company % — quedan FUERA del caché; corregir con_partida_hdr.period_id.',
            v_sin_periodo, p_company_id;
    END IF;

    INSERT INTO public.con_saldo_cuenta_backup_hist
    SELECT now(), v_tag, s.*
    FROM public.con_saldo_cuenta s
    WHERE s.company_id = p_company_id
      AND s.mes = 13
      AND s.tipo_transaccion = 0;
    GET DIAGNOSTICS v_respaldadas = ROW_COUNT;

    DELETE FROM public.con_saldo_cuenta s
    WHERE s.company_id = p_company_id
      AND s.mes = 13
      AND s.tipo_transaccion = 0;
    GET DIAGNOSTICS v_eliminadas = ROW_COUNT;

    WITH posted AS (
        -- Fuente única (sección 1b): misma agregación que usa el verificador.
        SELECT l.period_id, l.codigo_cuenta, l.debitos, l.creditos,
               l.cantidad_debitos, l.cantidad_creditos
        FROM public.fn_con_saldo_libro(p_company_id) l
    ),
    presupuesto_previo AS (
        -- filas con presupuesto cargado en el caché anterior (el motor no lo
        -- escribe): se preserva, incluso sin movimientos posteados.
        SELECT b.periodo_id, b.codigo_cuenta, b.presupuesto
        FROM public.con_saldo_cuenta_backup_hist b
        WHERE b.company_id = p_company_id
          AND b.backup_tag = v_tag
          AND b.presupuesto <> 0
    )
    INSERT INTO public.con_saldo_cuenta (
        company_id, periodo_id, codigo_cuenta, mes, tipo_transaccion,
        debitos, creditos, cantidad_debitos, cantidad_creditos,
        presupuesto, created_at, updated_at
    )
    SELECT
        p_company_id,
        COALESCE(p.period_id, b.periodo_id),
        COALESCE(p.codigo_cuenta, b.codigo_cuenta),
        13,
        0,
        COALESCE(p.debitos, 0),
        COALESCE(p.creditos, 0),
        COALESCE(p.cantidad_debitos, 0),
        COALESCE(p.cantidad_creditos, 0),
        COALESCE(b.presupuesto, 0),
        now(),
        now()
    FROM posted p
    FULL OUTER JOIN presupuesto_previo b
      ON b.periodo_id = p.period_id
     AND b.codigo_cuenta = p.codigo_cuenta;
    GET DIAGNOSTICS v_insertadas = ROW_COUNT;

    SELECT COUNT(DISTINCT s.periodo_id), COUNT(DISTINCT s.codigo_cuenta)
      INTO v_periodos, v_cuentas
    FROM public.con_saldo_cuenta s
    WHERE s.company_id = p_company_id
      AND s.mes = 13
      AND s.tipo_transaccion = 0;

    RETURN QUERY SELECT v_tag, v_respaldadas, v_eliminadas, v_insertadas,
        v_periodos, v_cuentas, v_sin_periodo;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_reconstruir_saldo_cuenta(bigint, text) IS
'F6: reconstrucción total del caché con_saldo_cuenta (mes=13/tipo=0) de una empresa desde con_partida_dtl (status=1). Respaldar→borrar→reinsertar; preserva presupuesto. Ejecutar SIEMPRE al final de una remigración SIMAFI. No es un tercer escritor (D1): es rebuild desde el libro en mantenimiento.';

-- -----------------------------------------------------------------------------
-- 3. fn_con_verificar_saldo_cuenta — reconciliación caché vs libro
-- -----------------------------------------------------------------------------
-- Compara con_saldo_cuenta (bucket del motor) contra fn_con_saldo_libro
-- (agregación canónica del libro) por (período, cuenta). Devuelve SOLO las
-- divergencias:
--   SOLO_CACHE  — fila en el caché sin respaldo en el libro
--   SOLO_LIBRO  — movimientos posteados sin fila en el caché
--   MONTOS      — débitos/créditos distintos
--   CONTEOS     — montos iguales pero cantidad_debitos/creditos distintas
--   FECHA_FUERA_PERIODO — pólizas POSTED cuya poliza_date cae fuera del rango
--                 de su período (columnas *_libro = montos afectados, *_cache
--                 NULL). No es divergencia caché-vs-libro, pero rompe la
--                 equivalencia caché=vivo del balance híbrido (sección 4):
--                 el reporte por rango de fechas y el caché por período
--                 clasifican esa póliza distinto. El motor lo impide al
--                 postear, así que >0 = data legacy/remigración a corregir.
-- Los grupos con montos y conteos en 0 se excluyen SIMÉTRICAMENTE de ambos
-- lados (líneas 0.00 del libro y filas solo-presupuesto del caché no son
-- divergencia). 0 filas = caché consistente + supuesto del híbrido válido.
-- Solo lectura; multitenant por p_company_id.
CREATE OR REPLACE FUNCTION public.fn_con_verificar_saldo_cuenta(
    p_company_id bigint,
    p_period_id bigint DEFAULT NULL,
    p_codigo_cuenta varchar DEFAULT NULL
) RETURNS TABLE(
    period_id bigint,
    periodo_code varchar,
    codigo_cuenta varchar,
    tipo_divergencia text,
    debitos_cache numeric,
    debitos_libro numeric,
    creditos_cache numeric,
    creditos_libro numeric,
    cantidad_debitos_cache integer,
    cantidad_debitos_libro integer,
    cantidad_creditos_cache integer,
    cantidad_creditos_libro integer
)
LANGUAGE sql
STABLE
AS $function$
    WITH libro AS (
        -- Fuente única (sección 1b) con la MISMA exclusión de grupos todo-cero
        -- que el lado caché (una línea 0.00 posteada no es divergencia).
        SELECT l.period_id, l.codigo_cuenta, l.debitos, l.creditos,
               l.cantidad_debitos, l.cantidad_creditos
        FROM public.fn_con_saldo_libro(p_company_id, p_period_id, p_codigo_cuenta) l
        WHERE NOT (l.debitos = 0 AND l.creditos = 0
                   AND l.cantidad_debitos = 0 AND l.cantidad_creditos = 0)
    ),
    cache AS (
        SELECT
            s.periodo_id AS period_id,
            s.codigo_cuenta,
            ROUND(s.debitos, 2) AS debitos,
            ROUND(s.creditos, 2) AS creditos,
            s.cantidad_debitos,
            s.cantidad_creditos
        FROM public.con_saldo_cuenta s
        WHERE s.company_id = p_company_id
          AND s.mes = 13
          AND s.tipo_transaccion = 0
          AND (p_period_id IS NULL OR s.periodo_id = p_period_id)
          AND (p_codigo_cuenta IS NULL OR s.codigo_cuenta = p_codigo_cuenta)
          -- exclusión simétrica: filas todo-cero (p.ej. solo-presupuesto)
          AND NOT (s.debitos = 0 AND s.creditos = 0
                   AND s.cantidad_debitos = 0 AND s.cantidad_creditos = 0)
    ),
    divergencias AS (
        SELECT
            COALESCE(l.period_id, c.period_id) AS period_id,
            COALESCE(l.codigo_cuenta, c.codigo_cuenta) AS codigo_cuenta,
            CASE
                WHEN l.period_id IS NULL THEN 'SOLO_CACHE'
                WHEN c.period_id IS NULL THEN 'SOLO_LIBRO'
                WHEN l.debitos <> c.debitos OR l.creditos <> c.creditos THEN 'MONTOS'
                ELSE 'CONTEOS'
            END AS tipo_divergencia,
            c.debitos AS debitos_cache,
            l.debitos AS debitos_libro,
            c.creditos AS creditos_cache,
            l.creditos AS creditos_libro,
            c.cantidad_debitos AS cantidad_debitos_cache,
            l.cantidad_debitos AS cantidad_debitos_libro,
            c.cantidad_creditos AS cantidad_creditos_cache,
            l.cantidad_creditos AS cantidad_creditos_libro
        FROM libro l
        FULL OUTER JOIN cache c
          ON c.period_id = l.period_id
         AND c.codigo_cuenta = l.codigo_cuenta
        WHERE l.period_id IS NULL
           OR c.period_id IS NULL
           OR l.debitos <> c.debitos
           OR l.creditos <> c.creditos
           OR l.cantidad_debitos <> c.cantidad_debitos
           OR l.cantidad_creditos <> c.cantidad_creditos

        UNION ALL

        -- Diagnóstico: pólizas POSTED con poliza_date fuera del rango de su
        -- período — el balance híbrido las clasifica distinto por caché
        -- (período) que por libro (fecha). Corregir period_id o poliza_date
        -- y reconstruir.
        SELECT
            h.period_id,
            a.code AS codigo_cuenta,
            'FECHA_FUERA_PERIODO' AS tipo_divergencia,
            NULL::numeric, ROUND(SUM(COALESCE(d.debit_amount, 0)), 2),
            NULL::numeric, ROUND(SUM(COALESCE(d.credit_amount, 0)), 2),
            NULL::integer, NULL::integer, NULL::integer, NULL::integer
        FROM public.con_partida_hdr h
        JOIN public.con_periodo_contable per
          ON per.period_id = h.period_id
        JOIN public.con_partida_dtl d
          ON d.company_id = h.company_id
         AND d.poliza_id = h.poliza_id
        JOIN public.con_plan_cuentas a
          ON a.account_id = d.account_id
        WHERE h.company_id = p_company_id
          AND h.status = 1
          AND (p_period_id IS NULL OR h.period_id = p_period_id)
          AND (p_codigo_cuenta IS NULL OR a.code = p_codigo_cuenta)
          AND (h.poliza_date::date < per.start_date::date
               OR h.poliza_date::date > per.end_date::date)
        GROUP BY h.period_id, a.code
    )
    SELECT
        dv.period_id,
        pc.code AS periodo_code,
        dv.codigo_cuenta,
        dv.tipo_divergencia,
        dv.debitos_cache,
        dv.debitos_libro,
        dv.creditos_cache,
        dv.creditos_libro,
        dv.cantidad_debitos_cache,
        dv.cantidad_debitos_libro,
        dv.cantidad_creditos_cache,
        dv.cantidad_creditos_libro
    FROM divergencias dv
    LEFT JOIN public.con_periodo_contable pc
      ON pc.period_id = dv.period_id
    ORDER BY 1, 3, 4;
$function$;

COMMENT ON FUNCTION public.fn_con_verificar_saldo_cuenta(bigint, bigint, varchar) IS
'F6: reconciliación con_saldo_cuenta (caché del motor) vs fn_con_saldo_libro por período/cuenta, más diagnóstico FECHA_FUERA_PERIODO. 0 filas = consistente. Divergencias → sp_con_reconstruir_saldo_cuenta en ventana de mantenimiento; FECHA_FUERA_PERIODO → corregir period_id/poliza_date.';

-- -----------------------------------------------------------------------------
-- 4. rep_balance_comprobacion v2 — períodos cerrados leen del caché
-- -----------------------------------------------------------------------------
-- Misma firma y mismas columnas que la v1 (20260702): el ESF comparativo la
-- llama dos veces y el dataset del catálogo (balance-comprobacion) mapea sus
-- parámetros por nombre. Cambia SOLO la fuente de movimientos:
--   * períodos CERRADOS (status_id=2) cuyo rango cae completo en un bucket del
--     reporte (todo antes de p_fecha_desde, o todo dentro de [desde, hasta])
--     suman desde con_saldo_cuenta — el caché oficial (F6);
--   * todo lo demás (períodos abiertos/precierre, períodos cerrados que el
--     rango pedido parte al medio, pólizas sin período) sigue en vivo desde
--     con_partida_dtl.
-- Equivalencia caché=vivo garantizada por el motor único (valida fecha dentro
-- del período al postear) + fn_con_verificar_saldo_cuenta.
DROP FUNCTION IF EXISTS public.rep_balance_comprobacion(bigint, date, date, boolean);

CREATE FUNCTION public.rep_balance_comprobacion(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date,
    p_incluir_sin_movimiento boolean DEFAULT false
)
RETURNS TABLE(
    empresa_id bigint,
    empresa_codigo text,
    empresa_nombre text,
    empresa_nombre_legal text,
    empresa_rtn text,
    empresa_email text,
    empresa_telefono text,
    empresa_direccion text,
    rubro_orden integer,
    rubro_nombre text,
    cuenta_id bigint,
    cuenta_padre_id bigint,
    cuenta_codigo character varying,
    cuenta_nombre text,
    cuenta_nombre_mostrar text,
    tipo_cuenta character varying,
    categoria character varying,
    nivel smallint,
    permite_movimiento boolean,
    tiene_hijos boolean,
    saldo_anterior numeric,
    saldo_anterior_deudor numeric,
    saldo_anterior_acreedor numeric,
    debitos_periodo numeric,
    creditos_periodo numeric,
    saldo_actual numeric,
    saldo_actual_deudor numeric,
    saldo_actual_acreedor numeric
)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;

    IF p_fecha_desde IS NULL OR p_fecha_hasta IS NULL THEN
        RAISE EXCEPTION 'Los parametros p_fecha_desde y p_fecha_hasta son obligatorios.';
    END IF;

    IF p_fecha_hasta < p_fecha_desde THEN
        RAISE EXCEPTION 'La fecha hasta no puede ser menor que la fecha desde.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.cfg_company co
        WHERE co.company_id = p_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%.', p_company_id;
    END IF;

    RETURN QUERY
    WITH RECURSIVE plan AS
    (
        SELECT
            c.account_id,
            c.parent_account_id,
            c.code,
            c.name,
            c.account_type,
            c.category,
            c.level,
            c.allows_posting,
            c.status
        FROM public.con_plan_cuentas c
        WHERE c.company_id = p_company_id
          AND COALESCE(upper(c.status), 'ACTIVO') NOT IN ('INACTIVO', 'INACTIVE')
    ),
    empresa AS
    (
        SELECT
            co.company_id AS empresa_id,
            co.code::text AS empresa_codigo,
            co.commercial_name::text AS empresa_nombre,
            co.legal_name::text AS empresa_nombre_legal,
            ec.id_fiscal_valor::text AS empresa_rtn,
            co.email::text AS empresa_email,
            co.phone::text AS empresa_telefono,
            co.address::text AS empresa_direccion
        FROM public.cfg_company co
        LEFT JOIN public.con_empresa_configuracion ec
          ON ec.company_id = co.company_id
        WHERE co.company_id = p_company_id
        LIMIT 1
    ),
    descendants AS
    (
        SELECT
            p.account_id AS ancestor_id,
            p.account_id AS descendant_id
        FROM plan p

        UNION ALL

        SELECT
            d.ancestor_id,
            c.account_id AS descendant_id
        FROM descendants d
        JOIN plan c
          ON c.parent_account_id = d.descendant_id
    ),
    -- Períodos cerrados clasificados por bucket del reporte:
    --   1 = completo antes de p_fecha_desde  → saldo anterior desde el caché
    --   2 = completo dentro de [desde,hasta] → movimiento del período desde el caché
    --   0 = el rango pedido lo parte         → se calcula en vivo
    periodos_cerrados AS
    (
        SELECT
            pc.period_id,
            CASE
                WHEN pc.end_date::date < p_fecha_desde THEN 1
                WHEN pc.start_date::date >= p_fecha_desde
                 AND pc.end_date::date <= p_fecha_hasta THEN 2
                ELSE 0
            END AS bucket
        FROM public.con_periodo_contable pc
        WHERE pc.company_id = p_company_id
          AND pc.status_id = 2
    ),
    -- El caché guarda el CÓDIGO de cuenta snapshoteado al postear: si una
    -- cuenta se recodifica en el plan, sus filas cacheadas dejan de matchear
    -- este join y el saldo desaparecería del reporte de períodos cerrados.
    -- fn_con_verificar_saldo_cuenta lo delata (SOLO_CACHE del código viejo +
    -- SOLO_LIBRO del nuevo) y sp_con_reconstruir_saldo_cuenta lo sana
    -- (reagrega con los códigos vigentes).
    mov_cache AS
    (
        SELECT
            a.account_id,
            COALESCE(SUM(s.debitos)  FILTER (WHERE pcer.bucket = 1), 0)::numeric(18,2) AS prev_debits,
            COALESCE(SUM(s.creditos) FILTER (WHERE pcer.bucket = 1), 0)::numeric(18,2) AS prev_credits,
            COALESCE(SUM(s.debitos)  FILTER (WHERE pcer.bucket = 2), 0)::numeric(18,2) AS period_debits,
            COALESCE(SUM(s.creditos) FILTER (WHERE pcer.bucket = 2), 0)::numeric(18,2) AS period_credits
        FROM public.con_saldo_cuenta s
        JOIN periodos_cerrados pcer
          ON pcer.period_id = s.periodo_id
         AND pcer.bucket IN (1, 2)
        JOIN public.con_plan_cuentas a
          ON a.company_id = s.company_id
         AND a.code = s.codigo_cuenta
        WHERE s.company_id = p_company_id
          AND s.mes = 13
          AND s.tipo_transaccion = 0
        GROUP BY a.account_id
    ),
    mov_vivo AS
    (
        SELECT
            d.account_id,
            COALESCE(SUM(CASE WHEN h.poliza_date::date < p_fecha_desde THEN d.debit_amount ELSE 0 END), 0)::numeric(18,2) AS prev_debits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date < p_fecha_desde THEN d.credit_amount ELSE 0 END), 0)::numeric(18,2) AS prev_credits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date >= p_fecha_desde AND h.poliza_date::date <= p_fecha_hasta THEN d.debit_amount ELSE 0 END), 0)::numeric(18,2) AS period_debits,
            COALESCE(SUM(CASE WHEN h.poliza_date::date >= p_fecha_desde AND h.poliza_date::date <= p_fecha_hasta THEN d.credit_amount ELSE 0 END), 0)::numeric(18,2) AS period_credits
        FROM public.con_partida_hdr h
        JOIN public.con_partida_dtl d
          ON d.company_id = h.company_id
         AND d.poliza_id = h.poliza_id
        -- anti-join (en vez de OR NOT EXISTS, que degrada a subplan por
        -- fila): excluye las pólizas de períodos servidos por el caché;
        -- h.period_id NULL nunca matchea el join → esas pólizas quedan vivas.
        LEFT JOIN periodos_cerrados pcer
          ON pcer.period_id = h.period_id
         AND pcer.bucket IN (1, 2)
        WHERE h.company_id = p_company_id
          AND h.status = 1
          AND h.poliza_date::date <= p_fecha_hasta
          AND pcer.period_id IS NULL
        GROUP BY d.account_id
    ),
    movimientos_base AS
    (
        SELECT
            u.account_id,
            SUM(u.prev_debits)::numeric(18,2) AS prev_debits,
            SUM(u.prev_credits)::numeric(18,2) AS prev_credits,
            SUM(u.period_debits)::numeric(18,2) AS period_debits,
            SUM(u.period_credits)::numeric(18,2) AS period_credits
        FROM (
            SELECT * FROM mov_cache
            UNION ALL
            SELECT * FROM mov_vivo
        ) u
        GROUP BY u.account_id
    ),
    movimientos_agregados AS
    (
        SELECT
            d.ancestor_id AS account_id,
            COALESCE(SUM(m.prev_debits), 0)::numeric(18,2) AS prev_debits,
            COALESCE(SUM(m.prev_credits), 0)::numeric(18,2) AS prev_credits,
            COALESCE(SUM(m.period_debits), 0)::numeric(18,2) AS period_debits,
            COALESCE(SUM(m.period_credits), 0)::numeric(18,2) AS period_credits
        FROM descendants d
        LEFT JOIN movimientos_base m
          ON m.account_id = d.descendant_id
        GROUP BY d.ancestor_id
    ),
    saldos AS
    (
        SELECT
            p.account_id,
            p.parent_account_id,
            p.code,
            p.name,
            p.account_type,
            p.category,
            p.level,
            p.allows_posting,
            EXISTS(
                SELECT 1
                FROM plan h
                WHERE h.parent_account_id = p.account_id
            ) AS has_children,
            COALESCE(m.prev_debits, 0)::numeric(18,2) AS prev_debits,
            COALESCE(m.prev_credits, 0)::numeric(18,2) AS prev_credits,
            COALESCE(m.period_debits, 0)::numeric(18,2) AS period_debits,
            COALESCE(m.period_credits, 0)::numeric(18,2) AS period_credits,
            ROUND(COALESCE(m.prev_debits, 0) - COALESCE(m.prev_credits, 0), 2) AS prev_balance,
            ROUND(
                (COALESCE(m.prev_debits, 0) - COALESCE(m.prev_credits, 0))
                + (COALESCE(m.period_debits, 0) - COALESCE(m.period_credits, 0)),
                2) AS current_balance
        FROM plan p
        LEFT JOIN movimientos_agregados m
          ON m.account_id = p.account_id
    )
    SELECT
        e.empresa_id,
        e.empresa_codigo,
        e.empresa_nombre,
        e.empresa_nombre_legal,
        e.empresa_rtn,
        e.empresa_email,
        e.empresa_telefono,
        e.empresa_direccion,
        CASE upper(COALESCE(s.account_type, ''))
            WHEN 'ACTIVO' THEN 10
            WHEN 'PASIVO' THEN 20
            WHEN 'CAPITAL' THEN 30
            WHEN 'INGRESO' THEN 40
            WHEN 'GASTO' THEN 50
            WHEN 'MEMORANDA' THEN 60
            ELSE 99
        END AS rubro_orden,
        CASE upper(COALESCE(s.account_type, ''))
            WHEN 'ACTIVO' THEN 'Activo'
            WHEN 'PASIVO' THEN 'Pasivo'
            WHEN 'CAPITAL' THEN 'Patrimonio'
            WHEN 'INGRESO' THEN 'Ingresos'
            WHEN 'GASTO' THEN 'Gastos'
            WHEN 'MEMORANDA' THEN 'Memoranda'
            ELSE 'Otros'
        END AS rubro_nombre,
        s.account_id AS cuenta_id,
        s.parent_account_id AS cuenta_padre_id,
        s.code AS cuenta_codigo,
        s.name::text AS cuenta_nombre,
        concat(repeat('  ', GREATEST(s.level::integer - 1, 0)), s.name)::text AS cuenta_nombre_mostrar,
        s.account_type AS tipo_cuenta,
        s.category AS categoria,
        s.level AS nivel,
        s.allows_posting AS permite_movimiento,
        s.has_children AS tiene_hijos,
        s.prev_balance AS saldo_anterior,
        CASE WHEN s.prev_balance > 0 THEN s.prev_balance ELSE 0 END AS saldo_anterior_deudor,
        CASE WHEN s.prev_balance < 0 THEN abs(s.prev_balance) ELSE 0 END AS saldo_anterior_acreedor,
        s.period_debits AS debitos_periodo,
        s.period_credits AS creditos_periodo,
        s.current_balance AS saldo_actual,
        CASE WHEN s.current_balance > 0 THEN s.current_balance ELSE 0 END AS saldo_actual_deudor,
        CASE WHEN s.current_balance < 0 THEN abs(s.current_balance) ELSE 0 END AS saldo_actual_acreedor
    FROM saldos s
    CROSS JOIN empresa e
    WHERE p_incluir_sin_movimiento
       OR abs(s.prev_balance) > 0.004
       OR abs(s.period_debits) > 0.004
       OR abs(s.period_credits) > 0.004
       OR abs(s.current_balance) > 0.004
    ORDER BY
        rubro_orden,
        s.code;
END;
$function$;

COMMENT ON FUNCTION public.rep_balance_comprobacion(bigint, date, date, boolean)
IS 'Balance de comprobacion para reporteria web. F6: períodos cerrados (status_id=2) contenidos en el rango suman desde con_saldo_cuenta (caché oficial); abiertos/precierre y períodos partidos por el rango calculan en vivo desde con_partida_dtl.';

-- -----------------------------------------------------------------------------
-- 5. Reconstrucción inicial del caché al aplicar el script
-- -----------------------------------------------------------------------------
-- Desde la sección 4 los períodos cerrados REPORTAN desde el caché. Para que
-- no exista ninguna ventana con caché divergente (la "inconsistencia
-- acumulada por remigraciones+cierres SIMAFI" que este script viene a
-- corregir), se reconstruye aquí mismo el caché de TODAS las empresas con
-- pólizas POSTED. Idempotente: re-aplicar el script re-reconstruye al mismo
-- estado (los respaldos se acumulan en con_saldo_cuenta_backup_hist, cada
-- corrida con su backup_tag).
DO $do$
DECLARE
    v_company bigint;
    v_resultado record;
BEGIN
    FOR v_company IN
        SELECT DISTINCT h.company_id
        FROM public.con_partida_hdr h
        WHERE h.status = 1
        ORDER BY 1
    LOOP
        SELECT * INTO v_resultado
        FROM public.sp_con_reconstruir_saldo_cuenta(v_company, 'ddl-f6');

        RAISE NOTICE 'F6 rebuild company %: % filas insertadas (respaldadas %, períodos %, cuentas %, pólizas sin período %) — tag %',
            v_company, v_resultado.filas_insertadas, v_resultado.filas_respaldadas,
            v_resultado.periodos, v_resultado.cuentas, v_resultado.polizas_sin_periodo,
            v_resultado.backup_tag;
    END LOOP;
END
$do$;
