-- =============================================================================
-- F7 H4 — Congelamiento del legacy: transaccion_abonado a solo-lectura
-- =============================================================================
-- Plan: docs/PLAN_UNIFICACION_COBRANZA_2026-07.md §3.7 / PLAN_F7_CORTE H4.
-- Prerequisitos ya cumplidos: H2 (cero escritores de espejo) y la migración
-- total SIMAFI (la tabla contiene los 12.17M de movimientos históricos reales).
--
-- Tres piezas:
--   1. sp_obtener_cliente_saldo v7: muere el término de residuo. Verificado
--      antes del corte: el residuo vigente de company 2 vale exactamente 0.00
--      (el piloto que lo generaba se eliminó en la migración M3c).
--   2. El trigger de espejo de F1 (trg_transaccion_abonado_sync_estado_id) se
--      retira: ya no entran filas que sincronizar. Su función queda para el
--      barrido de H5.
--   3. Candado de escritura. El REVOKE del plan se aplica, pero como el
--      portal conecta con un rol superusuario (que lo ignora), la defensa real
--      es un trigger que rechaza INSERT/UPDATE/DELETE salvo que la sesión
--      declare explícitamente `SET LOCAL siad.permitir_escritura_legacy='on'`
--      (reservado a scripts de migración y semillas de test).
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- ----------------------------------------------------------------------------
-- 0) Control previo: el residuo debe ser 0 para TODA empresa antes de cortarlo
-- ----------------------------------------------------------------------------
-- En bases de PRUEBA (siad_v3_test, sin la migración total y con residuo
-- sembrado a mano) el control se salta explícitamente:
--   PGOPTIONS="-c siad.forzar_freeze=on" psql ... -f este_script.sql
-- En 0.9/producción NUNCA: si el residuo no es 0, algo quedó sin migrar.
DO $$
DECLARE v_residuo numeric;
BEGIN
    IF COALESCE(current_setting('siad.forzar_freeze', true), '') = 'on' THEN
        RAISE NOTICE 'F7 H4: control de residuo SALTADO (siad.forzar_freeze=on — solo bases de prueba).';
        RETURN;
    END IF;

    SELECT COALESCE(SUM(COALESCE(ta.debitos,0) - COALESCE(ta.creditos,0)), 0)
      INTO v_residuo
      FROM public.vw_transaccion_abonado_vigente ta
     WHERE ta.tipotransaccion IN ('SALDO_ANTERIOR', 'SALDO_INICIAL');

    IF ROUND(v_residuo, 2) <> 0 THEN
        RAISE EXCEPTION
            'F7 H4 ABORTADO: el residuo vigente vale % (debe ser 0). Migrar/limpiar antes de congelar.',
            v_residuo;
    END IF;
END $$;

-- ----------------------------------------------------------------------------
-- 1) Saldo del cliente v7: facturas A/B + cuotas de plan activas + ND vivas.
--    Misma firma. Desaparece la vista de vigencia del cálculo (retiro en H5).
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
          SELECT SUM(dt.saldo_cuota)
          FROM public.cln_plan_pago_dtl dt
          JOIN public.cln_plan_pago_hdr h ON h.id = dt.idhdr
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = h.clienteid
                                        AND cm.company_id = h.company_id
          WHERE h.company_id = p_company_id
            AND h.estado_id  = 1
            AND cm.maestro_cliente_clave = pcodigocliente
            AND dt.estado_id IN (1, 4)
      ), 0)
    + COALESCE((
          SELECT SUM(nd.saldo_pendiente)
          FROM public.adm_nota_debito nd
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = nd.cliente_id
                                        AND cm.company_id = nd.company_id
          WHERE nd.company_id = p_company_id
            AND cm.maestro_cliente_clave = pcodigocliente
            AND nd.estado_id IN (1, 2)
            AND nd.saldo_pendiente > 0
      ), 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'F7 H4 (2026-07-30): saldo = facturas A/B + cuotas de plan activas + notas de débito vivas. El residuo migrado murió (la historia real vive como documentos). Misma firma desde 2026-05-09.';

-- ----------------------------------------------------------------------------
-- 2) Fuera el trigger de espejo de F1 (ya no entran filas que sincronizar)
-- ----------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_transaccion_abonado_sync_estado_id
    ON public.transaccion_abonado;

-- ----------------------------------------------------------------------------
-- 3) Candado de escritura
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_transaccion_abonado_congelada()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF COALESCE(current_setting('siad.permitir_escritura_legacy', true), '') <> 'on' THEN
        RAISE EXCEPTION
            'transaccion_abonado está CONGELADA (F7 H4, 2026-07-30): es el histórico de solo lectura. Los cobros viven en adm_pago/adm_pago_aplicacion. Scripts de migración: SET LOCAL siad.permitir_escritura_legacy = ''on''.';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END
$function$;

DROP TRIGGER IF EXISTS trg_transaccion_abonado_congelada ON public.transaccion_abonado;
CREATE TRIGGER trg_transaccion_abonado_congelada
    BEFORE INSERT OR UPDATE OR DELETE ON public.transaccion_abonado
    FOR EACH ROW EXECUTE FUNCTION public.fn_transaccion_abonado_congelada();

-- ----------------------------------------------------------------------------
-- 4) REVOKE del plan (defensa de permisos; el candado es la defensa efectiva)
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'siad_migracion') THEN
        CREATE ROLE siad_migracion NOLOGIN;
    END IF;
END $$;

REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public.transaccion_abonado FROM PUBLIC;
GRANT  SELECT                            ON public.transaccion_abonado TO PUBLIC;
GRANT  INSERT, UPDATE, DELETE            ON public.transaccion_abonado TO siad_migracion;

COMMENT ON TABLE public.transaccion_abonado IS
'CONGELADA (F7 H4, 2026-07-30). Histórico de solo lectura: 12.17M movimientos migrados de SIMAFI + la historia del dual-write F1-F6. Los cobros nuevos viven en adm_pago + adm_pago_aplicacion. Escritura solo para migración vía SET LOCAL siad.permitir_escritura_legacy = ''on''.';

COMMIT;

-- ----------------------------------------------------------------------------
-- Humo: el candado rechaza y el permiso de migración deja pasar
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    BEGIN
        INSERT INTO public.transaccion_abonado (cliente_clave, company_id, estado_id)
        VALUES ('HUMO_H4', 2, 1);
        RAISE EXCEPTION 'F7 H4: el candado NO bloqueó la escritura';
    EXCEPTION WHEN raise_exception THEN
        IF SQLERRM LIKE '%CONGELADA%' THEN
            RAISE NOTICE 'H4 OK: candado activo (%).', SQLERRM;
        ELSE
            RAISE;
        END IF;
    END;
END $$;
