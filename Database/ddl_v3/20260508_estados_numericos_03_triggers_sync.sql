-- =============================================================================
-- Estados numéricos — Triggers de sincronización string → _id
-- Sprint 2 Día 6 (anticipado al 2026-05-07)
--
-- Objetivo: que los INSERTs/UPDATEs vivos vía EF C# o Java que escriben las
-- columnas string (`estado`, `condicion`) mantengan automáticamente sincrónicas
-- las columnas numéricas (`estado_id`, `condicion_id`) durante la convivencia.
--
-- Esto evita tener que tocar 16 lugares en C# en este sprint. El código nuevo
-- puede empezar a leer `estado_id` con la confianza de que está siempre en
-- sync con `estado`.
--
-- Cuando el código C#/Java migre completamente a `*_id` (post-25-may), se
-- pueden quitar estos triggers y borrar las columnas string.
--
-- Idempotente.
-- =============================================================================

BEGIN;

-- ============================================================================
-- 1) Función helper: derivar estado_id desde codigo de
--    cfg_estado_documento_comercial (factura, transaccion_abonado)
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_estado_documento_comercial_id_from_codigo(p_codigo varchar)
RETURNS smallint
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE TRIM(UPPER(COALESCE(p_codigo, '')))
        WHEN 'A' THEN 1::smallint
        WHEN 'C' THEN 2::smallint
        WHEN 'N' THEN 3::smallint
        ELSE 1::smallint  -- default Activa
    END;
$$;

-- ============================================================================
-- 2) Trigger factura: sincroniza estado_id desde estado
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_factura_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.estado IS NOT NULL THEN
            NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        END IF;
    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.estado IS DISTINCT FROM OLD.estado THEN
            NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_factura_sync_estado_id ON public.factura;
CREATE TRIGGER trg_factura_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.factura
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_factura_sync_estado_id();

-- ============================================================================
-- 3) Trigger transaccion_abonado: sincroniza estado_id desde estado
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_transaccion_abonado_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.estado IS NOT NULL THEN
            NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        END IF;
    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.estado IS DISTINCT FROM OLD.estado THEN
            NEW.estado_id := public.fn_estado_documento_comercial_id_from_codigo(NEW.estado);
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_transaccion_abonado_sync_estado_id ON public.transaccion_abonado;
CREATE TRIGGER trg_transaccion_abonado_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.transaccion_abonado
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_transaccion_abonado_sync_estado_id();

-- ============================================================================
-- 4) Trigger historicomedicion: sincroniza condicion_id desde condicion
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_historicomedicion_sync_condicion_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        NEW.condicion_id := CASE TRIM(UPPER(COALESCE(NEW.condicion, '')))
            WHEN ''    THEN 0::smallint
            WHEN 'N'   THEN 1::smallint
            WHEN 'MIN' THEN 2::smallint
            WHEN 'PND' THEN 3::smallint
            WHEN 'PD'  THEN 4::smallint
            WHEN 'R'   THEN 5::smallint
            ELSE 0::smallint
        END;
    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.condicion IS DISTINCT FROM OLD.condicion THEN
            NEW.condicion_id := CASE TRIM(UPPER(COALESCE(NEW.condicion, '')))
                WHEN ''    THEN 0::smallint
                WHEN 'N'   THEN 1::smallint
                WHEN 'MIN' THEN 2::smallint
                WHEN 'PND' THEN 3::smallint
                WHEN 'PD'  THEN 4::smallint
                WHEN 'R'   THEN 5::smallint
                ELSE 0::smallint
            END;
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_historicomedicion_sync_condicion_id ON public.historicomedicion;
CREATE TRIGGER trg_historicomedicion_sync_condicion_id
    BEFORE INSERT OR UPDATE ON public.historicomedicion
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_historicomedicion_sync_condicion_id();

-- ============================================================================
-- 5) Trigger adm_cai_correlativo_emitido: estado_id desde estado_codigo
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_cai_correlativo_emitido_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.estado_codigo IS DISTINCT FROM OLD.estado_codigo) THEN
        NEW.estado_id := CASE TRIM(UPPER(COALESCE(NEW.estado_codigo, '')))
            WHEN 'PENDING_OFFLINE' THEN 1::smallint
            WHEN 'PENDING_SYNC'    THEN 2::smallint
            WHEN 'CONFIRMADO'      THEN 3::smallint
            WHEN 'SYNC_CONFLICT'   THEN 4::smallint
            WHEN 'ANULADO'         THEN 5::smallint
            ELSE 1::smallint
        END;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_cai_correlativo_emitido_sync_estado_id ON public.adm_cai_correlativo_emitido;
CREATE TRIGGER trg_cai_correlativo_emitido_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.adm_cai_correlativo_emitido
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_cai_correlativo_emitido_sync_estado_id();

-- ============================================================================
-- 6) Trigger adm_cai_bloque_reservado: estado_id desde estado_codigo
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_cai_bloque_reservado_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.estado_codigo IS DISTINCT FROM OLD.estado_codigo) THEN
        NEW.estado_id := CASE TRIM(UPPER(COALESCE(NEW.estado_codigo, '')))
            WHEN 'RESERVADO' THEN 1::smallint
            WHEN 'AGOTADO'   THEN 2::smallint
            WHEN 'EXPIRADO'  THEN 3::smallint
            ELSE 1::smallint
        END;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_cai_bloque_reservado_sync_estado_id ON public.adm_cai_bloque_reservado;
CREATE TRIGGER trg_cai_bloque_reservado_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.adm_cai_bloque_reservado
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_cai_bloque_reservado_sync_estado_id();

-- ============================================================================
-- 7) Trigger adm_lectura_v3_conflicto_sync: estado_id + codigo_conflicto_id
-- ============================================================================
CREATE OR REPLACE FUNCTION public.fn_conflicto_sync_sync_estado_id()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.estado_codigo IS DISTINCT FROM OLD.estado_codigo) THEN
        NEW.estado_id := CASE TRIM(UPPER(COALESCE(NEW.estado_codigo, '')))
            WHEN 'PENDIENTE' THEN 1::smallint
            WHEN 'REVISADO'  THEN 2::smallint
            WHEN 'CERRADO'   THEN 3::smallint
            ELSE 1::smallint
        END;
    END IF;
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.codigo_conflicto IS DISTINCT FROM OLD.codigo_conflicto) THEN
        NEW.codigo_conflicto_id := CASE TRIM(UPPER(COALESCE(NEW.codigo_conflicto, '')))
            WHEN 'SYNC_CONFIRM_ERROR'  THEN 1::smallint
            WHEN 'SYNC_CONFLICT_TOTAL' THEN 2::smallint
            WHEN 'FACTURA_YA_EMITIDA'  THEN 3::smallint
            WHEN 'CAI_VENCIDO'         THEN 4::smallint
            WHEN 'CAI_NO_ENCONTRADO'   THEN 5::smallint
            ELSE 99::smallint
        END;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_conflicto_sync_sync_estado_id ON public.adm_lectura_v3_conflicto_sync;
CREATE TRIGGER trg_conflicto_sync_sync_estado_id
    BEFORE INSERT OR UPDATE ON public.adm_lectura_v3_conflicto_sync
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_conflicto_sync_sync_estado_id();

COMMIT;

-- ============================================================================
-- Smoke test: verificar que un INSERT con estado='A' produce estado_id=1
-- ============================================================================
DO $$
DECLARE
    v_test_id integer;
    v_estado_id smallint;
BEGIN
    -- Test factura
    INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
        ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
    VALUES (2, 'TEST-TRIGGER', '090041002', 'F', '2026', '5', current_date, 'C', 'S', 1)
    RETURNING id, estado_id INTO v_test_id, v_estado_id;

    IF v_estado_id <> 2 THEN
        RAISE EXCEPTION 'Trigger factura falló: esperaba estado_id=2 (Cobrada), recibió %', v_estado_id;
    END IF;

    -- Limpieza
    DELETE FROM public.factura WHERE id = v_test_id;

    RAISE NOTICE 'Smoke test OK: trigger factura escribió estado_id=2 al recibir estado=C';
END $$;
