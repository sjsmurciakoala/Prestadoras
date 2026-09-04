-- El balance general expone el ejercicio anterior (2026-09-03).
--
-- La sobrecarga larga de rep_estado_situacion_financiera -la que usa el reporte, con las diez
-- opciones de presentacion- ya llamaba a la corta por CROSS JOIN LATERAL, y esa SI calcula
-- monto_anterior. Simplemente no lo devolvia, asi que el reporte no podia imprimir la columna
-- del anio anterior ni la variacion: el dato estaba ahi, invisible.
--
-- Se agrega monto_anterior a la firma de retorno y al select. Nada mas: el calculo no cambia,
-- porque lo hace la sobrecarga corta y esa no se toca.
--
-- ADITIVO / bajo riesgo, pero OJO: cambia la firma de RETURNS TABLE, y Postgres no admite
-- CREATE OR REPLACE con distinto tipo de retorno. Se elimina primero la sobrecarga larga -solo
-- esa, identificada por su lista de argumentos- y se vuelve a crear completa, para no perder
-- ninguna de las opciones que ya soportaba.

BEGIN;

DROP FUNCTION IF EXISTS public.rep_estado_situacion_financiera(
    bigint, date, bigint, boolean, boolean, boolean, boolean, boolean, boolean, boolean);

CREATE OR REPLACE FUNCTION public.rep_estado_situacion_financiera(p_company_id bigint, p_fecha_corte date, p_nivel_jerarquia bigint, p_incluir_saldo_cero boolean, p_incluir_fecha_pagina boolean, p_enumerar_paginas boolean, p_ajuste_fiscal boolean, p_ajustado_inflacion boolean, p_incluir_codigo_cuenta boolean, p_orientacion_horizontal boolean)
 RETURNS TABLE(empresa_nombre text, empresa_nombre_legal text, empresa_rtn text, empresa_email text, empresa_telefono text, empresa_direccion text, empresa_logo bytea, empresa_logo_mime text, moneda_codigo text, fecha_corte date, periodo_codigo text, periodo_nombre text, nivel_jerarquia bigint, incluir_saldo_cero boolean, incluir_fecha_pagina boolean, enumerar_paginas boolean, ajuste_fiscal boolean, ajustado_inflacion boolean, incluir_codigo_cuenta boolean, orientacion_horizontal boolean, seccion_orden integer, seccion_nombre text, clase smallint, linea_orden integer, codigo_cuenta character varying, codigo_cuenta_mostrar text, nivel_cuenta smallint, descripcion text, descripcion_mostrar text, monto numeric, monto_anterior numeric, porcentaje_activo numeric, mostrar_en_reporte boolean)
 LANGUAGE sql
 STABLE
AS $function$
WITH params AS (
    SELECT
        p_company_id AS company_id,
        (
            date_trunc('month', COALESCE(p_fecha_corte, CURRENT_DATE))::date
            + INTERVAL '1 month - 1 day'
        )::date AS fecha_corte,
        COALESCE(NULLIF(p_nivel_jerarquia, 0), 5) AS nivel_jerarquia,
        COALESCE(p_incluir_saldo_cero, false) AS incluir_saldo_cero,
        COALESCE(p_incluir_fecha_pagina, true) AS incluir_fecha_pagina,
        COALESCE(p_enumerar_paginas, false) AS enumerar_paginas,
        COALESCE(p_ajuste_fiscal, false) AS ajuste_fiscal,
        COALESCE(p_ajustado_inflacion, false) AS ajustado_inflacion,
        COALESCE(p_incluir_codigo_cuenta, true) AS incluir_codigo_cuenta,
        COALESCE(p_orientacion_horizontal, false) AS orientacion_horizontal
),
company AS (
    SELECT
        c.company_id,
        COALESCE(NULLIF(c.commercial_name, ''), NULLIF(c.legal_name, ''), c.code) AS empresa_nombre,
        c.legal_name AS empresa_nombre_legal,
        c.tax_id AS empresa_rtn,
        c.email AS empresa_email,
        c.phone AS empresa_telefono,
        c.address AS empresa_direccion,
        c.logo AS empresa_logo,
        c.logo_mime AS empresa_logo_mime,
        c.currency_code::text AS moneda_codigo
    FROM public.cfg_company c
    JOIN params p ON p.company_id = c.company_id
),
periodo AS (
    SELECT
        pc.company_id,
        pc.code::text AS periodo_codigo,
        pc.name::text AS periodo_nombre
    FROM public.con_periodo_contable pc
    JOIN params p ON p.company_id = pc.company_id
    WHERE p.fecha_corte BETWEEN pc.start_date::date AND pc.end_date::date
    ORDER BY pc.start_date DESC
    LIMIT 1
),
base AS (
    SELECT esf.*
    FROM params p
    CROSS JOIN LATERAL public.rep_estado_situacion_financiera(p.company_id::bigint, p.fecha_corte::date) esf
)
SELECT
    c.empresa_nombre,
    c.empresa_nombre_legal,
    c.empresa_rtn,
    c.empresa_email,
    c.empresa_telefono,
    c.empresa_direccion,
    c.empresa_logo,
    c.empresa_logo_mime,
    c.moneda_codigo,
    p.fecha_corte,
    pe.periodo_codigo,
    pe.periodo_nombre,
    p.nivel_jerarquia,
    p.incluir_saldo_cero,
    p.incluir_fecha_pagina,
    p.enumerar_paginas,
    p.ajuste_fiscal,
    p.ajustado_inflacion,
    p.incluir_codigo_cuenta,
    p.orientacion_horizontal,
    b.seccion_orden,
    b.seccion_nombre,
    b.clase,
    b.linea_orden,
    b.codigo_cuenta,
    CASE WHEN p.incluir_codigo_cuenta THEN b.codigo_cuenta::text ELSE '' END AS codigo_cuenta_mostrar,
    pc.level AS nivel_cuenta,
    b.descripcion,
    b.descripcion_mostrar,
    b.monto,
    b.monto_anterior,
    b.porcentaje_activo,
    b.mostrar_en_reporte
FROM params p
CROSS JOIN company c
LEFT JOIN periodo pe ON pe.company_id = p.company_id
JOIN base b ON true
LEFT JOIN public.con_plan_cuentas pc
       ON pc.company_id = p.company_id
      AND pc.code = b.codigo_cuenta
WHERE (p.incluir_saldo_cero OR COALESCE(b.monto, 0) <> 0)
  AND (pc.level IS NULL OR pc.level <= p.nivel_jerarquia)
ORDER BY b.seccion_orden, b.linea_orden, b.codigo_cuenta;
$function$;

COMMIT;

-- Verificacion:
--   SELECT pg_get_function_result(p.oid) LIKE '%monto_anterior%'
--   FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--   WHERE n.nspname = 'public'
--     AND p.proname = 'rep_estado_situacion_financiera'
--     AND pg_get_function_arguments(p.oid) LIKE '%p_nivel_jerarquia%';
--
-- Debe devolver true.
