-- =============================================================================
-- Informe de banco diario (backlog de pruebas operativas jul-2026:
-- "indispensable para operación").
--
-- Lista la recaudación VIGENTE del rango (default: hoy) desde el MODELO NUEVO
-- (adm_pago, estado 1 = aplicado), con banco/cuenta destino para los cobros
-- por banco, caja física y cajero para los de ventanilla, y el canal (caja /
-- WS bancario / app). Los anulados y reversados quedan fuera: el informe es
-- para cuadrar el depósito del día.
--
-- La plantilla DevExpress inicial la genera ReportTemplateFactory al abrir el
-- viewer/designer; el diseño editado se persiste por empresa en
-- rep_reporte_layout. Registro de catálogo:
-- ddl_v3/20260731_registro_informe_banco_diario_company2.sql
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

CREATE OR REPLACE FUNCTION public.rep_banco_diario(
    p_company_id bigint,
    p_fecha_desde date DEFAULT NULL,
    p_fecha_hasta date DEFAULT NULL)
RETURNS TABLE(
    fila_orden bigint,
    fecha date,
    numero_recibo text,
    cliente_clave text,
    cliente_nombre text,
    canal text,
    forma_pago text,
    banco text,
    cuenta_bancaria text,
    caja text,
    cajero text,
    monto numeric,
    empresa_nombre text,
    periodo_titulo text,
    fecha_desde date,
    fecha_hasta date,
    fecha_reporte date,
    fecha_reporte_texto text)
LANGUAGE sql
STABLE
AS $function$
WITH parametros AS (
    -- Informe OPERATIVO diario: rango tope de 31 días. El viewer de DevExpress
    -- puede mandar defaults absurdos (se vio 01/01/2025→31/07/2026 = 296k
    -- pagos migrados → timeout); el tope protege y el título muestra el rango
    -- efectivo, así el recorte nunca es silencioso.
    SELECT
        p_company_id AS company_id,
        COALESCE(p_fecha_desde, current_date) AS fecha_desde,
        LEAST(
            GREATEST(
                COALESCE(p_fecha_hasta, COALESCE(p_fecha_desde, current_date)),
                COALESCE(p_fecha_desde, current_date)
            ),
            COALESCE(p_fecha_desde, current_date) + 31
        ) AS fecha_hasta
),
empresa AS (
    SELECT
        p.company_id,
        COALESCE(NULLIF(c.legal_name, ''), NULLIF(c.commercial_name, ''), c.code, 'EMPRESA')::text AS empresa_nombre
    FROM parametros p
    LEFT JOIN public.cfg_company c
      ON c.company_id = p.company_id
)
SELECT
    ROW_NUMBER() OVER (ORDER BY pg.fecha, pg.pago_id)      AS fila_orden,
    pg.fecha,
    pg.numero_recibo::text                                  AS numero_recibo,
    pg.cliente_clave::text                                  AS cliente_clave,
    COALESCE(cm.maestro_cliente_nombre, '')::text           AS cliente_nombre,
    CASE pg.canal_id
        WHEN 1 THEN 'CAJA'
        WHEN 2 THEN 'BANCO (WS)'
        WHEN 3 THEN 'APP'
        ELSE pg.canal_id::text
    END::text                                               AS canal,
    COALESCE(pg.forma_pago, '')::text                       AS forma_pago,
    COALESCE(bb.nombre, bc.banco_nombre, '')::text          AS banco,
    COALESCE(bc.numero_cuenta, '')::text                    AS cuenta_bancaria,
    COALESCE(cj.nombre, '')::text                           AS caja,
    COALESCE(pg.usuario, '')::text                          AS cajero,
    pg.monto_total                                          AS monto,
    e.empresa_nombre,
    (
        'Informe de banco diario del '
        || to_char(p.fecha_desde, 'DD/MM/YYYY')
        || CASE WHEN p.fecha_hasta <> p.fecha_desde
                THEN ' al ' || to_char(p.fecha_hasta, 'DD/MM/YYYY')
                ELSE '' END
    )::text                                                 AS periodo_titulo,
    p.fecha_desde,
    p.fecha_hasta,
    current_date                                            AS fecha_reporte,
    to_char(current_date, 'DD/MM/YYYY')                     AS fecha_reporte_texto
FROM public.adm_pago pg
CROSS JOIN parametros p
CROSS JOIN empresa e
LEFT JOIN public.cliente_maestro cm
       ON cm.company_id = pg.company_id
      AND cm.maestro_cliente_clave = pg.cliente_clave
LEFT JOIN public.ban_cuenta bc
       ON bc.banco_cuenta_id = pg.banco_cuenta_id
LEFT JOIN public.ban_banco bb
       ON bb.ban_banco_id = bc.ban_banco_id
LEFT JOIN public.sesion_caja sc
       ON sc.id = pg.sesion_caja_id
LEFT JOIN public.adm_caja cj
       ON cj.caja_id = sc.caja_fisica_id
WHERE pg.company_id = p.company_id
  AND pg.estado_id = 1                       -- solo APLICADOS (adm_estado_pago)
  AND pg.fecha BETWEEN p.fecha_desde AND p.fecha_hasta
ORDER BY pg.fecha, pg.pago_id;
$function$;

COMMENT ON FUNCTION public.rep_banco_diario(bigint, date, date) IS
'Informe de banco diario: recaudación vigente del rango desde adm_pago (modelo
nuevo), con banco/cuenta destino, caja y cajero. Pruebas operativas jul-2026.';

COMMIT;
