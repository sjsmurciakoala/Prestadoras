CREATE OR REPLACE FUNCTION public.rep_auxiliar_lectura(
    p_company_id bigint,
    p_anio integer,
    p_mes integer,
    p_ciclo_id integer DEFAULT 0,
    p_solo_pendientes boolean DEFAULT false
)
RETURNS TABLE (
    fila_orden bigint,
    ruta_codigo text,
    ruta_titulo text,
    secuencia text,
    clave text,
    cliente_nombre text,
    contador text,
    lectura_anterior numeric(18,2),
    lectura_actual numeric(18,2),
    consumo numeric(18,2),
    fecha_lectura date,
    usuario text,
    condicion text,
    empresa_nombre text,
    periodo_titulo text,
    fecha_reporte date,
    fecha_reporte_texto text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_empresa_nombre text;
    v_ciclo_codigo text;
    v_periodo_titulo text;
BEGIN
    SELECT COALESCE(NULLIF(TRIM(legal_name), ''), NULLIF(TRIM(commercial_name), ''), 'EMPRESA')
      INTO v_empresa_nombre
      FROM public.cfg_company
     WHERE company_id = p_company_id
     LIMIT 1;

    IF p_ciclo_id > 0 THEN
        SELECT NULLIF(TRIM(ciclos_codigo), '')
          INTO v_ciclo_codigo
          FROM public.ciclos
         WHERE ciclos_id = p_ciclo_id
         LIMIT 1;
    END IF;

    v_periodo_titulo :=
        'Auxiliar de Lectura del periodo '
        || LPAD(p_mes::text, 2, '0')
        || '/'
        || p_anio::text
        || CASE
            WHEN v_ciclo_codigo IS NULL THEN ''
            ELSE ' - Ciclo ' || v_ciclo_codigo
           END
        || CASE
            WHEN p_solo_pendientes THEN ' (solo pendientes)'
            ELSE ''
           END;

    RETURN QUERY
    WITH base AS (
        SELECT
            TRIM(COALESCE(h.ruta, '')) AS ruta_codigo,
            TRIM(COALESCE(h.secuencia, '')) AS secuencia,
            TRIM(COALESCE(h.clave, '')) AS clave,
            TRIM(COALESCE(NULLIF(h.propietario, ''), cm.maestro_cliente_nombre, '')) AS cliente_nombre,
            TRIM(COALESCE(h.contador, '')) AS contador,
            COALESCE(h.lect_ant, 0)::numeric(18,2) AS lectura_anterior,
            COALESCE(h.lect_act, 0)::numeric(18,2) AS lectura_actual,
            COALESCE(h.consumo, 0)::numeric(18,2) AS consumo,
            COALESCE(h.fecha_lect_act, h.fecha) AS fecha_lectura,
            TRIM(COALESCE(h.usuario, '')) AS usuario,
            TRIM(COALESCE(h.condicion, '')) AS condicion
        FROM public.historicomedicion h
        LEFT JOIN public.cliente_maestro cm
          ON cm.company_id = h.company_id
         AND cm.maestro_cliente_clave = h.clave
        WHERE h.company_id = p_company_id
          AND h.ano = p_anio::numeric
          AND h.mes = p_mes::numeric
          AND (
                v_ciclo_codigo IS NULL
                OR TRIM(COALESCE(h.ciclo, '')) = v_ciclo_codigo
                OR TRIM(COALESCE(h.ciclo, '')) = TRIM(LEADING '0' FROM v_ciclo_codigo)
              )
          AND (
                NOT p_solo_pendientes
                OR COALESCE(TRIM(h.usuario), '') = ''
              )
    ),
    ordenado AS (
        SELECT
            ROW_NUMBER() OVER (
                ORDER BY
                    NULLIF(base.ruta_codigo, ''),
                    CASE
                        WHEN REGEXP_REPLACE(COALESCE(base.secuencia, ''), '[^0-9]', '', 'g') = '' THEN NULL
                        ELSE REGEXP_REPLACE(COALESCE(base.secuencia, ''), '[^0-9]', '', 'g')::integer
                    END,
                    base.clave
            ) AS fila_orden,
            base.ruta_codigo,
            CASE
                WHEN COALESCE(base.ruta_codigo, '') = '' THEN 'RUTA: SIN DEFINIR'
                ELSE 'RUTA: ' || base.ruta_codigo
            END AS ruta_titulo,
            base.secuencia,
            base.clave,
            base.cliente_nombre,
            base.contador,
            base.lectura_anterior,
            base.lectura_actual,
            base.consumo,
            base.fecha_lectura,
            base.usuario,
            base.condicion
        FROM base
    )
    SELECT
        o.fila_orden,
        o.ruta_codigo,
        o.ruta_titulo,
        o.secuencia,
        o.clave,
        o.cliente_nombre,
        o.contador,
        o.lectura_anterior,
        o.lectura_actual,
        o.consumo,
        o.fecha_lectura,
        o.usuario,
        o.condicion,
        v_empresa_nombre AS empresa_nombre,
        v_periodo_titulo AS periodo_titulo,
        CURRENT_DATE AS fecha_reporte,
        TO_CHAR(CURRENT_DATE, 'DD/MM/YYYY') AS fecha_reporte_texto
    FROM ordenado o
    ORDER BY o.fila_orden;
END;
$$;
