-- 2026-07-24_presupuesto_multitenant_company_id.sql
-- Multitenant en el módulo Presupuesto:
--   * agrega company_id a pst_config_presupuesto_hdr / pst_config_presupuesto_dtl,
--   * PKs compuestas por empresa y FKs de pst_actividad / pst_solicitud recompuestas,
--   * funciones fn_pst_* y vistas del módulo pasan a filtrar por company_id.
-- Backfill: toda la data existente pertenece a la empresa 2 (única activa hoy).
-- No borra ni modifica filas de negocio. Reejecutable (idempotente).

BEGIN;

-- ============================================================
-- 1) company_id en hdr/dtl + backfill (empresa 2)
-- ============================================================

ALTER TABLE public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS company_id BIGINT;

ALTER TABLE public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS company_id BIGINT;

UPDATE public.pst_config_presupuesto_hdr
   SET company_id = 2
 WHERE company_id IS NULL;

-- El detalle hereda la empresa de su encabezado; el remanente cae a la empresa 2.
UPDATE public.pst_config_presupuesto_dtl d
   SET company_id = h.company_id
  FROM public.pst_config_presupuesto_hdr h
 WHERE d.company_id IS NULL
   AND h.id_presupuesto = d.id_presupuesto;

UPDATE public.pst_config_presupuesto_dtl
   SET company_id = 2
 WHERE company_id IS NULL;

ALTER TABLE public.pst_config_presupuesto_hdr
    ALTER COLUMN company_id SET NOT NULL;

ALTER TABLE public.pst_config_presupuesto_dtl
    ALTER COLUMN company_id SET NOT NULL;

COMMENT ON COLUMN public.pst_config_presupuesto_hdr.company_id IS
    'Empresa propietaria del presupuesto (multitenant).';
COMMENT ON COLUMN public.pst_config_presupuesto_dtl.company_id IS
    'Empresa propietaria del detalle presupuestario (multitenant).';

-- ============================================================
-- 2) Soltar FKs que dependen de las PKs actuales
-- ============================================================

ALTER TABLE public.pst_config_presupuesto_dtl
    DROP CONSTRAINT IF EXISTS fk_pst_config_presupuesto_dtl_hdr;

ALTER TABLE public.pst_actividad_presupuesto
    DROP CONSTRAINT IF EXISTS fk_pst_act_hdr,
    DROP CONSTRAINT IF EXISTS fk_pst_act_dtl_origen,
    DROP CONSTRAINT IF EXISTS fk_pst_act_dtl_destino;

ALTER TABLE public.pst_solicitud_actividad_presupuesto
    DROP CONSTRAINT IF EXISTS fk_pst_sol_hdr,
    DROP CONSTRAINT IF EXISTS fk_pst_sol_dtl_origen,
    DROP CONSTRAINT IF EXISTS fk_pst_sol_dtl_destino;

-- ============================================================
-- 3) PKs compuestas por empresa
-- ============================================================

DO $$
DECLARE
    v_pk_cols text;
BEGIN
    SELECT string_agg(a.attname, ',' ORDER BY k.ordinality)
      INTO v_pk_cols
      FROM pg_constraint c
      JOIN unnest(c.conkey) WITH ORDINALITY AS k(attnum, ordinality) ON TRUE
      JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
     WHERE c.conrelid = 'public.pst_config_presupuesto_hdr'::regclass
       AND c.contype = 'p';

    IF v_pk_cols IS DISTINCT FROM 'company_id,id_presupuesto' THEN
        ALTER TABLE public.pst_config_presupuesto_hdr
            DROP CONSTRAINT IF EXISTS pk_pst_config_presupuesto_hdr;
        ALTER TABLE public.pst_config_presupuesto_hdr
            ADD CONSTRAINT pk_pst_config_presupuesto_hdr
            PRIMARY KEY (company_id, id_presupuesto);
    END IF;
END;
$$;

DO $$
DECLARE
    v_pk_cols text;
BEGIN
    SELECT string_agg(a.attname, ',' ORDER BY k.ordinality)
      INTO v_pk_cols
      FROM pg_constraint c
      JOIN unnest(c.conkey) WITH ORDINALITY AS k(attnum, ordinality) ON TRUE
      JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
     WHERE c.conrelid = 'public.pst_config_presupuesto_dtl'::regclass
       AND c.contype = 'p';

    IF v_pk_cols IS DISTINCT FROM 'company_id,id_presupuesto,con_cuenta_code' THEN
        ALTER TABLE public.pst_config_presupuesto_dtl
            DROP CONSTRAINT IF EXISTS pk_pst_config_presupuesto_dtl;
        ALTER TABLE public.pst_config_presupuesto_dtl
            ADD CONSTRAINT pk_pst_config_presupuesto_dtl
            PRIMARY KEY (company_id, id_presupuesto, con_cuenta_code);
    END IF;
END;
$$;

-- ============================================================
-- 4) FKs hacia cfg_company y FKs compuestas recreadas
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'fk_pst_cfg_hdr_company'
           AND conrelid = 'public.pst_config_presupuesto_hdr'::regclass
    ) THEN
        ALTER TABLE public.pst_config_presupuesto_hdr
            ADD CONSTRAINT fk_pst_cfg_hdr_company FOREIGN KEY (company_id)
            REFERENCES public.cfg_company (company_id) ON DELETE RESTRICT;
    END IF;
END;
$$;

ALTER TABLE public.pst_config_presupuesto_dtl
    ADD CONSTRAINT fk_pst_config_presupuesto_dtl_hdr
    FOREIGN KEY (company_id, id_presupuesto)
    REFERENCES public.pst_config_presupuesto_hdr (company_id, id_presupuesto)
    ON UPDATE CASCADE
    ON DELETE CASCADE;

ALTER TABLE public.pst_actividad_presupuesto
    ADD CONSTRAINT fk_pst_act_hdr
    FOREIGN KEY (company_id, id_presupuesto)
    REFERENCES public.pst_config_presupuesto_hdr (company_id, id_presupuesto)
    ON UPDATE CASCADE ON DELETE RESTRICT,
    ADD CONSTRAINT fk_pst_act_dtl_origen
    FOREIGN KEY (company_id, id_presupuesto, cuenta_origen_code)
    REFERENCES public.pst_config_presupuesto_dtl (company_id, id_presupuesto, con_cuenta_code)
    ON UPDATE CASCADE ON DELETE RESTRICT,
    ADD CONSTRAINT fk_pst_act_dtl_destino
    FOREIGN KEY (company_id, id_presupuesto, cuenta_destino_code)
    REFERENCES public.pst_config_presupuesto_dtl (company_id, id_presupuesto, con_cuenta_code)
    ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE public.pst_solicitud_actividad_presupuesto
    ADD CONSTRAINT fk_pst_sol_hdr
    FOREIGN KEY (company_id, id_presupuesto)
    REFERENCES public.pst_config_presupuesto_hdr (company_id, id_presupuesto)
    ON UPDATE CASCADE ON DELETE RESTRICT,
    ADD CONSTRAINT fk_pst_sol_dtl_origen
    FOREIGN KEY (company_id, id_presupuesto, cuenta_origen_code)
    REFERENCES public.pst_config_presupuesto_dtl (company_id, id_presupuesto, con_cuenta_code)
    ON UPDATE CASCADE ON DELETE RESTRICT,
    ADD CONSTRAINT fk_pst_sol_dtl_destino
    FOREIGN KEY (company_id, id_presupuesto, cuenta_destino_code)
    REFERENCES public.pst_config_presupuesto_dtl (company_id, id_presupuesto, con_cuenta_code)
    ON UPDATE CASCADE ON DELETE RESTRICT;

-- ============================================================
-- 5) Índice único del correlativo de detalle, por empresa
-- ============================================================

DROP INDEX IF EXISTS public.ux_pst_config_presupuesto_dtl_id_presupuesto_dtl;

CREATE UNIQUE INDEX ux_pst_config_presupuesto_dtl_id_presupuesto_dtl
    ON public.pst_config_presupuesto_dtl (company_id, id_presupuesto, id_presupuesto_dtl);

-- ============================================================
-- 6) Funciones company-aware
-- ============================================================

-- 6.1 Correlativo de detalle: cambia de firma (agrega company_id).
DROP FUNCTION IF EXISTS public.fn_pst_next_id_presupuesto_dtl(varchar);

CREATE OR REPLACE FUNCTION public.fn_pst_next_id_presupuesto_dtl(
    p_company_id bigint,
    p_id_presupuesto varchar
)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    v_base_id bigint;
    v_next_id bigint;
BEGIN
    IF p_company_id IS NULL THEN
        RAISE EXCEPTION 'company_id es requerido para generar id_presupuesto_dtl.';
    END IF;

    IF p_id_presupuesto IS NULL
       OR btrim(p_id_presupuesto) !~ '^[0-9]+$' THEN
        RAISE EXCEPTION 'id_presupuesto "%" no es numerico. No se puede generar id_presupuesto_dtl.', p_id_presupuesto;
    END IF;

    v_base_id := btrim(p_id_presupuesto)::bigint;

    -- Evita colisiones concurrentes por la misma (empresa, presupuesto).
    PERFORM pg_advisory_xact_lock(
        hashtext(p_company_id::text || '|' || btrim(p_id_presupuesto))::bigint
    );

    SELECT COALESCE(MAX(d.id_presupuesto_dtl), v_base_id) + 1
      INTO v_next_id
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto;

    RETURN v_next_id;
END;
$$;

-- 6.2 Recalcular disponible: cambia de firma (agrega company_id).
DROP FUNCTION IF EXISTS public.fn_pst_recalcular_valor_disponible(varchar);

CREATE OR REPLACE FUNCTION public.fn_pst_recalcular_valor_disponible(
    p_company_id bigint,
    p_id_presupuesto varchar
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_total_real numeric(18, 4);
BEGIN
    IF p_company_id IS NULL
       OR p_id_presupuesto IS NULL
       OR btrim(p_id_presupuesto) = '' THEN
        RETURN;
    END IF;

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_disponible = GREATEST(
           COALESCE(d.valor_proyeccion, 0) - COALESCE(d.valor_real, 0),
           0
       )
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto;

    SELECT COALESCE(SUM(d.valor_real), 0)
      INTO v_total_real
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto;

    UPDATE public.pst_config_presupuesto_hdr h
       SET valor_disponible = GREATEST(COALESCE(h.valor_global, 0) - v_total_real, 0)
     WHERE h.company_id = p_company_id
       AND h.id_presupuesto = p_id_presupuesto;
END;
$$;

-- 6.3 Delta de valor_real: misma firma, cuerpo filtrado por empresa.
--     Sin company_id ya no afecta presupuestos (guardia multitenant).
CREATE OR REPLACE FUNCTION public.fn_pst_aplicar_delta_valor_real(
    p_company_id bigint,
    p_account_id bigint,
    p_poliza_date date,
    p_delta numeric
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_cuenta_code varchar(30);
    v_presupuestos_afectados varchar(10)[];
    i integer;
BEGIN
    IF p_delta IS NULL OR p_delta = 0 THEN
        RETURN;
    END IF;

    IF p_company_id IS NULL OR p_poliza_date IS NULL OR p_account_id IS NULL THEN
        RETURN;
    END IF;

    v_cuenta_code := public.fn_pst_resolver_cuenta_code(p_company_id, p_account_id);

    IF v_cuenta_code IS NULL THEN
        RETURN;
    END IF;

    WITH updated_rows AS (
        UPDATE public.pst_config_presupuesto_dtl d
           SET valor_real = GREATEST(COALESCE(d.valor_real, 0) + p_delta, 0),
               valor_disponible = GREATEST(
                   COALESCE(d.valor_proyeccion, 0) - GREATEST(COALESCE(d.valor_real, 0) + p_delta, 0),
                   0
               )
         FROM public.pst_config_presupuesto_hdr h
         WHERE h.company_id = p_company_id
           AND d.company_id = h.company_id
           AND h.id_presupuesto = d.id_presupuesto
           AND upper(btrim(d.con_cuenta_code)) = upper(v_cuenta_code)
           AND COALESCE(h.estado_aprobado, FALSE) = TRUE
           AND p_poliza_date BETWEEN h.fecha_inicia AND h.fecha_finaliza
        RETURNING d.id_presupuesto
    )
    SELECT array_agg(DISTINCT u.id_presupuesto)
      INTO v_presupuestos_afectados
      FROM updated_rows u;

    IF v_presupuestos_afectados IS NULL OR array_length(v_presupuestos_afectados, 1) IS NULL THEN
        RETURN;
    END IF;

    FOR i IN 1..array_length(v_presupuestos_afectados, 1) LOOP
        PERFORM public.fn_pst_recalcular_valor_disponible(p_company_id, v_presupuestos_afectados[i]);
    END LOOP;
END;
$$;

-- 6.4 Regla de créditos: misma firma, cuerpo filtrado por empresa.
CREATE OR REPLACE FUNCTION public.fn_pst_afectar_saldo_real_credito(
    p_company_id bigint,
    p_account_id bigint,
    p_poliza_date date,
    p_credito numeric
)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
    v_account_code varchar(30);
    v_allows_budget boolean := false;
    v_id_presupuesto varchar(10);
    v_saldo_real numeric(18,4);
    v_saldo_proyectado numeric(18,4);
    v_estado_aprobado boolean := false;
    v_nuevo_saldo_real numeric(18,4);
BEGIN
    IF p_company_id IS NULL
       OR p_account_id IS NULL
       OR p_poliza_date IS NULL
       OR COALESCE(p_credito, 0) = 0 THEN
        RETURN 1;
    END IF;

    IF to_regclass('public.con_plan_cuenta') IS NOT NULL THEN
        SELECT c.code,
               COALESCE(c.allows_budget, false)
          INTO v_account_code,
               v_allows_budget
          FROM public.con_plan_cuenta c
         WHERE c.account_id = p_account_id
           AND c.company_id = p_company_id
         LIMIT 1;
    ELSIF to_regclass('public.con_plan_cuentas') IS NOT NULL THEN
        SELECT c.code,
               COALESCE(c.allows_budget, false)
          INTO v_account_code,
               v_allows_budget
          FROM public.con_plan_cuentas c
         WHERE c.account_id = p_account_id
           AND c.company_id = p_company_id
         LIMIT 1;
    ELSE
        RETURN 1;
    END IF;

    IF v_account_code IS NULL OR NOT v_allows_budget THEN
        RETURN 1;
    END IF;

    SELECT d.id_presupuesto,
           COALESCE(d.valor_real, 0),
           COALESCE(d.valor_proyeccion, 0),
           COALESCE(h.estado_aprobado, FALSE)
      INTO v_id_presupuesto,
           v_saldo_real,
           v_saldo_proyectado,
           v_estado_aprobado
      FROM public.pst_config_presupuesto_dtl d
      JOIN public.pst_config_presupuesto_hdr h
        ON h.company_id = d.company_id
       AND h.id_presupuesto = d.id_presupuesto
     WHERE d.company_id = p_company_id
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code))
       AND p_poliza_date BETWEEN h.fecha_inicia AND h.fecha_finaliza
     ORDER BY h.fecha_inicia DESC,
              h.id_presupuesto DESC
     LIMIT 1
     FOR UPDATE OF d;

    IF NOT FOUND THEN
        RETURN 1;
    END IF;

    IF p_credito > 0 AND NOT v_estado_aprobado THEN
        RETURN 2;
    END IF;

    IF p_credito > 0 AND v_saldo_real + p_credito > v_saldo_proyectado THEN
        RETURN 0;
    END IF;

    v_nuevo_saldo_real := COALESCE(v_saldo_real, 0) + p_credito;
    IF v_nuevo_saldo_real < 0 THEN
        v_nuevo_saldo_real := 0;
    END IF;

    UPDATE public.pst_config_presupuesto_dtl d
       SET valor_real = v_nuevo_saldo_real,
           valor_disponible = GREATEST(COALESCE(d.valor_proyeccion, 0) - v_nuevo_saldo_real, 0)
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = v_id_presupuesto
       AND upper(btrim(d.con_cuenta_code)) = upper(btrim(v_account_code));

    UPDATE public.pst_config_presupuesto_hdr h
       SET valor_disponible = GREATEST(
           COALESCE(h.valor_global, 0) - (
               SELECT COALESCE(SUM(d.valor_real), 0)
                 FROM public.pst_config_presupuesto_dtl d
                WHERE d.company_id = p_company_id
                  AND d.id_presupuesto = v_id_presupuesto
           ),
           0
       )
     WHERE h.company_id = p_company_id
       AND h.id_presupuesto = v_id_presupuesto;

    RETURN 1;
END;
$$;

-- ============================================================
-- 7) Vistas con empresa
-- ============================================================

DROP VIEW IF EXISTS public.view_lista_configuracion_presupuesto;

CREATE VIEW public.view_lista_configuracion_presupuesto AS
SELECT d.company_id,
    d.id_presupuesto,
    d.con_cuenta_code,
    h.valor_global,
    h.valor_disponible,
    d.valor_proyeccion,
    d.valor_real,
    d.valor_disponible AS valor_disponible_detalle,
    h.estado_aprobado,
    d.valor_disponible AS variacion,
    h.rango_periodo,
    h.fecha_inicia,
    h.fecha_finaliza
   FROM public.pst_config_presupuesto_dtl d
     JOIN public.pst_config_presupuesto_hdr h
       ON h.company_id = d.company_id
      AND h.id_presupuesto = d.id_presupuesto
  ORDER BY d.id_presupuesto, d.con_cuenta_code;

DROP VIEW IF EXISTS public.vw_pst_gestion_actividad_presupuesto;

-- El plan de cuentas se llama con_plan_cuenta o con_plan_cuentas según la base;
-- se resuelve el nombre real igual que en fn_pst_resolver_cuenta_code.
DO $$
DECLARE
    v_plan_table text;
BEGIN
    IF to_regclass('public.con_plan_cuenta') IS NOT NULL THEN
        v_plan_table := 'con_plan_cuenta';
    ELSIF to_regclass('public.con_plan_cuentas') IS NOT NULL THEN
        v_plan_table := 'con_plan_cuentas';
    ELSE
        RAISE EXCEPTION 'No existe con_plan_cuenta ni con_plan_cuentas; no se puede crear vw_pst_gestion_actividad_presupuesto.';
    END IF;

    EXECUTE format($vista$
CREATE VIEW public.vw_pst_gestion_actividad_presupuesto AS
SELECT
    a.actividad_id,
    a.company_id,
    a.id_presupuesto,
    h.rango_periodo,
    h.fecha_inicia,
    h.fecha_finaliza,
    h.estado_aprobado AS estado_presupuesto,

    a.tipo_actividad,
    a.estado AS estado_actividad,
    a.fecha_actividad,

    a.cuenta_origen_code,
    cpo.name AS cuenta_origen_nombre,
    a.cuenta_destino_code,
    cpd.name AS cuenta_destino_nombre,

    a.monto,
    d_origen.valor_proyeccion AS origen_proyeccion_actual,
    d_destino.valor_proyeccion AS destino_proyeccion_actual,

    CASE
        WHEN a.tipo_actividad = 'TRASLADO'
            THEN COALESCE(d_origen.valor_proyeccion, 0) - a.monto
        ELSE NULL
    END AS origen_proyeccion_resultante,

    COALESCE(d_destino.valor_proyeccion, 0) + a.monto AS destino_proyeccion_resultante,

    CASE
        WHEN a.tipo_actividad = 'AMPLIACION'
             AND a.estado IN ('PENDIENTE', 'APROBADA')
             AND h.estado_aprobado = TRUE
            THEN TRUE
        WHEN a.tipo_actividad = 'TRASLADO'
             AND a.estado IN ('PENDIENTE', 'APROBADA')
             AND h.estado_aprobado = TRUE
             AND COALESCE(d_origen.valor_proyeccion, 0) >= a.monto
            THEN TRUE
        ELSE FALSE
    END AS puede_aplicar,

    a.motivo,
    a.referencia,
    a.created_by,
    a.created_at,
    a.approved_by,
    a.approved_at,
    a.applied_by,
    a.applied_at
FROM public.pst_actividad_presupuesto a
JOIN public.pst_config_presupuesto_hdr h
  ON h.company_id = a.company_id
 AND h.id_presupuesto = a.id_presupuesto
LEFT JOIN public.pst_config_presupuesto_dtl d_origen
  ON d_origen.company_id = a.company_id
 AND d_origen.id_presupuesto = a.id_presupuesto
 AND d_origen.con_cuenta_code = a.cuenta_origen_code
LEFT JOIN public.pst_config_presupuesto_dtl d_destino
  ON d_destino.company_id = a.company_id
 AND d_destino.id_presupuesto = a.id_presupuesto
 AND d_destino.con_cuenta_code = a.cuenta_destino_code
LEFT JOIN public.%I cpo
  ON cpo.company_id = a.company_id
 AND UPPER(cpo.code) = UPPER(a.cuenta_origen_code)
LEFT JOIN public.%I cpd
  ON cpd.company_id = a.company_id
 AND UPPER(cpd.code) = UPPER(a.cuenta_destino_code)
$vista$, v_plan_table, v_plan_table);
END;
$$;

COMMENT ON VIEW public.vw_pst_gestion_actividad_presupuesto IS
'Vista de gestion para ampliaciones y traslados de presupuesto por cuenta (filtrada por empresa).';

COMMIT;
