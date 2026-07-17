-- =============================================================================
-- Dataset del formato de factura (ticket) para el catalogo web de reporteria.
--
-- public.rep_factura_ticket(p_company_id, p_factura_id) devuelve un resultset
-- PLANO: una fila por linea de servicio (factura_detalle), con las columnas de
-- cabecera repetidas (emisor, CAI, cliente, lectura, totales). El layout
-- DevExpress agrupa/encabeza con esas columnas; el diseno editado se persiste
-- por empresa en rep_reporte_layout (patron rep_estado_flujo_efectivo).
--
-- Datos que incluye:
--   - Emisor desde cfg_company (nombre comercial, RTN, direccion, telefono).
--   - Bloque fiscal SAR desde adm_cai_correlativo_emitido + adm_cai_facturacion
--     (CAI, rango autorizado, fecha limite de emision). NULL si la factura no
--     tiene correlativo CAI registrado.
--   - Lectura desde historicomedicion (ultima fila por clave+numerofactura) y
--     condicion desde adm_condicion_lectura.
--   - total_en_letras se resuelve en el layout con una expresion o se deja al
--     disenador; la funcion entrega el total numerico.
--
-- Idempotente (DROP + CREATE). Aplicar en cada BD (local, 0.9).
-- =============================================================================

BEGIN;

DROP FUNCTION IF EXISTS public.rep_factura_ticket(bigint, bigint);

CREATE FUNCTION public.rep_factura_ticket(
    p_company_id bigint,
    p_factura_id bigint
)
RETURNS TABLE(
    -- Emisor
    empresa_nombre text,
    empresa_rtn text,
    empresa_direccion text,
    empresa_telefono text,
    -- Bloque fiscal SAR
    codigo_cai text,
    rango_autorizado text,
    fecha_limite_emision date,
    -- Factura
    factura_id bigint,
    numero_factura text,
    num_recibo integer,
    fecha_emision date,
    fecha_vence date,
    periodo text,
    -- Cliente
    cliente_clave text,
    cliente_nombre text,
    cliente_rtn text,
    cliente_direccion text,
    -- Lectura
    medidor text,
    lectura_anterior numeric,
    lectura_actual numeric,
    consumo numeric,
    condicion text,
    fecha_lectura date,
    -- Totales / firma
    total numeric,
    lector text,
    -- Linea de servicio (una fila por detalle)
    linea_orden integer,
    linea_descripcion text,
    linea_moneda text,
    linea_monto numeric
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        co.commercial_name::text                            AS empresa_nombre,
        co.tax_id::text                                     AS empresa_rtn,
        co.address::text                                    AS empresa_direccion,
        co.phone::text                                      AS empresa_telefono,
        cai.codigo_cai::text                                AS codigo_cai,
        CASE WHEN cai.cai_id IS NOT NULL THEN
            concat(cai.prefijo_documento, lpad(cai.rango_desde::text, 8, '0'),
                   ' al ',
                   cai.prefijo_documento, lpad(cai.rango_hasta::text, 8, '0'))
        END                                                 AS rango_autorizado,
        cai.fecha_limite_emision                            AS fecha_limite_emision,
        f.id::bigint                                        AS factura_id,
        COALESCE(f.numfactura, '')::text                    AS numero_factura,
        COALESCE(f.numrecibo, 0)                            AS num_recibo,
        f.fechaemision                                      AS fecha_emision,
        f.fechavence                                        AS fecha_vence,
        COALESCE(f.periodo, concat(f.mes, '/', f.ano))::text AS periodo,
        COALESCE(f.clientecodigo, '')::text                 AS cliente_clave,
        COALESCE(cm.maestro_cliente_nombre, '')::text       AS cliente_nombre,
        COALESCE(NULLIF(f.rtn, ''), NULLIF(cm.maestro_cliente_rtn, ''), '0')::text AS cliente_rtn,
        cd.detalle_cliente_direccion::text                  AS cliente_direccion,
        hm.contador::text                                   AS medidor,
        hm.lect_ant                                         AS lectura_anterior,
        hm.lect_act                                         AS lectura_actual,
        hm.consumo                                          AS consumo,
        CASE WHEN hm.condicion IS NOT NULL
             THEN concat(hm.condicion, COALESCE(' - ' || cl.descripcion, ''))
        END                                                 AS condicion,
        hm.fecha_lect_act                                   AS fecha_lectura,
        COALESCE(f.saldototal, 0)                           AS total,
        f.usuario::text                                     AS lector,
        row_number() OVER (ORDER BY d.id)::int              AS linea_orden,
        COALESCE(d.descripcion, d.tiposervicio, '')::text   AS linea_descripcion,
        'L.'::text                                          AS linea_moneda,
        COALESCE(d.montovalor, 0)                           AS linea_monto
    FROM public.factura f
    JOIN public.cfg_company co
      ON co.company_id = f.company_id
    LEFT JOIN public.cliente_maestro cm
      ON cm.company_id = f.company_id AND cm.maestro_cliente_clave = f.clientecodigo
    LEFT JOIN LATERAL (
        SELECT dd.detalle_cliente_direccion
        FROM public.cliente_detalle dd
        WHERE dd.maestro_cliente_id = cm.maestro_cliente_id
        LIMIT 1
    ) cd ON TRUE
    LEFT JOIN public.adm_cai_correlativo_emitido e
      ON e.company_id = f.company_id AND e.factura_id = f.id AND e.status_id = 1
    LEFT JOIN public.adm_cai_facturacion cai
      ON cai.company_id = f.company_id AND cai.cai_id = e.cai_id
    LEFT JOIN LATERAL (
        SELECT h.condicion, h.contador, h.lect_ant, h.lect_act, h.consumo, h.fecha_lect_act
        FROM public.historicomedicion h
        WHERE h.company_id = f.company_id
          AND h.clave = f.clientecodigo
          AND h.numerofactura = f.numfactura
        ORDER BY h.ide DESC
        LIMIT 1
    ) hm ON TRUE
    LEFT JOIN public.adm_condicion_lectura cl
      ON cl.company_id = f.company_id AND cl.codigo = hm.condicion
    LEFT JOIN public.factura_detalle d
      ON d.company_id = f.company_id AND d.factura_id = f.id
    WHERE f.company_id = p_company_id
      AND f.id = p_factura_id
    ORDER BY d.id;
$$;

COMMENT ON FUNCTION public.rep_factura_ticket(bigint, bigint)
IS 'Dataset del formato de factura (ticket) del catalogo web: una fila por linea de servicio con cabecera repetida (emisor, CAI SAR, cliente, lectura, total). Consumido por el informe factura-ticket.';

COMMIT;
