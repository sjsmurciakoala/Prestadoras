-- =============================================================================
-- BUGFIX #5 — fn_getclientesaldos_posteomanual no encuentra facturas V3
-- Fecha: 2026-05-14
--
-- Problema:
--   La funcion filtraba "COALESCE(fd.montovalor_saldo, 0) > 0" pero
--   sp_lectura_v3 guarda montovalor_saldo = saldo previo del servicio antes
--   de esta factura. Para clientes nuevos (los 4 del demo Azure) eso es 0,
--   asi que la funcion descarta TODAS las facturas V3 nuevas → captacion
--   en caja no muestra el cliente como deudor pese a tener saldototal=331.11.
--
--   Bug adicional: la funcion devolvia f.numrecibo AS numreciboanterior
--   (mismo valor de la columna numrecibo, no el correlativo previo).
--
-- Fix:
--   1. Filtrar por f.saldototal > 0 AND f.estado = 'A' (criterio real de
--      factura pendiente de cobro).
--   2. Devolver fd.montovalor como r_monto / r_monto_distribuido (monto
--      facturado por servicio en esta factura). En el demo no hay pagos
--      parciales por servicio asi que ambos campos son iguales.
--   3. Calcular numreciboanterior con LAG() sobre numrecibo DESC para
--      reflejar el correlativo previo de ese cliente.
--
-- Deuda tecnica post-25-may:
--   - Manejo de pagos parciales por servicio (r_monto vs r_monto_distribuido)
--   - Migrar firma a (p_company_id, pcodigocliente) para multi-empresa
--   - Reevaluar el significado de factura_detalle.montovalor_saldo
-- =============================================================================

DROP FUNCTION IF EXISTS public.fn_getclientesaldos_posteomanual(character varying);

CREATE OR REPLACE FUNCTION public.fn_getclientesaldos_posteomanual(
    pcodigocliente character varying
)
RETURNS TABLE (
    r_ide integer,
    r_recibo integer,
    r_recibo_anterior integer,
    r_cliente character varying,
    r_tiposervicio character varying,
    r_monto numeric,
    r_monto_distribuido numeric,
    r_descripcion character varying,
    r_estado character varying
)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
    RETURN QUERY
    WITH facturas_pendientes AS (
        SELECT
            f.id            AS factura_id,
            f.numrecibo,
            f.clientecodigo,
            f.estado,
            LAG(f.numrecibo) OVER (
                PARTITION BY f.clientecodigo
                ORDER BY f.numrecibo
            ) AS numrecibo_anterior
        FROM public.factura f
        WHERE f.clientecodigo = pcodigocliente
          AND COALESCE(f.estado, '') = 'A'
          AND COALESCE(f.saldototal, 0) > 0
    )
    SELECT
        fd.id                                 AS r_ide,
        fp.numrecibo                          AS r_recibo,
        COALESCE(fp.numrecibo_anterior, 0)    AS r_recibo_anterior,
        fp.clientecodigo                      AS r_cliente,
        fd.tiposervicio                       AS r_tiposervicio,
        COALESCE(fd.montovalor, 0)            AS r_monto,
        COALESCE(fd.montovalor, 0)            AS r_monto_distribuido,
        fd.descripcion                        AS r_descripcion,
        fp.estado                             AS r_estado
    FROM facturas_pendientes fp
    INNER JOIN public.factura_detalle fd
            ON fd.factura_id = fp.factura_id
    WHERE COALESCE(fd.montovalor, 0) > 0
    ORDER BY fp.numrecibo DESC, fd.tiposervicio;
END
$function$;

COMMENT ON FUNCTION public.fn_getclientesaldos_posteomanual(character varying) IS
'Devuelve detalle de facturas pendientes (saldototal > 0 AND estado = ''A'') por servicio.
r_monto y r_monto_distribuido son iguales: monto facturado del servicio. El soporte de pagos
parciales por servicio queda como deuda tecnica post-25-may. Bugfix 2026-05-14: antes filtraba
por montovalor_saldo > 0, lo que descartaba facturas V3 de clientes nuevos.';
