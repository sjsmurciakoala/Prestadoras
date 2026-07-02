-- =============================================================================
-- Reportes: Cortes masivos
-- Dataset 1: Listado de clientes para corte
-- Dataset 2: Comparativo cortes vs recaudación
-- =============================================================================

-- ── Función 1: Listado para corte ──────────────────────────────────────────
DROP FUNCTION IF EXISTS public.rep_listado_corte_masivo(bigint, integer, boolean);

CREATE OR REPLACE FUNCTION public.rep_listado_corte_masivo(
    p_company_id  bigint,
    p_hdr_id      integer,
    p_solo_sin_pago boolean DEFAULT false
)
RETURNS TABLE (
    numero          bigint,
    cliente_clave   text,
    nombre_cliente  text,
    saldo_adeudado  numeric(18,2),
    dias_sin_pago   integer,
    pagado          boolean,
    correlativo     text,
    fecha_generacion date,
    criterio        text,
    empresa_nombre  text
)
LANGUAGE sql
STABLE
AS $function$
WITH empresa AS (
    SELECT COALESCE(NULLIF(legal_name,''), NULLIF(commercial_name,''), code, 'EMPRESA')::text AS nombre
    FROM cfg_company WHERE company_id = p_company_id
),
hdr AS (
    SELECT h.correlativo, h.fecha_generacion, h.criterio
    FROM cln_corte_masivo_hdr h
    WHERE h.id = p_hdr_id AND h.company_id = p_company_id
)
SELECT
    ROW_NUMBER() OVER (ORDER BY d.nombre_cliente)::bigint AS numero,
    d.cliente_clave::text,
    COALESCE(d.nombre_cliente,'')::text,
    COALESCE(d.saldo_adeudado, 0)::numeric(18,2),
    COALESCE(d.dias_sin_pago, 0)::integer,
    d.pagado,
    COALESCE(h.correlativo,'')::text,
    h.fecha_generacion,
    COALESCE(h.criterio,'')::text,
    COALESCE(e.nombre,'')
FROM cln_corte_masivo_dtl d
CROSS JOIN empresa e
LEFT JOIN hdr h ON true
WHERE d.hdr_id = p_hdr_id
  AND d.company_id = p_company_id
  AND (NOT p_solo_sin_pago OR NOT d.pagado)
ORDER BY d.nombre_cliente;
$function$;

COMMENT ON FUNCTION public.rep_listado_corte_masivo(bigint, integer, boolean) IS
'Listado de clientes de un lote de corte masivo. p_solo_sin_pago=true para reimpresión sin pagados.';

-- ── Función 2: Comparativo cortes vs recaudación ───────────────────────────
DROP FUNCTION IF EXISTS public.rep_comparativo_cortes_recaudacion(bigint, integer);

CREATE OR REPLACE FUNCTION public.rep_comparativo_cortes_recaudacion(
    p_company_id bigint,
    p_hdr_id     integer
)
RETURNS TABLE (
    numero           bigint,
    cliente_clave    text,
    nombre_cliente   text,
    monto_corte      numeric(18,2),
    monto_pagado     numeric(18,2),
    diferencia       numeric(18,2),
    pagado           boolean,
    correlativo      text,
    fecha_generacion date,
    empresa_nombre   text
)
LANGUAGE sql
STABLE
AS $function$
WITH empresa AS (
    SELECT COALESCE(NULLIF(legal_name,''), NULLIF(commercial_name,''), code, 'EMPRESA')::text AS nombre
    FROM cfg_company WHERE company_id = p_company_id
),
hdr AS (
    SELECT h.correlativo, h.fecha_generacion
    FROM cln_corte_masivo_hdr h
    WHERE h.id = p_hdr_id AND h.company_id = p_company_id
),
pagos AS (
    SELECT ta.cliente_clave,
           COALESCE(SUM(ta.creditos), 0)::numeric(18,2) AS monto_pagado
    FROM transaccion_abonado ta
    JOIN cln_corte_masivo_hdr hh ON hh.id = p_hdr_id AND hh.company_id = p_company_id
    WHERE ta.company_id = p_company_id
      AND ta.tipotransaccion ILIKE '%PAGO%'
      AND ta.fecha_docu >= hh.fecha_generacion
    GROUP BY ta.cliente_clave
)
SELECT
    ROW_NUMBER() OVER (ORDER BY d.nombre_cliente)::bigint,
    d.cliente_clave::text,
    COALESCE(d.nombre_cliente,'')::text,
    COALESCE(d.saldo_adeudado, 0)::numeric(18,2)  AS monto_corte,
    COALESCE(p.monto_pagado, 0)::numeric(18,2)     AS monto_pagado,
    (COALESCE(d.saldo_adeudado,0) - COALESCE(p.monto_pagado,0))::numeric(18,2) AS diferencia,
    d.pagado,
    COALESCE(h.correlativo,'')::text,
    h.fecha_generacion,
    COALESCE(e.nombre,'')
FROM cln_corte_masivo_dtl d
CROSS JOIN empresa e
LEFT JOIN hdr h ON true
LEFT JOIN pagos p ON p.cliente_clave = d.cliente_clave
WHERE d.hdr_id = p_hdr_id
  AND d.company_id = p_company_id
ORDER BY d.nombre_cliente;
$function$;

COMMENT ON FUNCTION public.rep_comparativo_cortes_recaudacion(bigint, integer) IS
'Comparativo de lo generado para corte vs lo recaudado desde la fecha del lote.';

-- ── Registro en catálogos (company_id dinámico) ────────────────────────────
DO $$
DECLARE
    v_company_id bigint;
    v_now        timestamptz := NOW();
    v_user       text := 'sistema';
BEGIN
    SELECT MIN(company_id) INTO v_company_id FROM cfg_company;
    IF v_company_id IS NULL THEN RETURN; END IF;

    -- Dataset 1: listado corte masivo
    INSERT INTO rep_catalogo_dataset
        (company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
         is_active, created_at, created_by)
    VALUES (
        v_company_id,
        'listado-corte-masivo',
        'Dataset listado corte masivo',
        'Clientes incluidos en un lote de corte masivo',
        'STORED_PROCEDURE',
        'public.rep_listado_corte_masivo',
        true, v_now, v_user
    )
    ON CONFLICT (company_id, codigo) DO UPDATE
        SET nombre        = EXCLUDED.nombre,
            origen_clave  = EXCLUDED.origen_clave,
            updated_at    = v_now,
            updated_by    = v_user;

    -- Dataset 2: comparativo cortes vs recaudación
    INSERT INTO rep_catalogo_dataset
        (company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
         is_active, created_at, created_by)
    VALUES (
        v_company_id,
        'comparativo-cortes-recaudacion',
        'Dataset comparativo cortes vs recaudacion',
        'Comparativo de lo generado para corte vs lo recaudado desde la fecha del lote',
        'STORED_PROCEDURE',
        'public.rep_comparativo_cortes_recaudacion',
        true, v_now, v_user
    )
    ON CONFLICT (company_id, codigo) DO UPDATE
        SET nombre        = EXCLUDED.nombre,
            origen_clave  = EXCLUDED.origen_clave,
            updated_at    = v_now,
            updated_by    = v_user;

    -- Informe 1
    INSERT INTO rep_catalogo_informe
        (company_id, codigo, nombre, descripcion, categoria, tipo_origen, ruta,
         icono_css_class, orden, permite_exportar, permite_imprimir,
         is_active, created_at, created_by)
    VALUES (
        v_company_id,
        'listado-corte-masivo',
        'Listado de clientes para corte',
        'Listado de clientes generados en un lote de cortes masivos',
        'Cobranza', 'REPORT', '/informes/reportes/listado-corte-masivo',
        'bi bi-scissors', 100, true, true,
        true, v_now, v_user
    )
    ON CONFLICT (company_id, codigo) DO UPDATE
        SET nombre     = EXCLUDED.nombre,
            updated_at = v_now,
            updated_by = v_user;

    -- Informe 2
    INSERT INTO rep_catalogo_informe
        (company_id, codigo, nombre, descripcion, categoria, tipo_origen, ruta,
         icono_css_class, orden, permite_exportar, permite_imprimir,
         is_active, created_at, created_by)
    VALUES (
        v_company_id,
        'comparativo-cortes-recaudacion',
        'Comparativo cortes vs recaudacion',
        'Comparativo de lo generado para corte versus lo recaudado',
        'Cobranza', 'REPORT', '/informes/reportes/comparativo-cortes-recaudacion',
        'bi bi-bar-chart', 101, true, true,
        true, v_now, v_user
    )
    ON CONFLICT (company_id, codigo) DO UPDATE
        SET nombre     = EXCLUDED.nombre,
            updated_at = v_now,
            updated_by = v_user;
END $$;
