-- =============================================================================
-- 2026-09-08 — Replica hacia el mirror la estructura que solo existia en prod
--
-- QUE ES ESTO
--   Los ultimos 7 objetos de ESTRUCTURA que `siad_v4` tenia y el mirror no.
--   Con esto la estructura de `siad_v3_restore` queda completa respecto de
--   produccion, salvo el andamiaje de migracion (ver abajo).
--
--   El contenido NO se escribio a mano: se extrajo de `siad_v4` con
--   pg_get_functiondef y pg_get_indexdef, en una sesion abierta con
--   default_transaction_read_only=on.
--
-- DIRECCION DEL FLUJO
--   ⚠️ **A produccion NO se le toca nada.** Este script va de arriba hacia
--   abajo: replica `siad_v4` en el mirror, que es el unico sentido permitido.
--   * `siad_v3_restore` (mirror) — destino.
--   * `siad_v3_desarrollo` — se puede aplicar.
--   * `siad_v4` (produccion) — **NO**. Ya los tiene; es el origen.
--
-- QUE TRAE
--
--   Dos funciones de reporte:
--     * rep_banco_diario(company, desde, hasta)   — tiene script propio en el
--       repo (2026-07-31_rep_banco_diario.sql), pero se toma la version que
--       corre arriba, que es la fuente de verdad.
--     * rep_factura_ticket(company, factura_id)   — **no tiene ningun script
--       en Database/**. Este archivo es su unico respaldo en el repositorio.
--
--   Cinco indices:
--     * ix_adm_pago_ta_ide               (adm_pago)
--     * ix_factura_company_clientecodigo (factura)
--     * ix_factura_company_numfactura    (factura, parcial)
--     * ix_factura_company_recibo_cliente(factura)
--     * ix_ta_company_cliente            (transaccion_abonado)
--
--   ⚠️ Los cinco son indices de RENDIMIENTO pensados para el volumen de
--   produccion: alli factura tiene 3,9 M de filas, transaccion_abonado 12,1 M
--   y adm_pago 2,8 M. En el mirror esas tablas tienen 225, 1.494 y 0 filas, asi
--   que **no aportan nada aqui**: se traen por fidelidad estructural, para que
--   el diff contra prod quede limpio, no porque mejoren ninguna consulta local.
--   Su creacion es instantanea por el mismo motivo.
--
-- QUE **NO** TRAE
--   * **Los datos.** Las filas de `rep_catalogo_dataset`, `rep_dataset_parametro`
--     y `rep_catalogo_informe` de "banco-diario" y "factura-ticket" son datos,
--     no estructura, y quedan para la fase siguiente. Sin ellas las dos
--     funciones existen pero los informes no aparecen en el portal.
--   * **El andamiaje de la migracion**: las tablas `_m4_aplic`, `_m4_cargo`,
--     `_m4_credito`, `_m4_seg` y `_repcmp` con sus columnas e indices (38
--     objetos). Son restos temporales de la migracion a `siad_v4`, estan vacias
--     salvo `_repcmp` (30 filas) y no las usa ningun codigo. Se omiten a
--     proposito. Si se quisiera un mirror byte a byte, el camino es el restore,
--     no este script.
--
-- IDEMPOTENTE
--   Las funciones son CREATE OR REPLACE y los indices llevan IF NOT EXISTS.
--   Se puede reejecutar.
--
-- IMPACTO DE DATOS
--   Ninguno. No inserta, no borra y no actualiza ninguna fila.
--
-- REVERSA
--   DROP FUNCTION IF EXISTS public.rep_banco_diario(bigint, date, date);
--   DROP FUNCTION IF EXISTS public.rep_factura_ticket(bigint, bigint);
--   DROP INDEX IF EXISTS public.ix_adm_pago_ta_ide;
--   DROP INDEX IF EXISTS public.ix_factura_company_clientecodigo;
--   DROP INDEX IF EXISTS public.ix_factura_company_numfactura;
--   DROP INDEX IF EXISTS public.ix_factura_company_recibo_cliente;
--   DROP INDEX IF EXISTS public.ix_ta_company_cliente;
-- =============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

-- --- Las dos funciones de reporte, tal como estan en siad_v4 ----------------

CREATE OR REPLACE FUNCTION public.rep_banco_diario(p_company_id bigint, p_fecha_desde date DEFAULT NULL::date, p_fecha_hasta date DEFAULT NULL::date)
 RETURNS TABLE(fila_orden bigint, fecha date, numero_recibo text, cliente_clave text, cliente_nombre text, canal text, forma_pago text, banco text, cuenta_bancaria text, caja text, cajero text, monto numeric, empresa_nombre text, periodo_titulo text, fecha_desde date, fecha_hasta date, fecha_reporte date, fecha_reporte_texto text)
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
$function$
;

CREATE OR REPLACE FUNCTION public.rep_factura_ticket(p_company_id bigint, p_factura_id bigint)
 RETURNS TABLE(empresa_nombre text, empresa_rtn text, empresa_direccion text, empresa_telefono text, codigo_cai text, rango_autorizado text, fecha_limite_emision date, factura_id bigint, numero_factura text, num_recibo integer, fecha_emision date, fecha_vence date, periodo text, cliente_clave text, cliente_nombre text, cliente_rtn text, cliente_direccion text, medidor text, lectura_anterior numeric, lectura_actual numeric, consumo numeric, condicion text, fecha_lectura date, total numeric, lector text, linea_orden integer, linea_descripcion text, linea_moneda text, linea_monto numeric)
 LANGUAGE sql
 STABLE
AS $function$
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
$function$
;

-- --- Los cinco indices de rendimiento --------------------------------------

CREATE INDEX IF NOT EXISTS ix_adm_pago_ta_ide ON public.adm_pago USING btree (company_id, transaccion_abonado_ide);
CREATE INDEX IF NOT EXISTS ix_factura_company_clientecodigo ON public.factura USING btree (company_id, clientecodigo);
CREATE INDEX IF NOT EXISTS ix_factura_company_numfactura ON public.factura USING btree (company_id, numfactura) WHERE (numfactura IS NOT NULL);
CREATE INDEX IF NOT EXISTS ix_factura_company_recibo_cliente ON public.factura USING btree (company_id, numrecibo, clientecodigo);
CREATE INDEX IF NOT EXISTS ix_ta_company_cliente ON public.transaccion_abonado USING btree (company_id, cliente_clave);

COMMIT;

-- =============================================================================
-- VERIFICACION (ejecutar aparte, despues del COMMIT)
-- =============================================================================
--
-- Las dos funciones deben existir:
--   SELECT p.proname, pg_get_function_identity_arguments(p.oid)
--     FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--    WHERE n.nspname = 'public'
--      AND p.proname IN ('rep_banco_diario', 'rep_factura_ticket')
--    ORDER BY 1;
--   -- esperado: 2 filas
--
-- Los cinco indices deben existir:
--   SELECT indexname FROM pg_indexes
--    WHERE schemaname = 'public'
--      AND indexname IN ('ix_adm_pago_ta_ide', 'ix_factura_company_clientecodigo',
--                        'ix_factura_company_numfactura', 'ix_factura_company_recibo_cliente',
--                        'ix_ta_company_cliente')
--    ORDER BY 1;
--   -- esperado: 5 filas
--
-- Y las huellas de las dos funciones deben coincidir con las de siad_v4:
--   SELECT p.proname, md5(regexp_replace(p.prosrc, '\s+', '', 'g'))
--     FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--    WHERE n.nspname = 'public'
--      AND p.proname IN ('rep_banco_diario', 'rep_factura_ticket')
--    ORDER BY 1;
--   -- rep_banco_diario en siad_v4: ce02ca8c2334a3246bad28e2f2aab3df
