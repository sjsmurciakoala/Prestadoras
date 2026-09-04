-- Comparativo de presupuesto: ejercicio base contra el siguiente (2026-09-03).
--
-- El juego impreso incluye un comparativo que hoy no existe como informe: por cada cuenta, lo
-- presupuestado y lo ejecutado del ejercicio en curso, y lo presupuestado del siguiente.
--
-- vw_pst_ejecucion_presupuestaria ya trae presupuesto y ejecutado, pero de UN presupuesto a la
-- vez. Esta funcion pone los dos ejercicios en la misma fila, que es lo que necesita el informe
-- para restarlos: con una fila por ejercicio habria que cruzarlos en el reporte, y eso es trabajo
-- de la consulta.
--
-- Las diferencias y los porcentajes NO se calculan aqui: los tres importes quedan en la misma
-- fila y el reporte los resta, igual que en los demas estados. Tenerlos en los dos sitios
-- garantiza que un dia dejen de coincidir.
--
-- ADITIVO / bajo riesgo: crea una funcion y registra el dataset y el informe. No toca nada
-- existente.

BEGIN;

CREATE OR REPLACE FUNCTION public.rep_presupuesto_comparativo(
    p_company_id bigint,
    p_anio_base bigint)
RETURNS TABLE(
    empresa_nombre text,
    empresa_nombre_legal text,
    empresa_direccion text,
    anio_base integer,
    anio_siguiente integer,
    seccion_orden integer,
    seccion_nombre text,
    cuenta_codigo text,
    cuenta_nombre text,
    presupuestado_base numeric,
    ejecutado_base numeric,
    presupuestado_siguiente numeric)
LANGUAGE sql
STABLE
AS $function$
    WITH parametros AS (
        SELECT
            p_company_id                                        AS company_id,
            COALESCE(p_anio_base, EXTRACT(YEAR FROM CURRENT_DATE)::bigint)::integer AS base
    ),
    empresa AS (
        SELECT
            COALESCE(NULLIF(btrim(co.commercial_name), ''), '')::text AS nombre,
            COALESCE(NULLIF(btrim(co.legal_name), ''), '')::text      AS nombre_legal,
            COALESCE(NULLIF(btrim(co.address), ''), '')::text         AS direccion
        FROM public.cfg_company co
        CROSS JOIN parametros p
        WHERE co.company_id = p.company_id
    ),
    -- Un presupuesto por ejercicio: el que abarca el 1 de enero de ese anio.
    ejercicio AS (
        SELECT
            v.id_presupuesto,
            EXTRACT(YEAR FROM v.fecha_inicia)::integer AS anio,
            v.con_cuenta_code::text                    AS cuenta_codigo,
            v.cuenta_nombre::text                      AS cuenta_nombre,
            v.cuenta_tipo::text                        AS cuenta_tipo,
            SUM(v.presupuesto)::numeric(18,2)          AS presupuesto,
            SUM(v.ejecutado)::numeric(18,2)            AS ejecutado
        FROM public.vw_pst_ejecucion_presupuestaria v
        CROSS JOIN parametros p
        WHERE v.company_id = p.company_id
          AND EXTRACT(YEAR FROM v.fecha_inicia)::integer IN (p.base, p.base + 1)
        GROUP BY 1, 2, 3, 4, 5
    ),
    -- Todas las cuentas de cualquiera de los dos ejercicios: una cuenta nueva del ejercicio
    -- siguiente tambien tiene que aparecer, con el base en cero.
    cuentas AS (
        SELECT DISTINCT e.cuenta_codigo, e.cuenta_nombre, e.cuenta_tipo
        FROM ejercicio e
    )
    SELECT
        em.nombre,
        em.nombre_legal,
        em.direccion,
        p.base                  AS anio_base,
        p.base + 1              AS anio_siguiente,
        CASE c.cuenta_tipo
            WHEN 'INGRESO' THEN 10
            WHEN 'COSTO'   THEN 20
            WHEN 'GASTO'   THEN 30
            WHEN 'CAPITAL' THEN 40
            ELSE 99
        END                     AS seccion_orden,
        CASE c.cuenta_tipo
            WHEN 'INGRESO' THEN 'INGRESOS'
            WHEN 'COSTO'   THEN 'COSTOS'
            WHEN 'GASTO'   THEN 'GASTOS'
            WHEN 'CAPITAL' THEN 'CAPITAL'
            ELSE 'OTROS'
        END                     AS seccion_nombre,
        c.cuenta_codigo,
        c.cuenta_nombre,
        COALESCE(b.presupuesto, 0)::numeric(18,2)  AS presupuestado_base,
        COALESCE(b.ejecutado, 0)::numeric(18,2)    AS ejecutado_base,
        COALESCE(s.presupuesto, 0)::numeric(18,2)  AS presupuestado_siguiente
    FROM cuentas c
    CROSS JOIN parametros p
    CROSS JOIN empresa em
    LEFT JOIN ejercicio b ON b.cuenta_codigo = c.cuenta_codigo AND b.anio = p.base
    LEFT JOIN ejercicio s ON s.cuenta_codigo = c.cuenta_codigo AND s.anio = p.base + 1
    -- Una cuenta sin cifra en ninguno de los dos ejercicios no aporta nada al comparativo.
    WHERE COALESCE(b.presupuesto, 0) <> 0
       OR COALESCE(b.ejecutado, 0) <> 0
       OR COALESCE(s.presupuesto, 0) <> 0
    ORDER BY 6, 8;
$function$;

-- Dataset e informe del catalogo, por empresa.
INSERT INTO public.rep_catalogo_dataset
    (company_id, codigo, nombre, descripcion, tipo_origen, origen_clave, is_active, created_at, created_by)
SELECT c.company_id,
       'presupuesto-comparativo',
       'Dataset comparativo de presupuesto',
       'Presupuestado y ejecutado del ejercicio base contra lo presupuestado del siguiente.',
       'STORED_PROCEDURE',
       'public.rep_presupuesto_comparativo',
       true,
       now(),
       'presupuesto-comparativo'
FROM public.cfg_company c
WHERE NOT EXISTS (
    SELECT 1 FROM public.rep_catalogo_dataset d
    WHERE d.company_id = c.company_id AND d.codigo = 'presupuesto-comparativo');

-- Los parametros siguen la forma del resto de los estados: la empresa la pone el portal
-- (CURRENT_COMPANY) y el anio lo pide el reporte.
INSERT INTO public.rep_dataset_parametro
    (company_id, dataset_id, nombre, etiqueta, tipo_dato, fuente_valor,
     visible, permite_nulo, requerido, orden, nombre_origen, created_by)
SELECT d.company_id, d.dataset_id, v.nombre, v.etiqueta, v.tipo, v.fuente,
       v.visible, false, true, v.orden, v.origen, 'presupuesto-comparativo'
FROM public.rep_catalogo_dataset d
CROSS JOIN (VALUES
        ('CompanyId', 'Empresa actual',   'INT64', 'CURRENT_COMPANY', false,  0, 'p_company_id'),
        ('AnioBase',  'Anio del ejercicio', 'INT64', 'REPORT',         true,  10, 'p_anio_base')
) AS v(nombre, etiqueta, tipo, fuente, visible, orden, origen)
WHERE d.codigo = 'presupuesto-comparativo'
  AND NOT EXISTS (
      SELECT 1 FROM public.rep_dataset_parametro p
      WHERE p.dataset_id = d.dataset_id AND p.nombre = v.nombre);

INSERT INTO public.rep_catalogo_informe
    (company_id, codigo, nombre, descripcion, categoria, tipo_origen, ruta, consulta_clave,
     icono_css_class, orden, permite_exportar, permite_imprimir, is_active, created_at, created_by)
SELECT c.company_id,
       'presupuesto-comparativo',
       'Comparativo de presupuesto',
       'Presupuestado y ejecutado del ejercicio contra lo presupuestado del siguiente.',
       'Contabilidad',
       'REPORT',
       '/informes/reportes/presupuesto-comparativo/viewer',
       'presupuesto-comparativo',
       'bi bi-bar-chart-line',
       55,
       true,
       true,
       true,
       now(),
       'presupuesto-comparativo'
FROM public.cfg_company c
WHERE NOT EXISTS (
    SELECT 1 FROM public.rep_catalogo_informe i
    WHERE i.company_id = c.company_id AND i.codigo = 'presupuesto-comparativo');

COMMIT;

-- Verificacion:
--   SELECT seccion_nombre, count(*), sum(presupuestado_base), sum(ejecutado_base),
--          sum(presupuestado_siguiente)
--   FROM public.rep_presupuesto_comparativo(2, 2025)
--   GROUP BY 1 ORDER BY 1;
--
-- Debe listar INGRESOS, COSTOS y GASTOS con sus cifras de los dos ejercicios.
