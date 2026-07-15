-- =============================================================================
-- 2026-07-15  Alta de cuentas contables faltantes del plan ERSAPS (company 2)
-- Fuente: plancuentasersaps (3).xlsx (contabilidad) vs con_plan_cuentas.
-- 591 cuentas nuevas. SOLO INSERTs (no toca cuentas existentes);
-- idempotente via ON CONFLICT (company_id, code) DO NOTHING.
-- Convenciones replicadas del catalogo vivo: tipo por clase (1=ACTIVO...7=GASTO),
-- level por segmentos d1|d2|d3|d4d5|d6d7|d8-d11; codigos de 12 digitos
-- (proveedores analiticos) nivel 5 colgando del grupo de nivel 4;
-- status ACTIVE, allows_posting=true en hojas / false si tiene hijas nuevas.
-- =============================================================================
BEGIN;

INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '125010103003', 'Recibido en Transferencia', 'ACTIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '12501000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '211010109163', 'EMPRESA DE SERVICIOS AGRICOLAS, S.A. DE C.V.', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '211010109168', 'CARLOS ISMAEL TERCERO RODRIGUEZ', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '211010109173', 'AUTO REPUESTOS S.A DE C.V', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '211010109174', 'TRACTOR COMPANY  S DE R.L DE C.V', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106020000', 'Tasa Ambiental', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106030000', 'Recoleccion de Deshechos', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106040000', 'Bomberos', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106050000', 'Servicios Indirectos', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106060000', 'Mantenimiento del Parque', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106070000', 'Barrido de Calles', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106080000', 'Seguridad Ciudadana', 'PASIVO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000010000', 'Servicios de Agua Potable', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000020000', 'Servicio de Alcantarillado Sanitario', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000030000', 'Tasa Ambiental', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000040000', 'Recoleccion de Deshechos', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000050000', 'Bomberos', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000060000', 'Servicios Indirectos', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000070000', 'Mantenimiento del Parque', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000080000', 'Barrido de Calles', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000090000', 'Seguridad Ciudadana', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '53000100000', 'Asistencia Social', 'INGRESO', 5, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '53000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103010000', 'Suministros y otros', 'GASTO', 5, false, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020000', 'Materiales y repuestos', 'GASTO', 5, false, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104020000', 'Amortizacion y reservas', 'GASTO', 5, false, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11102010402', 'Banco Atlantida Cta. Ahorro', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11209010001', 'Deposito a plazo fijo de Lafise #240511000237', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11209010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301010305', 'Convenios de pagos', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020101', 'Tasa Ambiental', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020102', 'Cuentas por cobrar municipal - Canon de arrendamiento', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020201', 'Recoleccion de Deshechos', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020301', 'Bomberos', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020401', 'Servicios Indirectos', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020501', 'Mantenimiento de Parques', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301020601', 'Barrido de Calles', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030101', 'Banco Occidente', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030201', 'Banco Atlantida', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030301', 'Banco BAC Credomatic', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030401', 'Banco del Pais', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030501', 'Banco Ficohsa', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030601', 'Banco de los Trabajadores', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030701', 'Banco Lafise', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030801', 'Comixprol', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301030901', 'Coompol', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031001', 'Caceenp', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031101', 'Banco Davivienda', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031201', 'Cacihl', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031301', 'Tigo Money (Telefonia Celular S.A de C.V.)', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031401', 'Banco Banrural', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031501', 'Comixven', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031601', 'Banco Promerica', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031701', 'Caja #1', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031801', 'Banco Cuscatlan', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301031901', 'DILO', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301032001', 'Cobrador #1', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11301040101', 'Cuentas a empleados', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11301040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000101', 'Cuentas a Contratistas', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000201', 'Cuentas por cobrar otros', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000301', 'Cuentas por cobrar SAR', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000401', 'Materiales por cobrar conexiones Alc. Sanitar', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000501', 'Materiales para reparaciones Alc. Sanitario', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000601', 'Deudores Varios', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000701', 'Subsidio I.H.S.S.', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000801', 'Promosas educación', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309000901', 'Cuentas por cobrar socios', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11309010101', 'Cuentas por cobrar Aseguradora Atlantida', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11309000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000102', 'Moises Roberto Colomer Aguilar', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000103', 'Nancy Lizeth Dominguez Espinal', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000104', 'Dunia Ivett Gonzales', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000106', 'Yeimi Samantha Membreño Martinez', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000107', 'Siomy Geraldin Martinez Alvarado', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000108', 'Carolina Ipsen Rodriguez', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000113', 'Ever Onan Pérez Maradiaga', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11310000114', 'Eva Alicia Muñoz Membreño', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11310000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010101', 'Inv. Tubería y accesorios Agua Potable', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010102', 'Inv. de tuberia de polietileno', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010201', 'Inv. Tubería y accesorios Alc. Sanitario', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010301', 'Materiales y Utiles de oficina', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010401', 'Materiales en Consignación', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401010501', 'Mercaderías en transito', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401020101', 'Inv. Producto quimico', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11401020201', 'Alc. Sanitario', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11401020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11409010101', 'Inventario de materiales electricos', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11409000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11409020101', 'Herramientas menores y otras', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11409000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11409030101', 'Inv. por materiales municipales', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11409000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11501010101', 'Renovacion de poliza de seguros', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11501000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '11504010101', 'Pagos a cuentas SAR', 'ACTIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '11504000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21101010734', 'Asesoria Tecmaredes TV', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21101010957', 'Vidrios y Metales de Cortes', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21101010981', 'Techno Design Computadoras S.A.', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21102010143', 'Carlos Roberto Torrez', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21102000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104010101', 'I.H.S.S. aportación patronal', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104010102', 'R.A.P. aportación patronal', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104010103', 'INFOP', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104010201', 'I.H.S.S. Aportación Empleados', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104010202', 'R.A.P. Aportación Empleados', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104020101', 'Prestaciones laborales', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104020102', 'Prestaciones Laborales - AFP Atlantida', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104020103', 'Reserva laborar - RAP', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104030101', 'Decimo Cuarto Mes', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104030102', 'Decimo Tercer Mes', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040101', 'Retencion en la Fuente', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040102', 'Prestamos bancarios de empleados', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040103', 'Embargos de empleados', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040104', 'Otras retenciones', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040105', 'Acciones Suscritas', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21104040106', 'Deducciones sobre utilidades', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21104040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21105010101', 'Impuestos sobre la renta', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21105010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21105010102', 'Impuestos sobre honorarios', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21105010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21105010103', 'Aportación solidaria', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21105010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21105010104', 'Impuesto sobre Activo Neto', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21105010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21105020101', 'Tasa de Seguridad', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21105020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106010101', 'Canon de Arrendamiento MNCPL', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106090101', 'DAMCO', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106090000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106090102', 'ENEE MNCPL', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106090000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21106090103', 'Tasa Ambiental', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21106090000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21107010101', 'Tasa de SVA   ERSAPS', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21107000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000101', 'Seguros Atlantida', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000102', 'Seguros Ficohsa', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000103', 'Seguros Equidad', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000104', 'Seguros Crefisa S.A.', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000105', 'Seguros Ficohsa - Vehiculos', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000106', 'Seguros Davivienda', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000201', 'Depositos no identificados - clientes', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000202', 'Cuentas por pagar caja chica', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000203', 'Cheques pendientes de pago', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000204', 'Promosas educación', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000301', 'Dividendos por pagar', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21109000401', 'Otros', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21109000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21201010101', 'Convenio Financiamiento Vehiculo', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21201000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010101', 'Prestamo No. 40130100024 C/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010102', 'Prestamo No.20130104943  C/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010103', 'Prestamo No.201510013573 C/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010104', 'Prestamo No.201510014087 C/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010105', 'Prestamo bco Atlantida #20130108887', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010106', 'Prestamo Lafise No.201510016527 C/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21203010107', 'Prestamo Davivienda #2532613102', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21203010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010102', 'Moises Roberto Colomer Aguilar', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010103', 'Nancy Lizeth Dominguez Espinal', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010104', 'Dunia Ivett Gonzales', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010106', 'Yeimi Samantha Membreño Martinez', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010107', 'Siomy Geraldin Martinez Alvarado', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010108', 'Carolina Ipsen Rodriguez', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010113', 'Ever Onan Pérez Maradiaga', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '21900010114', 'Eva Alicia Muñoz Membreño', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '21900010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010001', 'Prestamo No. 40130100024', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010002', 'Prestamo No.20130104943', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010003', 'Prestamo No.201510013573 L/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010004', 'Prestamo No. 20151001487 L/P', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010005', 'Prestamo bco Atlantida #20130108887', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010006', 'Prestamo Lafise No.201510016527', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '22103010007', 'Prestamo Davivienda #2532613102', 'PASIVO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '22103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '31101010101', 'Capital social', 'CAPITAL', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '31100000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '31300010101', 'Reserva Legal', 'CAPITAL', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '31300000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '31300020101', 'Reserva para contingencias', 'CAPITAL', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '31300000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '31300030101', 'Reserva por prima en venta de acciones', 'CAPITAL', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '31300000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51101010101', 'Con Medición - Doméstico', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51101000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51102010101', 'Con Medición - Comercial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51102000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51103010101', 'Con Medición - Industrial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51104010101', 'Con medición - Gubernamental', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51105010101', 'Sin Medición - Doméstico', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51105000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51106010101', 'Sin Medición - Comercial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51107010101', 'Sin Medición - Industrial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51107000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51108010101', 'Sin Medición - Gubernamental', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51108000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51201010101', 'Doméstico', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51201000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51202010101', 'Comercial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51202000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51203010101', 'Industrial', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51204010101', 'Gubernamental', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51204000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51301000101', 'Ing. x sev. ap.-conexiones', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51301000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51301000102', 'Ing. servicio conexiones de alc. sanitario', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51301000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51301010114', 'Cargo por Conexion', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51301000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51302000101', 'Ing. x serv. ap.-reconexiones', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51302000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51303000101', 'Ing. x serv. ap.-por vta. camion cisterna', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010101', 'Cambio de nombre', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010102', 'Venta de accesorios', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010103', 'Agua no facturada', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010104', 'Cambio de valvula', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010105', 'Alquiler equipo y maquinaria', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010106', 'Traslado de grifo', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010107', 'Inspeccion de fosa septica', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010108', 'Limpieza y descarga de aguas residuales', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010109', 'Reparaciones de agua y alc. sanitario', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010110', 'Constancias', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010111', 'Conexiones temporales', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010112', 'Otros servicios', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010113', 'Cambio de ubicacion', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010114', 'Ing. x serv. ap.-cortes a pedimento', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '51304010115', 'Intereses por financiamiento alc.', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '51304000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '52000010101', 'Ingresos por venta de activos', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '52000010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '52201010101', 'Intereses por cuenta de ahorro', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '52200000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '59000010101', 'Ingresos por recuperación de incobrables', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '59000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '59900010101', 'Ingresos por recuperación de incobrables', 'INGRESO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '59900000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101010101', 'Sueldos y salarios A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101010102', 'Sueldos y salarios eventuales A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101010201', 'Horas extras A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101010202', 'Horas extras eventuales A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101010301', 'Vacaciones A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101020101', 'I.H.S.S Aportacion patronal', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101030101', 'Decimo tercer mes A.P.C.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101030102', 'Decimo cuarto mes A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101030103', 'Prestaciones laborales A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101030104', 'Decimo tercer mes A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61101030105', 'Decimo cuarto mes A.P.C', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102010101', 'Servicios basicos energia electrica ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102010201', 'Servicios basicos telefonia privada (cel)-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102020101', 'Alquileres y derecho Oficna A.P', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102020201', 'Alquiler de uso de maquinaria y equipo-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102020301', 'Renta de terrenos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102020401', 'Canon de arrendamiento agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102030101', 'Mantenimiento y reparacion de edificios-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102030102', 'Mantenimiento y rep. en sistemas de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102030103', 'Mantenimiento y rep. de obras varias-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102030104', 'Mantenimiento y rep. de equipo de oficina-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102030105', 'Manten. Y Reparac. De Infraestructura', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102040101', 'Mantenimiento y rep. de equipo de transp.-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102040102', 'Mantenimiento y rep. de equipo de bombeo-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102040103', 'Mantenimiento y rep. de equipo varios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102050101', 'Servicios tecnicos y prof. de electricidad', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102050102', 'Servicios tec. y prof. de laboratorio-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102050103', 'Servicio Ceremonial Y Protocolos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102190101', 'Primas de seguro agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102190102', 'Servicio de vigilancia privada agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61102190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103010101', 'Combustibles y lubricantes agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103010102', 'Productos quimicos agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103020101', 'Uniformes y calzado agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103020102', 'Prendas y accesorios de proteccion-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103030101', 'Repuestos y accesorios varios agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103040101', 'Productos metalicos herramientas menores-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61103050101', 'Otros materiales y suministros agua potable', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61104010101', 'Dep. mob. y .eq.-prod.,mant. y oper. sist.-ap', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61104010102', 'Dep. maq. y eq.-prod., mant. y oper. sist.-ap', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61104010103', 'Dep. eq. de Transp-prod., mant. y oper. sist.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61104010104', 'Dep. herramienta -mant. y operc. sist.-ap.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201010101', 'Sueldos y salarios Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201010102', 'Sueldos y salarios eventuales Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201010201', 'Horas extras Capt. agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201010202', 'Horas extras eventuales Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201010301', 'Vacaciones Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201020101', 'I.H.S.S Aportacion Patronal Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201020102', 'RAP aportación patronal Capt. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201020301', 'INFOP Aportacion Patronal Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201030101', 'Decimo tercer mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201030102', 'Decimo Cuarto Mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201030103', 'Prestaciones Laborales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201030104', 'Decimo tercer mes eventuales Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61201030105', 'Decimo cuarto mes Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202010101', 'Energia Electrica Capt. de Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202010201', 'Telefonia privada Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202020101', 'Alquileres de edificios Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202020102', 'Alquiler de maquinaria Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202020103', 'Renta de terrenos Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202030101', 'Mantenimiento y Reparacion de edificios Capt.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202030102', 'Mantenimiento y Reparacion del sistema Capt.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202030103', 'Mantenimiento y Repracion de obras varias Cap', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202030105', 'Mantenimento y Repracion de bombas del sistem', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202040101', 'Mantenimiento y Reparacion de equipo oficina', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202040102', 'Mantenimiento y reparacion de Eq. Transporte', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202040103', 'Mantenimiento y Reparacion de eq. comunicacio', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202040104', 'Mantenimiento y Reparacion de eq. varios Capt', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202050101', 'Servicios tecnicos y profesionales de electri', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202050104', 'Servicios tecnicos y prof. Estudios y factibilidad', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202060101', 'Pasajes Viaticos y Otros Gastos de Viaje Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202190101', 'Primas de seguros Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202190102', 'Beneficios, compensaciones y capacitaciones', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61202190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61203010101', 'Combustibles y lubricantes Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61203010102', 'Alimentacion Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61203020101', 'Uniformes y Calzados Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61203020102', 'Prendas de proteccion y otros Capt. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61203060101', 'Materiales electricos y otros Capt. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301010101', 'Sueldos y salarios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301010102', 'Sueldos y salarios eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301010201', 'Horas extras transporte y elevacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301010202', 'Horas extas eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301010301', 'Vacaciones Transp. y elevacion de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301020101', 'I.H.S.S Aportacion patronal Transp. y elev. a', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301020102', 'RAP Aportacion patronal Transp. y elev. agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301020301', 'Contribucion patronal INFOP', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301020302', 'Contribucion patronal INFOP eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301030101', 'Decimo Tercer mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301030102', 'Decimo Cuarto mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301030103', 'Prestaciones Laborales Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301030104', 'Decimo tercer mes eventuales Transp. y elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61301030105', 'Decimo cuarto mes eventuales Transp. y elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61301030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302010101', 'Energia Electrica Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302010201', 'Telefonia privada Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302030101', 'Mantenimiento y reparacion de edificios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302030102', 'Mantenimiento y reparacion del sistema de agu', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302030105', 'Mant. y Rep. de equipo de bombeo Transp. y Elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302040102', 'Mantenimiento y reparacion de eq. transp.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302040104', 'Mantenimiento y Reparacion  Electrico.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302050101', 'Servicios tecnicos y profesionales electricos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302060101', 'Pasajes Viaticos y Otros Gastos de Viaje Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302190101', 'Primas de seguros Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302190102', 'Beneficios y compensaciones Transp. y Elevacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61302190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61302190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61303010101', 'Combustible y lubricantes Transp. y elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61303010102', 'Alimentacion Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61303020101', 'Uniformes y Calzados Transp. y elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61303020102', 'Prendas de Proteccion y otros Transp. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61303060101', 'Materiales y suministros electricos Tranps. y Elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61303000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401010101', 'Sueldos y salarios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401010102', 'Sueldos y Salarios eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401010201', 'Horas extras potabilizacion y desinfectacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401010202', 'Horas extras eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401010301', 'Vacaciones', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401020101', 'I.H.S.S. Contribucion patronal Potabil. y desinfeccion de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401020102', 'RAP Contribucion patronal Patabil. y desinfec. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401020103', 'INFOP Contribucion patronal Potabil. y desinfec. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401030101', 'Decimo Tercer mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401030102', 'Decimo Cuarto mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61401030103', 'Prestaciones Laborales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61401030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402010101', 'Energia Electrica Potab. y desinf. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402010201', 'Telefonia privada Potab. y Desinfec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402030103', 'Mantenimiento y reparación de infraestructura Potb. y desinf', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402040102', 'Mantenimento y reparacion del sistema P.D.A', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402050101', 'Servicios tecnicos y profesionales electricidad Potab. y Desinf. agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402050102', 'Servicios tecnicos y profesionales de Laboratorio Potab. y desinfec. del agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402060101', 'Pasajes Viaticos y Otros Gastos de Viaje Potab. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402080101', 'Productos quimicos Potab. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402080000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402190101', 'Primas de seguros Potabil. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402190102', 'Beneficio y compensaciones Potab. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61402190301', 'Otros gastos por potabilizacion y desinfeccio', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61402190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403010101', 'Combustible y Lubricantes Potab. y desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403010102', 'Alimentacion Potab. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403010103', 'Producto quimicos potabilizacion y desinfectacion del agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403020101', 'Uniformes y Calzados Potab. y Desinf.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403020102', 'Prendas de Proteccion y otros Potab. y Desinf', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61403020106', 'Otros materiales y sumistros', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61403000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61502190101', 'Primas de seguro Almcj. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61502190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61502190102', 'Beneficios y Compensaciones Almac. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61502190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61502190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61502190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61503010102', 'Alimentacion Almacj. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61503000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61503020102', 'Prendas de Proteccion y otros Almacj. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61503000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601010101', 'Sueldos y salarios Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601010102', 'Sueldos y salaraios eventuales Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601010201', 'Horas extras distribucion y elevacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601010202', 'Horas extras eventuales Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601010301', 'Vacaciones Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601020101', 'Contibucion patronal I.H.S.S', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601020102', 'RAP aportación patronal', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601020301', 'Contribucion patronal INFOP', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601020302', 'Contribucion patronal INFOP eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601030101', 'Decimo Tercer mes Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601030102', 'Decimo Cuarto mes Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61601030103', 'Prestaciones Laborales Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61601030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602010101', 'Energia Electrica Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602010201', 'Telefonia privada Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602010202', 'Correo e internet', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602020102', 'Alquiler de maquinaria y equipo Distr. y Eleva.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602030101', 'Mantenimiento y reparacion de edificios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602030102', 'Mantenimiento y reparacion del sistema de agu', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602030103', 'Mantenimiento y reparación de infraestructuras Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602030104', 'Mantenimiento y reparación de Eq. Oficina Distrib. y Eleva. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602040101', 'Mantenimieto y reparacion de eq. transp. Distrib. y elevac. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602040102', 'Mant. y rep. del sistema de bombeo AP', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602040103', 'Mant. y Reparacion de equipos varios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602040104', 'Mantenimiento y reparación electrico', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602050101', 'Servicios tecnicos y profesionales Electricos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602050103', 'Serv. Tecn y profesionales de capacitaón Distrib. y elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602050104', 'Serv. Tecn. y Prof. estudios, factibilidad, investigación y proyect.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602050105', 'Servicios Tecn. y profesionales varios Distr. y Elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602070102', 'Matriculas de vehiculos Distrib. y elevacion de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602190101', 'Primas de seguros Distr. y Elev. Agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602190102', 'Beneficios y Compensaciones Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61602190302', 'Servicios de vigilancia Distrib. y Elevac. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61602190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603010101', 'Combustible y lubricantes Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603010102', 'Alimentacion Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603020101', 'Uniformes y Calzados Distrib. y Eleva.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603020102', 'Prendas de Proteccion y otros Distrib. y Elevac.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603020104', 'Materiales de oficina Distrib. y Elev.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603030101', 'Respuestos y accesorios varios Distrib. y elevac. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603030102', 'Llantas y neumaticos Distrib. y elevac. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603040101', 'Herramientas menores Distrib. y elevacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603050102', 'Productos y materiales de limpieza Distrib. y Elevac. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61603060101', 'Materiales y utiles electricos Distrib. y eleva. de agua', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61603000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604010101', 'Gasto por depreciacion de Mobiliario y eq.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604010102', 'Gasto por depreciacion de vehiculos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604010103', 'Gasto por depreciacion de eq. comunicacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604010104', 'Gasto por depreciacion de Maquinaria y eq.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604010105', 'Gasto por depreciacion de herramientas', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '61604020101', 'Gasto por amortizar activos intangibles', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '61604000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101010101', 'Sueldos y salarios Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101010102', 'Sueldos y salarios eventual Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101010201', 'Horas extras redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101010202', 'Horas extras eventual alc. sanitario', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101010301', 'Vacaciones Redes de Recolec', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101020101', 'Contribucion patronal I.H.S.S. Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101020102', 'RAP aportación patronal Redes de Recl.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101020104', 'Contribuciones patronales eventuales-as', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101020301', 'Contribucion patronal INFOP', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101020302', 'Contribiucion patronal INFOP eventuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101030101', 'Decimo Tercer Mes Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101030102', 'Decimo Cuarto Mes Redes de Recolec', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101030103', 'Prestaciones Laborales Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101030104', 'Decimo tercer mes eventuales Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62101030105', 'Decimo cuarto mes eventuales Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102010101', 'Servicios basicos energia electrica-as', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102010201', 'Telefonia privada Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102020101', 'Alquileres de edificios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102020102', 'Alquiler de maquinaria', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102030101', 'Mantenimiento y reparacion de edificios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102030102', 'Mantenimiento y reparación de Eq. Oficina Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102030201', 'Mantenimiento y rep. en sistemas de alcantari', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102030202', 'Mantenimiento y rep. de obras varias', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040101', 'Mant. y rep. de equipo de transporte', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040102', 'Mantenimiento y rep de equipo de bombeo', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040103', 'Mantenimiento y rep. de equipo electrico', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040104', 'Mantenimiento y rep. de equipo especializado', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040105', 'Mantenimiento y repracion de pozos puntas Redes de Recolecc.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102040106', 'Matenimiento y reparación de eq. varios Red. Recolecc.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102050102', 'Serv. Tecnicos y profecionales de capacitación Red. Recol.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102050103', 'Servicios tecnicos y profesionales varios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102060101', 'Pasajes Viaticos y Otros Gastos de Viaje Redes Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102070101', 'Impuestos, Tasas y  Derechos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102070102', 'Matriculas de vehiculos Redes de Recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102190101', 'Primas de seguros Redes de Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102190102', 'Beneficios, Capacitaciones  y Compensacion Redes de Recolecc.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62102190302', 'Servicios de vigilancia Redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103010101', 'Combustibles y lubricantes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103010102', 'Alimentacion Redes de Recolecc.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103020101', 'Uniformes y calzado', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103020102', 'Prendas y accesorios de proteccion Redes Recolec.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103020104', 'Materiales de oficina Redes de Recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103030101', 'Repuestos y accesorios varios', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103030102', 'Llantas y neumaticos Redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103040101', 'Herramientas menores Redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103050101', 'Otros materiales y suministros Redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103050102', 'Productos y materiales de limpieza Redes de recoleccion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62103060101', 'Utiles y materiales electricos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104010101', 'Gasto por depreciacion de Mobiliario y eq.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104010102', 'Gasto por depreciacion de Vehiculos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104010103', 'Gasto por depreciacion de eq. comunicacion', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104010104', 'Gastos por depreciacion de maquinaria y eq.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104010105', 'Gasto por depreciacion de herramientas', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62104020101', 'Gastos por amortizar actvos intangibles', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201010101', 'Sueldos y salarios Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201010102', 'Sueldos y salarios eventuales Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201010201', 'Horas extras Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201010202', 'Horas extras eventuales Depuracion aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201010301', 'Vacaciones Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201020101', 'I.H.S.S contribu. patronal Depuracion de aguas resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201020102', 'RAP Contrib. patronal Depuracion Aguas. resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201020103', 'INFOP Contrib. patronal Depuracion aguas resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201030101', 'Decimo Tercer mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201030102', 'Decimo Cuarto mes', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201030103', 'Prestaciones Laborales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201030104', 'Decimo tercer mes eventuales Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62201030105', 'Decimo cuarto mes Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62201030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202010201', 'Telefonia privada Depuracion Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202020101', 'Alquiler de locales Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202020103', 'Renta de Terrenos Depurac. Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202030201', 'Mantenimiento de lagunas', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202040104', 'Mantenimiento electrico - Depuración de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202050102', 'Servicios tecnicos y profesionales de Laboratorio Dep. Aguas', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202050104', 'Estudios, Invest. y Proyecto de Factibilidad', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202060101', 'Pasajes Viaticos y Otros Gastos de Viaje Depur. Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202190101', 'Primas de seguros Depur. Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202190102', 'Beneficios y Compensacion Depur. Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62202190302', 'Servicio de vigilancia Depuración de Aguas R.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203010101', 'Combustibles y lubricantes Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203010102', 'Producto quimico Depur. Agua Resid', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203010103', 'Alimentacion Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203020101', 'Uniformes y calzado Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203020102', 'Prendas de Proteccion y otros Depur. Aguas Resid.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203020104', 'Materiales y utiles Depuracion de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62203040101', 'Herramientas menores Depuración de aguas residuales', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62203000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62204010101', 'Dep. mobiliario y equipo mant. y oper. sist', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62204000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62204010102', 'Dep. maquinaria y equipo mant. y oper. sist', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62204000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62204010103', 'Dep. eq. de transporte.-mant. y oper. sist.', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62204000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '62204010104', 'Dep. herramientas mant. operac. sistemas', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '62204000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '63102190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '63102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '63202190301', 'Otros gastos', 'COSTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '63202190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101010101', 'Sueldos y salarios por comercializacion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101010102', 'Sueldos y salarios eventuales comercializacio', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101010201', 'Horas Extras atencion al cliente', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101010202', 'Horas extras - eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101010301', 'Vacaciones', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101020101', 'I.H.S.S.', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101020102', 'RAP aportación patronal Comercialización', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101020201', 'R.A.P.', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101020301', 'Contribucion patronal INFOP', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101030101', 'Decimo Tercer Mes', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101030102', 'Decimo Cuarto Mes', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71101030103', 'Prestaciones Laborales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102030101', 'Mantenimiento y reparacion de edificios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102030102', 'Mantenimiento y reparacion de eq. oficina', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102060101', 'Pasajes viaticos y otros gastos de viajes', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102190101', 'Primas de seguros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102190102', 'Otros servicios no especificos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102190103', 'Beneficios, compensaciones y capacitaciones', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71102190302', 'Sevicio de vigilancia - Atención al cliente', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71103010101', 'Productos alimenticios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71103010103', 'Combustible y lubricantes - Comercial', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71103020101', 'Uniformes y calzados', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71103020102', 'Prendas de proteccion y otros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71103000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71202050301', 'Otros gastos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71202050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71206010101', 'Gasto por perdiada en cuentas incobrables Serv. Agua Potable', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71206000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '71206010102', 'Gasto por perdiada en ctas. incibrables Alc. Sanitario', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '71206000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010101', 'Sueldos y salarios administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010102', 'Sueldos Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010103', 'Sueldos Bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010104', 'Sueldos y salarios eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010201', 'Horas extras administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010202', 'Horas extras bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010203', 'Horas extras eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010301', 'Vacaciones administración', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010302', 'Vacaciones administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101010303', 'Vacaciones bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020101', 'I.H.S.S. aportación patronal Admon', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020102', 'I.H.S.S. Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020201', 'RAP aportación patronal Admon', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020301', 'INFOP Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020302', 'INFOP Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020303', 'INFOP Bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101020304', 'INFOP Eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030101', 'Decimo tercer mes Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030102', 'Decimo tercer mes administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030103', 'Decimo tercer mes bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030104', 'Decimo tercer mes eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030201', 'Decimo cuarto mes Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030202', 'Decimo cuarto mes administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030203', 'Decimo cuarto mes bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030204', 'Decimo cuarto mes eventuales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030301', 'Prestaciones laborales Administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030302', 'Prestaciones laborales administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030303', 'Prestaciones laborales bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72101030401', 'Gasto - AFP Atlantida', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72101030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102010101', 'Energia Electrica Admon', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102010201', 'Telefonia HONDUTEL', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102010202', 'Telefonia plan corporativo', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102010203', 'Correo e internet', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102020101', 'Alquiler de edificios y locales', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102020102', 'Alquiler de mobiliario y equipo', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102030101', 'Mantenimiento y reparacion de edificios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102030000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102040101', 'Mantenimiento y reparacion de oficina', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102040102', 'Mantenimiento y reparacion de transporte', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102040103', 'Mnatenimiento y reparacion de comunicacion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102040000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050101', 'Servicios tecnicos y profesionales juridicos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050102', 'Servicios tecnicos y profesionales de capacit', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050103', 'Servicios tecnicos y profesionales de informa', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050104', 'Servicios tecnicos y profesionales de auditor', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050105', 'Servicios tecnicos y profesionales de electri', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050106', 'Servicios tecnicos y profesionales varios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102050107', 'Serv. Tecn. y Profes. de Consultoria', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102050000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102060101', 'Pasajes y viaticos junta directiva', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102060102', 'Pasajes y viaticos jefatura', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102060103', 'Pasajes y viaticos administración', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102060000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102070101', 'Tasa e impuestos municipales y otros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102070102', 'Matriculas', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102070103', 'Recargos e intereses sobre impuesto', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102070104', 'Licencias de microsoft office y Backup', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102070000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190100', 'Beneficios y compensaciones', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190101', 'Primas de seguros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190102', 'Beneficios y compensaciones administracion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190103', 'Beneficios y compensaciones bodega', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190200', 'Servicios comerciales y financieros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190201', 'Gastos y comisiones bancarias', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190202', 'Primas de seguro', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190203', 'Tasa de seguridad', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190204', 'Propaganda y publicidad', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190205', 'Vigilancia privada', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190206', 'Donaciones a particulares e instituciones', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190300', 'Otros servicios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190301', 'Otros gastos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72102190302', 'Servicio ceremonial y prot- admon', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72102190000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103010101', 'Alimentacion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103010102', 'Uniformes y calzados', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103010103', 'Combustibles y lubricantes', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103010104', 'Prendas de proteccion y otros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103010000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020101', 'Llantas y neumaticos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020102', 'Repuestos y accesorios', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020103', 'Productos para limpieza', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020104', 'Materiales y utiles de oficina', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020105', 'Materiales electricos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020106', 'Otros materiales y suministros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72103020107', 'Productos fumigacion, insecticidas y otros', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72103020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104010100', 'Depreciaciones', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104010101', 'Gasto por depreciacion de mobiliario y equipo', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104010102', 'Gasto por depreciacion de vehiculos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104010103', 'Gasto por depreciacion de equipo de comunicacion', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104020101', 'Amortizacion de activos intangibles', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72104020102', 'Reserva por cuentas incobrables', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72104020000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '72106010101', 'Canon de arrendamiento MNCPL', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '72106000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '73102010101', 'Intereses sobre prestamos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '73102000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '75901010101', 'Otros gastos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '70000000000'
ON CONFLICT (company_id, code) DO NOTHING;
INSERT INTO public.con_plan_cuentas (company_id, parent_account_id, code, name, account_type, level, allows_posting, status, created_at, created_by)
SELECT 2, p.account_id, '79401010101', 'Perdida por baja de activos fijos', 'GASTO', 6, true, 'ACTIVE', now(), 'seed-ersaps-20260715'
FROM public.con_plan_cuentas p WHERE p.company_id = 2 AND p.code = '79400000000'
ON CONFLICT (company_id, code) DO NOTHING;

-- Verificacion: todas insertadas
DO $$
DECLARE v_total int;
BEGIN
    SELECT count(*) INTO v_total FROM public.con_plan_cuentas WHERE company_id = 2 AND created_by = 'seed-ersaps-20260715';
    RAISE NOTICE 'Cuentas insertadas por el seed: %', v_total;
END $$;
COMMIT;
