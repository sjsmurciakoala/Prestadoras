-- =====================================================
-- Script: Seed Data para Captación de Pagos
-- Usa NUEVO esquema (ddl_v2) - SIN legacy
-- Tablas: ban_banco, con_periodo_contable
-- Fecha: 2026-01-19
-- =====================================================

-- Assumir company_id = 1 (primera empresa/tenant por defecto)
-- Cambiar company_id si es necesario en tu instalación

-- =====================================================
-- 1. SEED BANCOS (ban_banco)
-- =====================================================
-- Nota: Si los bancos ya existen (constraint UNIQUE), se ignoran
INSERT INTO public.ban_banco 
    (company_id, code, nombre, activo, created_by)
VALUES 
    (1, '001', 'BANCO NACIONAL', true, 'MIGRATION'),
    (1, '002', 'BANCO POPULAR', true, 'MIGRATION'),
    (1, '003', 'BCR', true, 'MIGRATION'),
    (1, '004', 'EFECTIVO', true, 'MIGRATION'),
    (1, '005', 'DEPÓSITO EN TRÁNSITO', true, 'MIGRATION')
ON CONFLICT (company_id, code) DO NOTHING;

SELECT 'Bancos insertados: ' || COUNT(*) FROM public.ban_banco WHERE company_id = 1;

-- =====================================================
-- 2. SEED PERÍODOS CONTABLES (con_periodo_contable)
-- =====================================================
-- Crear período actual (Enero 2026) en estado 0 = ABIERTO
INSERT INTO public.con_periodo_contable 
    (company_id, code, name, start_date, end_date, status, status_id, created_by)
VALUES 
    (1, '202601', 'Enero 2026', '2026-01-01', '2026-01-31', 'ABIERTO', 0, 'MIGRATION'),
    (1, '202602', 'Febrero 2026', '2026-02-01', '2026-02-28', 'CERRADO', 2, 'MIGRATION'),
    (1, '202512', 'Diciembre 2025', '2025-12-01', '2025-12-31', 'CERRADO', 2, 'MIGRATION')
ON CONFLICT (company_id, code) DO UPDATE 
SET status = EXCLUDED.status,
    status_id = EXCLUDED.status_id;

-- Asegurar que solo un período está ABIERTO
UPDATE public.con_periodo_contable 
SET status = 'CERRADO',
    status_id = 2
WHERE company_id = 1 
  AND code <> '202601' 
  AND status_id = 0;

-- Asegurar que 202601 está ABIERTO (si existe)
UPDATE public.con_periodo_contable 
SET status = 'ABIERTO',
    status_id = 0
WHERE company_id = 1 AND code = '202601';

SELECT 'Períodos insertados: ' || COUNT(*) FROM public.con_periodo_contable WHERE company_id = 1;

-- =====================================================
-- 3. VERIFICACIÓN FINAL
-- =====================================================
DO $$
DECLARE
    v_bancos INT;
    v_periodos INT;
    v_periodo_abierto VARCHAR;
BEGIN
    SELECT COUNT(*) INTO v_bancos FROM public.ban_banco WHERE company_id = 1 AND activo = true;
    SELECT COUNT(*) INTO v_periodos FROM public.con_periodo_contable WHERE company_id = 1;
    SELECT code INTO v_periodo_abierto FROM public.con_periodo_contable 
        WHERE company_id = 1 AND status_id = 0 LIMIT 1;
    
    RAISE NOTICE '========================================';
    RAISE NOTICE 'SEED DATA CAPTACIÓN DE PAGOS - COMPLETADO';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Bancos activos: %', v_bancos;
    RAISE NOTICE 'Períodos totales: %', v_periodos;
    RAISE NOTICE 'Período ABIERTO: %', COALESCE(v_periodo_abierto, 'NINGUNO');
    RAISE NOTICE '========================================';
END $$;

-- =====================================================
-- 4. LISTADO FINAL (para verificación manual)
-- =====================================================
RAISE NOTICE 'BANCOS:';
SELECT code, nombre, activo FROM public.ban_banco WHERE company_id = 1 ORDER BY code;

RAISE NOTICE 'PERÍODOS:';
SELECT code, name, start_date, end_date, status, status_id FROM public.con_periodo_contable 
WHERE company_id = 1 ORDER BY start_date DESC;
