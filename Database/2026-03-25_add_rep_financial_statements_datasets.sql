BEGIN;

DROP FUNCTION IF EXISTS public.rep_estado_situacion_financiera(bigint, date);
DROP FUNCTION IF EXISTS public.rep_estado_resultados(bigint, date, date);

CREATE OR REPLACE FUNCTION public.rep_estado_situacion_financiera(
    p_company_id bigint,
    p_fecha_corte date
)
RETURNS TABLE
(
    seccion_orden integer,
    seccion_nombre text,
    clase smallint,
    linea_orden integer,
    codigo_cuenta character varying(30),
    descripcion text,
    descripcion_mostrar text,
    monto numeric(18,2),
    porcentaje_activo numeric(18,4),
    mostrar_en_reporte boolean
)
LANGUAGE plpgsql
STABLE
AS
$$
BEGIN
    IF COALESCE(p_company_id, 0) <= 0 THEN
        RAISE EXCEPTION 'El parametro p_company_id es obligatorio.';
    END IF;

    IF p_fecha_corte IS NULL THEN
        RAISE EXCEPTION 'El parametro p_fecha_corte es obligatorio.';
    END IF;

    RETURN QUERY
    WITH balance AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            p_fecha_corte,
            p_fecha_corte,
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
            WHEN cfg.clase = 5 THEN 30
            WHEN cfg.clase = 6 THEN 40
            WHEN cfg.clase = 7 THEN 50
            WHEN cfg.clase = 8 THEN 60
            ELSE 99
        END AS seccion_orden,
        CASE
            WHEN cfg.clase IN (1, 2) THEN 'Activos'
            WHEN cfg.clase IN (3, 4) THEN 'Pasivos'
            WHEN cfg.clase = 5 THEN 'Capital'
            WHEN cfg.clase = 6 THEN 'Pasivo y Capital'
            WHEN cfg.clase = 7 THEN 'Orden'
            WHEN cfg.clase = 8 THEN 'Percontra'
            ELSE 'Otros'
        END AS seccion_nombre,
        cfg.clase::smallint AS clase,
        cfg.numero_linea::integer AS linea_orden,
        cfg.codigo_cuenta,
        cfg.descripcion,
        cfg.descripcion AS descripcion_mostrar,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.clase IN (1, 2) THEN bal.saldo_actual
                    ELSE bal.saldo_actual * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto,
        cfg.porcentaje_activo,
        cfg.mostrar_en_reporte
    FROM configuracion cfg
    LEFT JOIN balance bal
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    ORDER BY
        seccion_orden,
        cfg.numero_linea,
        cfg.codigo_cuenta;
END;
$$;

COMMENT ON FUNCTION public.rep_estado_situacion_financiera(bigint, date)
IS 'Estado de situacion financiera configurado por empresa a partir de con_configuracion_balance y saldos contables.';

CREATE OR REPLACE FUNCTION public.rep_estado_resultados(
    p_company_id bigint,
    p_fecha_desde date,
    p_fecha_hasta date
)
RETURNS TABLE
(
    seccion_orden integer,
    seccion_nombre text,
    linea_orden integer,
    tipo_linea smallint,
    codigo_cuenta character varying(30),
    descripcion text,
    descripcion_mostrar text,
    nivel_indentacion smallint,
    mostrar_subtotal boolean,
    monto numeric(18,2),
    monto_neto numeric(18,2)
)
LANGUAGE plpgsql
STABLE
AS
$$
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

    RETURN QUERY
    WITH balance AS
    (
        SELECT *
        FROM public.rep_balance_comprobacion(
            p_company_id,
            p_fecha_desde,
            p_fecha_hasta,
            TRUE
        )
    ),
    configuracion AS
    (
        SELECT
            r.numero_linea,
            r.tipo_linea,
            r.codigo_cuenta,
            NULLIF(regexp_replace(COALESCE(r.codigo_cuenta, ''), '[./\s-]', '', 'g'), '') AS codigo_normalizado,
            COALESCE(
                NULLIF(btrim(r.descripcion_linea), ''),
                COALESCE(r.codigo_cuenta, '')
            )::text AS descripcion,
            COALESCE(r.nivel_indentacion, 0)::smallint AS nivel_indentacion,
            r.mostrar_subtotal
        FROM public.con_configuracion_linea_resultado r
        WHERE r.company_id = p_company_id
    )
    SELECT
        CASE cfg.tipo_linea
            WHEN 0 THEN 10
            WHEN 1 THEN 20
            WHEN 2 THEN 30
            ELSE 99
        END AS seccion_orden,
        CASE cfg.tipo_linea
            WHEN 0 THEN 'Ingresos'
            WHEN 1 THEN 'Costos'
            WHEN 2 THEN 'Gastos'
            ELSE 'Otros'
        END AS seccion_nombre,
        cfg.numero_linea::integer AS linea_orden,
        cfg.tipo_linea::smallint AS tipo_linea,
        cfg.codigo_cuenta,
        cfg.descripcion,
        concat(repeat('  ', GREATEST(cfg.nivel_indentacion::integer, 0)), cfg.descripcion)::text AS descripcion_mostrar,
        cfg.nivel_indentacion,
        cfg.mostrar_subtotal,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo - bal.debitos_periodo
                    ELSE bal.debitos_periodo - bal.creditos_periodo
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto,
        ROUND(
            COALESCE(
                CASE
                    WHEN cfg.tipo_linea = 0 THEN bal.creditos_periodo - bal.debitos_periodo
                    ELSE (bal.debitos_periodo - bal.creditos_periodo) * -1
                END,
                0
            ),
            2
        )::numeric(18,2) AS monto_neto
    FROM configuracion cfg
    LEFT JOIN balance bal
      ON cfg.codigo_normalizado IS NOT NULL
     AND regexp_replace(COALESCE(bal.cuenta_codigo, ''), '[./\s-]', '', 'g') = cfg.codigo_normalizado
    ORDER BY
        seccion_orden,
        cfg.numero_linea,
        cfg.codigo_cuenta;
END;
$$;

COMMENT ON FUNCTION public.rep_estado_resultados(bigint, date, date)
IS 'Estado de resultados configurado por empresa a partir de con_configuracion_linea_resultado y movimientos del periodo.';

INSERT INTO public.rep_catalogo_dataset
(
    company_id,
    codigo,
    nombre,
    descripcion,
    tipo_origen,
    origen_clave,
    sql_text,
    connection_name,
    is_active,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    c.company_id,
    v.codigo,
    v.nombre,
    v.descripcion,
    'STORED_PROCEDURE',
    v.origen_clave,
    NULL,
    'DefaultConnection',
    TRUE,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.cfg_company c
JOIN
(
    VALUES
        ('estado-situacion-financiera', 'Estado de situacion financiera', 'Dataset configurado por empresa para el estado de situacion financiera.', 'public.rep_estado_situacion_financiera'),
        ('estado-resultados', 'Estado de resultados', 'Dataset configurado por empresa para el estado de resultados.', 'public.rep_estado_resultados')
) AS v(codigo, nombre, descripcion, origen_clave)
  ON true
WHERE NOT EXISTS
(
    SELECT 1
    FROM public.rep_catalogo_dataset d
    WHERE d.company_id = c.company_id
      AND d.codigo = v.codigo
);

UPDATE public.rep_catalogo_dataset d
SET
    nombre = v.nombre,
    descripcion = v.descripcion,
    tipo_origen = 'STORED_PROCEDURE',
    origen_clave = v.origen_clave,
    sql_text = NULL,
    connection_name = 'DefaultConnection',
    is_active = TRUE,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
FROM
(
    VALUES
        ('estado-situacion-financiera', 'Estado de situacion financiera', 'Dataset configurado por empresa para el estado de situacion financiera.', 'public.rep_estado_situacion_financiera'),
        ('estado-resultados', 'Estado de resultados', 'Dataset configurado por empresa para el estado de resultados.', 'public.rep_estado_resultados')
) AS v(codigo, nombre, descripcion, origen_clave)
WHERE d.codigo = v.codigo;

INSERT INTO public.rep_dataset_parametro
(
    company_id,
    dataset_id,
    nombre,
    nombre_origen,
    etiqueta,
    tipo_dato,
    fuente_valor,
    valor_default,
    visible,
    permite_nulo,
    requerido,
    orden,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    d.company_id,
    d.dataset_id,
    p.nombre,
    p.nombre_origen,
    p.etiqueta,
    p.tipo_dato,
    p.fuente_valor,
    p.valor_default,
    p.visible,
    p.permite_nulo,
    p.requerido,
    p.orden,
    NOW(),
    'reporteria-bootstrap',
    NOW(),
    'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
JOIN
(
    VALUES
        ('estado-situacion-financiera', 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('estado-situacion-financiera', 'FechaCorte', 'p_fecha_corte', 'Fecha de corte', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('estado-resultados', 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('estado-resultados', 'FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('estado-resultados', 'FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 20)
) AS p(dataset_codigo, nombre, nombre_origen, etiqueta, tipo_dato, fuente_valor, valor_default, visible, permite_nulo, requerido, orden)
  ON d.codigo = p.dataset_codigo
WHERE NOT EXISTS
(
    SELECT 1
    FROM public.rep_dataset_parametro x
    WHERE x.company_id = d.company_id
      AND x.dataset_id = d.dataset_id
      AND x.nombre = p.nombre
);

UPDATE public.rep_dataset_parametro p
SET
    nombre_origen = v.nombre_origen,
    etiqueta = v.etiqueta,
    tipo_dato = v.tipo_dato,
    fuente_valor = v.fuente_valor,
    valor_default = v.valor_default,
    visible = v.visible,
    permite_nulo = v.permite_nulo,
    requerido = v.requerido,
    orden = v.orden,
    updated_at = NOW(),
    updated_by = 'reporteria-bootstrap'
FROM public.rep_catalogo_dataset d
JOIN
(
    VALUES
        ('estado-situacion-financiera', 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('estado-situacion-financiera', 'FechaCorte', 'p_fecha_corte', 'Fecha de corte', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('estado-resultados', 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, FALSE, FALSE, TRUE, 0),
        ('estado-resultados', 'FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 10),
        ('estado-resultados', 'FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, TRUE, FALSE, TRUE, 20)
) AS v(dataset_codigo, nombre, nombre_origen, etiqueta, tipo_dato, fuente_valor, valor_default, visible, permite_nulo, requerido, orden)
  ON d.codigo = v.dataset_codigo
WHERE d.company_id = p.company_id
  AND d.dataset_id = p.dataset_id
  AND p.nombre = v.nombre;

COMMIT;

SELECT
    d.company_id,
    d.codigo,
    d.nombre,
    d.origen_clave,
    p.nombre AS parametro,
    p.tipo_dato,
    p.fuente_valor
FROM public.rep_catalogo_dataset d
LEFT JOIN public.rep_dataset_parametro p
  ON p.company_id = d.company_id
 AND p.dataset_id = d.dataset_id
WHERE d.codigo IN ('estado-situacion-financiera', 'estado-resultados')
ORDER BY d.company_id, d.codigo, p.orden, p.nombre;
