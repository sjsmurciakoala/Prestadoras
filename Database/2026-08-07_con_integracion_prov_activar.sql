-- =============================================================================
-- Retenciones a proveedores — F0: activar el posteo del asiento de PAGO por el
--   motor config-driven (module='PROV')
-- Fecha: 2026-08-07
-- Rama: feat/almacen-integracion-contable
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   Hoy las partidas de compromisos/OPD (module='PROV') nacen status=0 (BORRADOR)
--   vía sp_registrar_partida_contable y NUNCA llegan al mayor. El binario de F0
--   mueve el asiento de PAGO (procesar + abonos) al motor
--   sp_con_generar_comprobante_config (POSTED, idempotente, con reverso). Ese motor
--   exige, por empresa: (a) fila en con_integracion_config (ya existe) con
--   activo_proveedores=TRUE, y (b) fila en con_integracion_asiento (module='PROV')
--   con journal_id + type_id. Este script deja ambos datos listos. NO es estructura:
--   'PROV' ya está en el CHECK de con_integracion_asiento y la columna
--   activo_proveedores ya existe (fase2).
--
-- QUÉ HACE (para company_id = 2, la única con con_integracion_config)
--   1) INSERT en con_integracion_asiento la fila module='PROV' con journal_id/type_id
--      resueltos con las MISMAS reglas que OrdenesPagoDirectoService hoy:
--        - diario: code='PRV' activo; si no, el primer diario activo por journal_id.
--        - tipo:   is_default activo; si no, el primer tipo activo por type_id.
--      (En el mirror hoy resuelve a journal_id=1 '00000' y type_id=1 'FACT'.)
--   2) UPDATE con_integracion_config.activo_proveedores = TRUE.
--
-- ADITIVO / IDEMPOTENTE (INSERT con WHERE NOT EXISTS, UPDATE idempotente).
--   Sin DROP/ALTER/TRUNCATE/DELETE. Re-ejecutarlo deja el mismo estado.
--   Aborta con RAISE si la empresa no tiene con_integracion_config, o si no hay
--   diario/tipo activo (no deja el módulo medio-configurado).
--
-- CON BINARIO: el binario de F0 (OrdenesPagoDirectoService + PrvContabilidad) lee
--   esta configuración al postear. Aplicar el SQL en la misma ventana que el portal.
--
-- ⚠️ EFECTO DE NEGOCIO: a partir de aquí los pagos NUEVOS de compromisos impactan la
--   contabilidad (mayor) y "Retenciones por pagar" aparece como saldo. Las 23 partidas
--   PROV históricas en borrador se DEJAN como histórico (F0 solo aplica a nuevas).
-- =============================================================================
BEGIN;

DO $$
DECLARE
    v_company_id bigint := 2;   -- tenant en uso (MERENDON): única empresa con con_integracion_config.
    v_journal_id bigint;
    v_type_id    bigint;
BEGIN
    -- La empresa debe tener configuración de integración (si falta, el motor lanza al postear).
    IF NOT EXISTS (SELECT 1 FROM public.con_integracion_config WHERE company_id = v_company_id) THEN
        RAISE EXCEPTION 'La empresa % no tiene fila en con_integracion_config; configure la integración contable antes de activar PROV.', v_company_id;
    END IF;

    -- Diario: code='PRV' activo, si no el primer diario activo (fallback de ResolveJournalIdAsync).
    SELECT COALESCE(
        (SELECT journal_id FROM public.con_diario
          WHERE company_id = v_company_id AND is_active AND upper(btrim(code)) = 'PRV'
          ORDER BY journal_id LIMIT 1),
        (SELECT journal_id FROM public.con_diario
          WHERE company_id = v_company_id AND is_active
          ORDER BY journal_id LIMIT 1)
    ) INTO v_journal_id;

    -- Tipo de partida: is_default activo, si no el primer tipo activo (fallback de ResolveTipoPartidaIdAsync).
    SELECT COALESCE(
        (SELECT type_id FROM public.con_tipo_transaccion
          WHERE company_id = v_company_id AND is_default AND status IN ('ACTIVE','ACTIVO')
          ORDER BY type_id LIMIT 1),
        (SELECT type_id FROM public.con_tipo_transaccion
          WHERE company_id = v_company_id AND status IN ('ACTIVE','ACTIVO')
          ORDER BY type_id LIMIT 1)
    ) INTO v_type_id;

    IF v_journal_id IS NULL THEN
        RAISE EXCEPTION 'No hay diario contable activo para la empresa %; no se puede configurar el asiento PROV.', v_company_id;
    END IF;
    IF v_type_id IS NULL THEN
        RAISE EXCEPTION 'No hay tipo de partida activo para la empresa %; no se puede configurar el asiento PROV.', v_company_id;
    END IF;

    -- (1) Fila del asiento PROV (idempotente): diario + tipo que el motor usa para module='PROV'.
    INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id)
    SELECT v_company_id, 'PROV', v_journal_id, v_type_id
    WHERE NOT EXISTS (
        SELECT 1 FROM public.con_integracion_asiento
        WHERE company_id = v_company_id AND module = 'PROV'
    );

    -- (2) Encender el posteo de proveedores por el motor (idempotente).
    UPDATE public.con_integracion_config
       SET activo_proveedores = TRUE
     WHERE company_id = v_company_id
       AND activo_proveedores IS DISTINCT FROM TRUE;

    RAISE NOTICE 'PROV activado para company %: con_integracion_asiento journal_id=%, type_id=%; activo_proveedores=TRUE.',
        v_company_id, v_journal_id, v_type_id;
END $$;

COMMIT;

-- =============================================================================
-- ¿Ya aplicado? (correr a mano)
--   SELECT c.activo_proveedores,
--          a.journal_id, a.type_id
--     FROM public.con_integracion_config c
--     LEFT JOIN public.con_integracion_asiento a
--            ON a.company_id = c.company_id AND a.module = 'PROV'
--    WHERE c.company_id = 2;
--   -- Esperado: activo_proveedores = t | journal_id y type_id NO nulos.
-- =============================================================================
-- ROLLBACK (ejecutar manualmente SOLO si hay que revertir F0):
--   BEGIN;
--     UPDATE public.con_integracion_config SET activo_proveedores = FALSE WHERE company_id = 2;
--     DELETE FROM public.con_integracion_asiento WHERE company_id = 2 AND module = 'PROV';
--   COMMIT;
--   -- Con activo_proveedores=FALSE el binario NO postea al mayor (vuelve a status=0 vía el camino
--   -- viejo). Nota: partidas PROV ya POSTEADAS por el motor no se borran con esto (revertir por UI).
-- =============================================================================
