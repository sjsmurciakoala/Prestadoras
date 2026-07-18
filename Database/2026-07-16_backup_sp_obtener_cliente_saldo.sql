-- =============================================================================
-- RESPALDO: sp_obtener_cliente_saldo — definiciones ANTES del fix de vigencia
-- Fecha de captura: 2026-07-16 (pg_get_functiondef sobre siad_v3_restore)
--
-- Ejecutar este archivo RESTAURA el comportamiento previo del SP (revierte la
-- parte de función de Database/2026-07-16_saldo_vigencia_y_desglose_abono.sql).
-- La vista vw_transaccion_abonado_vigente y la tabla adm_desglose_abono_porcentaje
-- de ese script son aditivas y no necesitan revertirse.
--
-- NOTA: estas definiciones tienen el bug documentado en
-- docs/plans/2026-07-16-desglose-abono-porcentajes-design.md — leen el saldo
-- corrido del último movimiento con estado 'A', por lo que ignoran los abonos
-- vigentes (estado 'C') y cuentan los abonos reversados (estado 'A').
-- =============================================================================

CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(pcodigocliente character varying)
 RETURNS TABLE(saldo_actual numeric)
 LANGUAGE plpgsql
AS $function$
BEGIN
  RETURN QUERY
  SELECT ta.saldo
  FROM transaccion_abonado ta
  WHERE ta.cliente_clave = pcodigocliente
    AND ta.estado = 'A'        -- bug fix 2026-05-07: excluye movimientos anulados
  ORDER BY ta.ide DESC
  LIMIT 1;
END
$function$;

CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(p_company_id bigint, pcodigocliente character varying)
 RETURNS TABLE(saldo_actual numeric)
 LANGUAGE plpgsql
 STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT ta.saldo
  FROM public.transaccion_abonado ta
  WHERE ta.company_id    = p_company_id
    AND ta.cliente_clave = pcodigocliente
    AND ta.estado        = 'A'
  ORDER BY ta.ide DESC
  LIMIT 1;
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'Saldo total del cliente (ultimo movimiento activo). Multi-empresa.
Fuente unica: transaccion_abonado.saldo. Ver REPORTE_SALDO_CLIENTE_2026-05-05.md.
Esta firma reemplaza a sp_obtener_cliente_saldo(varchar) cuando el caller conoce el company_id.';
