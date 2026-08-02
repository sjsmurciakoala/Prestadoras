-- 2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql
-- fn_pst_next_id_presupuesto_dtl: soporta ids de presupuesto NO numericos.
--   * La version anterior (2026-07-24_presupuesto_multitenant_company_id.sql, seccion 6.1)
--     exigia id_presupuesto numerico para derivar la semilla del correlativo
--     ('60000' -> 60001, 60002, ...). Los datos reales usan ids como 'PRE-2025' /
--     'PRE-2026' (correlativos ya poblados 1..224), por lo que agregar un detalle
--     nuevo lanzaba excepcion. Problema pre-existente al cambio multitenant.
--   * Nueva semilla: MAX(id_presupuesto_dtl) por (company_id, id_presupuesto);
--     si el id es numerico se conserva su semilla historica como piso (GREATEST),
--     si no lo es la semilla base es 0. La unicidad la garantiza el indice
--     ux_pst_config_presupuesto_dtl_id_presupuesto_dtl (company_id, id_presupuesto,
--     id_presupuesto_dtl).
-- Requiere: 2026-07-24_presupuesto_multitenant_company_id.sql aplicado antes
-- (firma con company_id e indice unico compuesto).
-- Solo redefine la funcion; no altera tablas ni datos. Reejecutable (idempotente).

BEGIN;

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
       OR btrim(p_id_presupuesto) = '' THEN
        RAISE EXCEPTION 'id_presupuesto es requerido para generar id_presupuesto_dtl.';
    END IF;

    -- Ids numericos conservan su semilla historica como piso ('60000' -> 60001...);
    -- ids no numericos ('PRE-2026') arrancan del MAX existente (semilla 0 si esta vacio).
    v_base_id := CASE
        WHEN btrim(p_id_presupuesto) ~ '^[0-9]{1,18}$'
            THEN btrim(p_id_presupuesto)::bigint
        ELSE 0
    END;

    -- Evita colisiones concurrentes por la misma (empresa, presupuesto).
    PERFORM pg_advisory_xact_lock(
        hashtext(p_company_id::text || '|' || btrim(p_id_presupuesto))::bigint
    );

    SELECT GREATEST(COALESCE(MAX(d.id_presupuesto_dtl), 0), v_base_id) + 1
      INTO v_next_id
      FROM public.pst_config_presupuesto_dtl d
     WHERE d.company_id = p_company_id
       AND d.id_presupuesto = p_id_presupuesto;

    RETURN v_next_id;
END;
$$;

COMMIT;

-- ¿Ya aplicado? (debe devolver 225 con los datos actuales de la empresa 2, sin error):
-- SELECT public.fn_pst_next_id_presupuesto_dtl(2::bigint, 'PRE-2026'::varchar);
-- Verificacion posterior (no persiste nada):
-- BEGIN;
-- SELECT public.fn_pst_next_id_presupuesto_dtl(2::bigint, 'PRE-2025'::varchar); -- 115
-- SELECT public.fn_pst_next_id_presupuesto_dtl(2::bigint, '60000'::varchar);    -- 60001 (compatibilidad numerica)
-- ROLLBACK;
