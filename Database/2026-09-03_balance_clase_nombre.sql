-- El balance nombra sus clases (2026-09-03).
--
-- El juego impreso separa activo corriente de no corriente, y pasivo corriente de no corriente,
-- con un subtotal por cada uno. Esa division YA existe en el dato -la columna clase, 1 a 8- pero
-- solo como numero: el reporte no tenia como titular cada bloque.
--
-- Se agrega clase_nombre, derivado del numero en el mismo CASE que ya deriva seccion_nombre. Se
-- hace en la base y no en el reporte a proposito: el vocabulario del balance -que es activo
-- corriente y que no- pertenece a la contabilidad, no a la presentacion, y repartirlo entre los
-- dos sitios garantiza que un dia dejen de coincidir.
--
-- Va junto con la exposicion de monto_anterior en la sobrecarga larga
-- (2026-09-03_balance_monto_anterior.sql), que este script incluye: al cambiar la firma de la
-- corta hay que recrear las dos.
--
-- ADITIVO / bajo riesgo. Cambia la firma de RETURNS TABLE de AMBAS sobrecargas, y Postgres no
-- admite CREATE OR REPLACE con distinto tipo de retorno: se eliminan primero -cada una por su
-- lista de argumentos- y se recrean completas.

BEGIN;

-- La larga primero: depende de la corta.
DROP FUNCTION IF EXISTS public.rep_estado_situacion_financiera(
    bigint, date, bigint, boolean, boolean, boolean, boolean, boolean, boolean, boolean);
DROP FUNCTION IF EXISTS public.rep_estado_situacion_financiera(bigint, date);

CREATE OR REPLACE FUNCTION public.rep_estado_situacion_financiera(p_company_id bigint, p_fecha_corte date)
 RETURNS TABLE(seccion_orden integer, seccion_nombre text, clase smallint, clase_nombre text, linea_orden integer, codigo_cuenta character varying, descripcion text, descripcion_mostrar text, monto numeric, monto_anterior numeric, porcentaje_activo numeric, mostrar_en_reporte boolean)
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_fecha_anterior date;
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;

    IF p_fecha_corte IS NULL THEN
        RAISE EXCEPTION 'El parametro p_fecha_corte es obligatorio.';
    END IF;

    v_fecha_anterior := (p_fecha_corte - INTERVAL '1 year')::date;

    RETURN QUERY
    WITH balance_actual AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            p_fecha_corte,
            p_fecha_corte,
            TRUE
        )
    ),
    balance_anterior AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            v_fecha_anterior,
            v_fecha_anterior,
            TRUE
        )
    ),
    configuracion AS
    (
        SELECT
            b.numero_linea,
            b.clase,
            b.codigo_cuenta,
            NULLIF(regexp_replace(COALESCE(b.codigo_cuenta, ''), '[./\s-]', '', 'g'), '') AS codigo_normalizado,
            COALESCE(
                NULLIF(btrim(b.descripcion_linea), ''),
                NULLIF(btrim(b.descripcion_cuenta), ''),
                COALESCE(b.codigo_cuenta, '')
            )::text AS descripcion,
            COALESCE(b.porcentaje_activo, 0)::numeric(18,4) AS porcentaje_activo,
            b.mostrar_en_reporte
        FROM public.con_configuracion_balance b
        WHERE b.company_id = p_company_id
          AND b.mostrar_en_reporte
    )
    SELECT
        CASE
            WHEN cfg.clase IN (1, 2) THEN 10
            WHEN cfg.clase IN (3, 4) THEN 20
            WHEN cfg.clase IN (5, 6) THEN 30
            WHEN cfg.clase = 7 THEN 50
            WHEN cfg.clase = 8 THEN 60
            ELSE 99
        END AS seccion_orden,
        CASE
            WHEN cfg.clase IN (1, 2) THEN 'ACTIVO'
            WHEN cfg.clase IN (3, 4) THEN 'PASIVO'
            WHEN cfg.clase IN (5, 6) THEN 'PATRIMONIO'
            WHEN cfg.clase = 7 THEN 'ORDEN'
            WHEN cfg.clase = 8 THEN 'PERCONTRA'
            ELSE 'OTROS'
        END AS seccion_nombre,
        cfg.clase::smallint AS clase,
        -- El nombre de la clase se deriva aqui, junto al de la seccion, para que el balance
        -- diga lo mismo en todas partes. Es la division corriente / no corriente que separa
        -- los subtotales del juego impreso.
        CASE
            WHEN cfg.clase = 1 THEN 'ACTIVO CORRIENTE'
            WHEN cfg.clase = 2 THEN 'ACTIVO NO CORRIENTE'
            WHEN cfg.clase = 3 THEN 'PASIVO CORRIENTE'
            WHEN cfg.clase = 4 THEN 'PASIVO NO CORRIENTE'
            WHEN cfg.clase = 5 THEN 'CAPITAL CONTABLE'
            WHEN cfg.clase = 6 THEN 'RESULTADOS ACUMULADOS'
            WHEN cfg.clase = 7 THEN 'CUENTAS DE ORDEN'
            WHEN cfg.clase = 8 THEN 'CUENTAS PER CONTRA'
            ELSE 'OTROS'
        END AS clase_nombre,
        cfg.numero_linea::integer AS linea_orden,
        cfg.codigo_cuenta,
        cfg.descripcion,
        cfg.descripcion AS descripcion_mostrar,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.clase IN (1, 2) THEN bal_act.saldo_actual
                    ELSE bal_act.saldo_actual * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.clase IN (1, 2) THEN bal_ant.saldo_actual
                    ELSE bal_ant.saldo_actual * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto_anterior,
        cfg.porcentaje_activo,
        cfg.mostrar_en_reporte
    FROM configuracion cfg
    LEFT JOIN balance_actual bal_act
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal_act.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    LEFT JOIN balance_anterior bal_ant
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal_ant.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    ORDER BY
        seccion_orden,
        cfg.numero_linea,
        cfg.codigo_cuenta;
END;
$function$;

CREATE OR REPLACE FUNCTION public.rep_estado_situacion_financiera(p_company_id bigint, p_fecha_corte date, p_nivel_jerarquia bigint, p_incluir_saldo_cero boolean, p_incluir_fecha_pagina boolean, p_enumerar_paginas boolean, p_ajuste_fiscal boolean, p_ajustado_inflacion boolean, p_incluir_codigo_cuenta boolean, p_orientacion_horizontal boolean)
 RETURNS TABLE(empresa_nombre text, empresa_nombre_legal text, empresa_rtn text, empresa_email text, empresa_telefono text, empresa_direccion text, empresa_logo bytea, empresa_logo_mime text, moneda_codigo text, fecha_corte date, periodo_codigo text, periodo_nombre text, nivel_jerarquia bigint, incluir_saldo_cero boolean, incluir_fecha_pagina boolean, enumerar_paginas boolean, ajuste_fiscal boolean, ajustado_inflacion boolean, incluir_codigo_cuenta boolean, orientacion_horizontal boolean, seccion_orden integer, seccion_nombre text, clase smallint, clase_nombre text, linea_orden integer, codigo_cuenta character varying, codigo_cuenta_mostrar text, nivel_cuenta smallint, descripcion text, descripcion_mostrar text, monto numeric, monto_anterior numeric, porcentaje_activo numeric, mostrar_en_reporte boolean)
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
    b.clase_nombre,
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
--   SELECT seccion_nombre, clase, clase_nombre, count(*)
--   FROM public.rep_estado_situacion_financiera(2, current_date)
--   GROUP BY 1, 2, 3 ORDER BY 2;
--
-- Debe listar ACTIVO CORRIENTE, ACTIVO NO CORRIENTE, PASIVO CORRIENTE, PASIVO NO CORRIENTE y
-- CAPITAL CONTABLE, cada uno con sus cuentas.
