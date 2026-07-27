-- ============================================================================
-- Unificación de cobranza — F4 (2026-07-28)
-- El saldo del cliente deja de leerse de transaccion_abonado y pasa a
-- calcularse por DOCUMENTOS PENDIENTES:
--
--   saldo = SUM(saldo de líneas de facturas en estado A/B)          [documentos]
--         + SUM(vigente SALDO_ANTERIOR / SALDO_INICIAL)             [residuo]
--
-- El "residuo" es la cartera migrada de SIMAFI que aún no existe como
-- documento; muere en F7 cuando la re-migración la escriba como documentos
-- (el término queda en 0 y se puede retirar).
--
-- Equivalencia auditada en siad_v3_copia09 (company 2) antes del cambio:
-- 850/850 clientes exactos, diferencia neta 0.00 contra
-- SUM(vw_transaccion_abonado_vigente). La misma auditoría queda como test de
-- integración (SaldoDocumentosTests) y debe correr en cada fase hasta F7.
--
-- Firmas intactas (contratos blindados):
--   * sp_obtener_cliente_saldo(bigint, varchar) → TABLE(saldo_actual numeric)
--     — la usan el estado de cuenta, el motor de cobro (espejo), el WS
--     bancario, sp_adm_calcular_factura_lectura (mora online) y el snapshot
--     offline (mora offline). Cambian JUNTAS de fuente aquí mismo.
--   * sp_obtener_cliente_saldo_servicio_detalle(bigint, varchar, varchar) y
--     su overload legacy (varchar, varchar) — la usan sp_lectura_v3 y el
--     snapshot (saldos_por_servicio). La fuente legacy (corrida saldo_detalle
--     del último movimiento) estaba DESACTUALIZADA para clientes con pagos
--     del motor (auditoría: 20 de 358 pares corregidos por este cambio).
--
-- El overload 1-arg de sp_obtener_cliente_saldo (cross-company, [DEPRECATED]
-- desde 2026-05-09) NO se toca: sin callers vivos, se elimina en F7.
-- ============================================================================

BEGIN;

-- Índice de soporte: el SP corre por cliente dentro de la lectura masiva y
-- del snapshot; no existía índice por (company, cliente, estado).
CREATE INDEX IF NOT EXISTS ix_factura_company_cliente_estado
    ON public.factura (company_id, clientecodigo, estado);

-- ----------------------------------------------------------------------------
-- Saldo total del cliente (v3 — documentos + residuo migrado)
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(
    p_company_id     bigint,
    pcodigocliente   character varying)
RETURNS TABLE(saldo_actual numeric)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT
      COALESCE((
          SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = pcodigocliente
            AND f.estado IN ('A','B')
      ), 0)
    + COALESCE((
          -- Residuo migrado sin documento (cartera SIMAFI). Se retira en F7.
          SELECT SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))
          FROM public.vw_transaccion_abonado_vigente ta
          WHERE ta.company_id      = p_company_id
            AND ta.cliente_clave   = pcodigocliente
            AND ta.tipotransaccion IN ('SALDO_ANTERIOR', 'SALDO_INICIAL')
      ), 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'F4 (2026-07-28): saldo por documentos pendientes (facturas A/B, SUM de montovalor_saldo por línea) + residuo migrado SALDO_ANTERIOR/SALDO_INICIAL de la vista vigente. Misma firma desde 2026-05-09. El residuo desaparece en F7.';

-- ----------------------------------------------------------------------------
-- Saldo del cliente POR SERVICIO (v2 — líneas pendientes del servicio)
-- Antes: última corrida saldo_detalle de transaccion_abonado, que quedaba
-- desactualizada con los pagos del motor. Los SALDO_ANTERIOR migrados llevan
-- tipo_servicio NULL y saldo_detalle 0, por lo que esta función nunca los
-- incluyó — no lleva residuo.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(
    p_company_id     bigint,
    pcodigocliente   character varying,
    servicio_codigo  character varying)
RETURNS numeric
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_valor numeric(18,2);
BEGIN
    SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
      INTO v_valor
      FROM public.factura f
      JOIN public.factura_detalle d ON d.factura_id = f.id
     WHERE f.company_id    = p_company_id
       AND f.clientecodigo = pcodigocliente
       AND f.estado IN ('A','B')
       AND d.tiposervicio  = servicio_codigo;

    RETURN COALESCE(v_valor, 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(bigint, character varying, character varying) IS
'F4 (2026-07-28): saldo pendiente del servicio = SUM de líneas (montovalor_saldo) de facturas A/B con ese tiposervicio.';

-- Overload legacy SIN company (lo llama sp_lectura_v3 vigente). Conserva su
-- semántica cross-company (bug documentado, gap L8) pero cambia de fuente.
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(
    pcodigocliente   character varying,
    servicio_codigo  character varying)
RETURNS numeric
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_valor numeric(18,2);
BEGIN
    SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
      INTO v_valor
      FROM public.factura f
      JOIN public.factura_detalle d ON d.factura_id = f.id
     WHERE f.clientecodigo = pcodigocliente
       AND f.estado IN ('A','B')
       AND d.tiposervicio  = servicio_codigo;

    RETURN COALESCE(v_valor, 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(character varying, character varying) IS
'[DEPRECATED cross-company — usar el overload con company_id] F4 (2026-07-28): misma fuente por documentos que el overload nuevo.';

COMMIT;

-- ----------------------------------------------------------------------------
-- Smoke post-aplicación (manual): equivalencia legacy vs v3 por cliente.
-- Debe devolver con_diff = 0 hasta el corte de F7.
--
-- WITH legacy AS (
--   SELECT cliente_clave, SUM(COALESCE(debitos,0)-COALESCE(creditos,0)) AS s
--   FROM vw_transaccion_abonado_vigente WHERE company_id = :cid GROUP BY 1
-- )
-- SELECT COUNT(*) FILTER (WHERE l.s <> v.saldo_actual) AS con_diff
-- FROM legacy l, LATERAL sp_obtener_cliente_saldo(:cid, l.cliente_clave) v;
-- ----------------------------------------------------------------------------
