-- =============================================================================
-- SPs de saldo del cliente CON company_id (overload, multi-empresa)
-- Fecha: 2026-05-09
-- Plan: PLAN_ENTREGA_2026-05-25.md Sprint 2 dia 8 (snapshot V3 con saldo previo)
--
-- Estrategia: PostgreSQL distingue por signature, asi que las funciones viejas
-- (sin p_company_id) quedan vivas para no romper callers no documentados.
-- Las nuevas firmas con p_company_id son las que usa el snapshot V3 nuevo.
-- Limpieza de las firmas viejas: post-25, en commit aparte que migre tambien
-- sp_lectura_v3 y sp_adm_calcular_factura_lectura a la nueva firma.
--
-- Precondicion (07-may): transaccion_abonado.company_id NOT NULL + backfill
-- via cliente_maestro JOIN. Verificar antes de aplicar:
--   SELECT COUNT(*) FROM transaccion_abonado WHERE company_id IS NULL;  -- debe ser 0
--
-- Idempotente.
-- =============================================================================

BEGIN;

-- 1. sp_obtener_cliente_saldo(p_company_id, pcodigocliente)
--    Saldo total del cliente: ultimo movimiento activo de transaccion_abonado.
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(
    p_company_id bigint,
    pcodigocliente character varying
)
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

-- 2. sp_obtener_cliente_saldo_servicio_detalle(p_company_id, pcodigocliente, servicio_codigo)
--    Saldo pendiente del cliente para un servicio especifico.
--    Bug fix: la version sin company_id tiene el COALESCE comentado y devuelve NULL
--    cuando no hay registro. Aqui devuelve 0.
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(
    p_company_id bigint,
    pcodigocliente character varying,
    servicio_codigo character varying
)
RETURNS numeric
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_valor numeric(18,2);
BEGIN
    SELECT INTO v_valor ta.saldo_detalle
    FROM public.transaccion_abonado ta
    WHERE ta.company_id    = p_company_id
      AND ta.cliente_clave = pcodigocliente
      AND ta.tipo_servicio = servicio_codigo
    ORDER BY ta.ide DESC
    LIMIT 1;

    RETURN COALESCE(v_valor, 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(bigint, character varying, character varying) IS
'Saldo pendiente del cliente por servicio (transaccion_abonado.saldo_detalle del ultimo movimiento). Multi-empresa.
La firma sin p_company_id queda viva (deprecated) hasta que migren sp_lectura_v3 y sp_adm_calcular_factura_lectura.';

-- 3. Marcar las firmas viejas como deprecated (no se eliminan para no romper callers ocultos)
COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(character varying) IS
'[DEPRECATED 2026-05-09] Use sp_obtener_cliente_saldo(bigint p_company_id, varchar pcodigocliente).
La version sin company_id queda viva por compat de callers legacy. Migracion completa post-25.';

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo_servicio_detalle(character varying, character varying) IS
'[DEPRECATED 2026-05-09] Use sp_obtener_cliente_saldo_servicio_detalle(bigint p_company_id, varchar pcodigocliente, varchar servicio_codigo).
Tambien tiene un bug: el COALESCE esta comentado, devuelve NULL cuando no hay registro. La nueva firma corrige el bug.';

COMMIT;

-- =============================================================================
-- Verificaciones (correr manualmente despues del COMMIT)
-- =============================================================================

-- (a) Las 4 firmas conviven:
SELECT proname,
       pg_get_function_identity_arguments(oid) AS args,
       obj_description(oid) AS comment
  FROM pg_proc
 WHERE proname IN ('sp_obtener_cliente_saldo', 'sp_obtener_cliente_saldo_servicio_detalle')
 ORDER BY proname, args;

-- (b) Smoke contra cliente real con saldo > 0 (reemplazar valores de prueba):
-- SELECT * FROM sp_obtener_cliente_saldo(1::bigint, '00000001'::varchar);
-- SELECT sp_obtener_cliente_saldo_servicio_detalle(1::bigint, '00000001'::varchar, 'AGUA_POTABLE'::varchar);

-- (c) Validar que transaccion_abonado.company_id esta poblado para todos los activos:
SELECT 'transaccion_abonado sin company_id' AS check, COUNT(*) AS rows
  FROM public.transaccion_abonado
 WHERE company_id IS NULL;
-- Debe devolver 0. Si > 0, NO usar el snapshot nuevo en produccion sin backfill.
