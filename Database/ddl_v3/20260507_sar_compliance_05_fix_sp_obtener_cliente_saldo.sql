-- =============================================================================
-- Fix sp_obtener_cliente_saldo — agregar filtro estado='A' (anulados fuera)
-- Fecha: 2026-05-07
-- Plan: PLAN_ENTREGA_2026-05-25.md Sprint 2 dia 7 (anticipado)
-- Cambio mínimo invasivo: solo agrega filtro de estado.
-- La fuente única (saldo_detalle agregado) se aborda en otro paso al hacer
-- snapshot V3 con saldo previo.
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

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(character varying) IS
'Saldo del cliente. Fuente: transaccion_abonado.saldo (último movimiento activo).
Multi-tenancy diferida: si en el futuro hay clientes con misma clave en
empresas distintas, agregar p_company_id como parámetro y filtrar por él.
Ver REPORTE_SALDO_CLIENTE_2026-05-05.md para discusión de fuente única.';
