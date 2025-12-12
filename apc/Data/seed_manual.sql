-- =========================================================================
-- SEED MANUAL PARA POSTGRESQL
-- =========================================================================
-- Este script crea los datos iniciales:
-- 1. Empresa demo (APC - Aguas de Puerto Cortes)
-- 2. Usuario admin
-- 3. Rol admin
-- 4. Asignacion de rol al usuario
-- 5. Asignacion de empresa al usuario (claim tenant_company)
-- 
-- Contrasena: Admin123@
-- Email: admin@siad-demo.com
-- =========================================================================

-- Asegurar extension para gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

BEGIN;

-- =========================================================================
-- 1. CREAR ROLES SI NO EXISTEN (USANDO INSERT ... SELECT ... WHERE NOT EXISTS)
-- =========================================================================

-- Rol Admin
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Admin', 'ADMIN', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'ADMIN');

-- Rol User
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'User', 'USER', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'USER');

-- Roles por dominio
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Contabilidad', 'CONTABILIDAD', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'CONTABILIDAD');

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Compras', 'COMPRAS', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'COMPRAS');

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Ventas', 'VENTAS', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'VENTAS');

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Bancos', 'BANCOS', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'BANCOS');

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Configuracion', 'CONFIGURACION', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'CONFIGURACION');

-- =========================================================================
-- 2. CREAR EMPRESA DEMO
-- =========================================================================

INSERT INTO public.cfg_company (
  code,
  commercial_name,
  legal_name,
  tax_id,
  country_code,
  currency_code,
  timezone,
  status,
  created_at,
  created_by,
  email,
  phone,
  address
)
SELECT
  'APC',
  'Aguas de Puerto Cortes',
  'Aguas de Puerto Cortes, S.A.',
  '0000000000000',
  'HN',
  'HNL',
  'America/Tegucigalpa',
  'A',
  NOW() AT TIME ZONE 'UTC',
  'admin@siad-demo.com',
  NULL,
  NULL,
  NULL
WHERE NOT EXISTS (SELECT 1 FROM public.cfg_company c WHERE c.code = 'APC');

-- =========================================================================
-- 3. CREAR USUARIO ADMIN (si no existe)
-- =========================================================================
-- Contrasena: Admin123@
-- Hash generado con PasswordHasher (password "Admin123@")

INSERT INTO identity."AspNetUsers" (
  "Id",
  "UserName",
  "NormalizedUserName",
  "Email",
  "NormalizedEmail",
  "EmailConfirmed",
  "PasswordHash",
  "SecurityStamp",
  "ConcurrencyStamp",
  "PhoneNumber",
  "PhoneNumberConfirmed",
  "TwoFactorEnabled",
  "LockoutEnd",
  "LockoutEnabled",
  "AccessFailedCount"
)
SELECT
  gen_random_uuid()::text,
  'admin@siad-demo.com',
  'ADMIN@SIAD-DEMO.COM',
  'admin@siad-demo.com',
  'ADMIN@SIAD-DEMO.COM',
  TRUE,
  -- PasswordHash generado por PasswordHasher: password "Admin123@"
  'AQAAAAIAAYagAAAAEFQhyxX1wIMI51jeii4uVzQ47AVMa+0fEBtEKyex6lv7TR+BCc/Fpq2myyRNM65G1Q==',
  gen_random_uuid()::text,
  gen_random_uuid()::text,
  NULL,
  FALSE,
  FALSE,
  NULL,
  TRUE,
  0
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetUsers" u WHERE u."NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM');

-- =========================================================================
-- 4. ASIGNAR ROL ADMIN AL USUARIO
-- =========================================================================

WITH r AS (
  SELECT "Id" FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ADMIN' LIMIT 1
), u AS (
  SELECT "Id" FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM' LIMIT 1
)
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM u, r
WHERE NOT EXISTS (
  SELECT 1 FROM identity."AspNetUserRoles" ur WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
);

-- =========================================================================
-- 5. CREAR CONFIGURACION DE EMPRESA (con_empresa_configuracion)
-- =========================================================================

INSERT INTO public.con_empresa_configuracion (
  company_id,
  created_at,
  created_by,
  updated_at,
  updated_by
)
SELECT 
  c.company_id,
  NOW() AT TIME ZONE 'UTC',
  'admin@siad-demo.com',
  NULL,
  NULL
FROM public.cfg_company c
WHERE c.code = 'APC'
  AND NOT EXISTS (
    SELECT 1 FROM public.con_empresa_configuracion conf WHERE conf.company_id = c.company_id
  );

-- =========================================================================
-- 6. ASIGNAR EMPRESA AL USUARIO (claim tenant_company)
-- =========================================================================

WITH u AS (
  SELECT "Id" FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM' LIMIT 1
), c AS (
  SELECT company_id FROM public.cfg_company WHERE code = 'APC' LIMIT 1
)
INSERT INTO identity."AspNetUserClaims" ("UserId", "ClaimType", "ClaimValue")
SELECT u."Id", 'tenant_company', c.company_id::text
FROM u, c
WHERE NOT EXISTS (
  SELECT 1 FROM identity."AspNetUserClaims" uc
  WHERE uc."UserId" = u."Id" AND uc."ClaimType" = 'tenant_company'
);

-- =========================================================================
-- 7. ASIGNAR ROLES ADICIONALES AL USUARIO ADMIN
-- =========================================================================
WITH u AS (
  SELECT "Id" FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM' LIMIT 1
), roles AS (
  SELECT "Id", "NormalizedName" FROM identity."AspNetRoles"
  WHERE "NormalizedName" IN ('CONTABILIDAD', 'COMPRAS', 'VENTAS', 'BANCOS', 'CONFIGURACION')
)
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM u, roles r
WHERE NOT EXISTS (
  SELECT 1 FROM identity."AspNetUserRoles" ur WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
);

COMMIT;

-- =========================================================================
-- NOTAS IMPORTANTES
-- =========================================================================
-- 1) Este script usa gen_random_uuid() (pgcrypto). Si tu servidor no tiene pgcrypto,
--    reemplaza gen_random_uuid()::text por uuid_generate_v4()::text (y crea extension "uuid-ossp").
--
-- 2) Si prefieres IDs estaticos, cambia gen_random_uuid()::text por un GUID fijo.
--
-- 3) Para revertir cambios manualmente:
--    DELETE FROM identity."AspNetUserRoles" WHERE "UserId" IN (SELECT "Id" FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM');
--    DELETE FROM identity."AspNetUserClaims" WHERE "UserId" IN (SELECT "Id" FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM');
--    DELETE FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM';
--    DELETE FROM identity."AspNetRoles" WHERE "NormalizedName" IN ('ADMIN', 'USER');
--    DELETE FROM public.con_empresa_configuracion WHERE company_id = (SELECT company_id FROM public.cfg_company WHERE code = 'APC');
--    DELETE FROM public.cfg_company WHERE code = 'APC';
-- =========================================================================
