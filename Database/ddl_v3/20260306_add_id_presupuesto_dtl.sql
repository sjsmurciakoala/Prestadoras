-- Agrega id_presupuesto_dtl como correlativo por id_presupuesto.
-- Ejemplo: id_presupuesto=60000 => 60001, 60002, 60003...

ALTER TABLE public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS id_presupuesto_dtl bigint;

CREATE OR REPLACE FUNCTION public.fn_pst_next_id_presupuesto_dtl(
    p_id_presupuesto varchar
)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    v_base_id bigint;
    v_next_id bigint;
BEGIN
    IF p_id_presupuesto IS NULL
       OR btrim(p_id_presupuesto) !~ '^[0-9]+$' THEN
        RAISE EXCEPTION 'id_presupuesto "%" no es numerico. No se puede generar id_presupuesto_dtl.', p_id_presupuesto;
    END IF;

    v_base_id := btrim(p_id_presupuesto)::bigint;

    -- Evita colisiones concurrentes por el mismo id_presupuesto.
    PERFORM pg_advisory_xact_lock(hashtext(btrim(p_id_presupuesto))::bigint);

    SELECT COALESCE(MAX(d.id_presupuesto_dtl), v_base_id) + 1
      INTO v_next_id
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.id_presupuesto = p_id_presupuesto;

    RETURN v_next_id;
END;
$$;

WITH bounds AS (
    SELECT d.id_presupuesto,
           CASE
               WHEN btrim(d.id_presupuesto) ~ '^[0-9]+$' THEN btrim(d.id_presupuesto)::bigint
               ELSE NULL
           END AS base_id,
           MAX(d.id_presupuesto_dtl) AS max_actual
      FROM public.pst_config_presupuesto_dtl d
     GROUP BY d.id_presupuesto
),
pendientes AS (
    SELECT d.id_presupuesto,
           d.con_cuenta_code,
           GREATEST(
               COALESCE(b.max_actual, b.base_id),
               b.base_id
           ) AS semilla,
           ROW_NUMBER() OVER (
               PARTITION BY d.id_presupuesto
               ORDER BY d.con_cuenta_code
           ) AS rn
      FROM public.pst_config_presupuesto_dtl d
      JOIN bounds b
        ON b.id_presupuesto = d.id_presupuesto
     WHERE d.id_presupuesto_dtl IS NULL
       AND b.base_id IS NOT NULL
)
UPDATE public.pst_config_presupuesto_dtl d
   SET id_presupuesto_dtl = p.semilla + p.rn
  FROM pendientes p
 WHERE d.id_presupuesto = p.id_presupuesto
   AND d.con_cuenta_code = p.con_cuenta_code;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM public.pst_config_presupuesto_dtl d
         WHERE d.id_presupuesto_dtl IS NULL
    ) THEN
        RAISE EXCEPTION 'Existen filas sin id_presupuesto_dtl. Verifique que id_presupuesto sea numerico en todos los registros.';
    END IF;
END;
$$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_pst_config_presupuesto_dtl_id_presupuesto_dtl
    ON public.pst_config_presupuesto_dtl (id_presupuesto, id_presupuesto_dtl);

ALTER TABLE public.pst_config_presupuesto_dtl
    ALTER COLUMN id_presupuesto_dtl SET NOT NULL;

DROP TRIGGER IF EXISTS trg_pst_set_id_presupuesto_dtl
    ON public.pst_config_presupuesto_dtl;

DROP FUNCTION IF EXISTS public.trg_pst_set_id_presupuesto_dtl();
