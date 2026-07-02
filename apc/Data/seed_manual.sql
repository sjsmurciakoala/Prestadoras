-- =========================================================================
-- SEED MANUAL PARA POSTGRESQL
-- =========================================================================
-- Este script crea los datos iniciales:
-- 1. Empresa demo (APC - Aguas de Puerto Cortes)
-- 2. Usuario admin (Super Administrador)
-- 3. Rol Super Administrador
-- 4. Asignacion de rol al usuario
-- 5. Asignacion de empresa al usuario (claim tenant_company)
-- 6. Asignacion de permisos (role claims)
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

-- Rol Super Administrador
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Super Administrador', 'SUPER ADMINISTRADOR', gen_random_uuid()::text
WHERE NOT EXISTS (SELECT 1 FROM identity."AspNetRoles" r WHERE r."NormalizedName" = 'SUPER ADMINISTRADOR');

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
-- 4. ASIGNAR ROL SUPER ADMINISTRADOR AL USUARIO
-- =========================================================================

WITH r AS (
  SELECT "Id" FROM identity."AspNetRoles" WHERE "NormalizedName" = 'SUPER ADMINISTRADOR' LIMIT 1
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
-- 7. ASIGNAR PERMISOS AL ROL SUPER ADMINISTRADOR (role claims)
-- =========================================================================

WITH r AS (
  SELECT "Id" FROM identity."AspNetRoles" WHERE "NormalizedName" = 'SUPER ADMINISTRADOR' LIMIT 1
)
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.perm
FROM r
CROSS JOIN (VALUES
  ('module.ventas.view'),
  ('module.ventas.create'),
  ('module.ventas.edit'),
  ('module.ventas.delete'),
  ('module.bancos.view'),
  ('module.bancos.create'),
  ('module.bancos.edit'),
  ('module.bancos.delete'),
  ('module.compras.view'),
  ('module.compras.create'),
  ('module.compras.edit'),
  ('module.compras.delete'),
  ('module.proveedores.view'),
  ('module.proveedores.create'),
  ('module.proveedores.edit'),
  ('module.proveedores.delete'),
  ('module.inventario.view'),
  ('module.inventario.create'),
  ('module.inventario.edit'),
  ('module.inventario.delete'),
  ('module.contabilidad.view'),
  ('module.contabilidad.create'),
  ('module.contabilidad.edit'),
  ('module.contabilidad.delete'),
  ('module.reporteria.view'),
  ('module.reporteria.create'),
  ('module.reporteria.edit'),
  ('module.reporteria.delete'),
  ('module.configuracion.view'),
  ('module.configuracion.create'),
  ('module.configuracion.edit'),
  ('module.configuracion.delete'),
  ('module.ventas.clientes.view'),
  ('module.ventas.clientes.create'),
  ('module.ventas.clientes.edit'),
  ('module.ventas.clientes.delete'),
  ('module.ventas.captacion_pagos.view'),
  ('module.ventas.captacion_pagos.create'),
  ('module.ventas.captacion_pagos.edit'),
  ('module.ventas.captacion_pagos.delete'),
  ('module.ventas.cobranza.view'),
  ('module.ventas.cobranza.create'),
  ('module.ventas.cobranza.edit'),
  ('module.ventas.cobranza.delete'),
  ('module.ventas.facturacion_miscelaneos.view'),
  ('module.ventas.facturacion_miscelaneos.create'),
  ('module.ventas.facturacion_miscelaneos.edit'),
  ('module.ventas.facturacion_miscelaneos.delete'),
  ('module.ventas.notas_credito_debito.view'),
  ('module.ventas.notas_credito_debito.create'),
  ('module.ventas.notas_credito_debito.edit'),
  ('module.ventas.notas_credito_debito.delete'),
  ('module.ventas.captacion_pagos__captacionpagos.create'),
  ('module.ventas.captacion_pagos__captacionpagos_arqueos.view'),
  ('module.ventas.captacion_pagos__captacionpagos_arqueos_paged.view'),
  ('module.ventas.captacion_pagos__captacionpagos_bancos.view'),
  ('module.ventas.captacion_pagos__captacionpagos_cajas.view'),
  ('module.ventas.captacion_pagos__captacionpagos_clientes.view'),
  ('module.ventas.captacion_pagos__captacionpagos_miscelaneos.view'),
  ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_paged.view'),
  ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_registrar.create'),
  ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_reverso.edit'),
  ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_recibo_detalle.view'),
  ('module.ventas.captacion_pagos__captacionpagos_periodo_actual.view'),
  ('module.ventas.captacion_pagos__captacionpagos_posteo_manual.edit'),
  ('module.ventas.captacion_pagos__captacionpagos_posteo_manual_reverso.edit'),
  ('module.ventas.captacion_pagos__captacionpagos_reverso.edit'),
  ('module.ventas.captacion_pagos__captacionpagos_saldos_manual_clienteclave.view'),
  ('module.ventas.captacion_pagos__captacionpagos_search_term.view'),
  ('module.ventas.captacion_pagos__captacionpagos_numfactura.view'),
  ('module.ventas.captacion_pagos__captacionpagos_numfactura_existe.view'),
  ('module.ventas.clientes__clientes.view'),
  ('module.ventas.clientes__clientes.create'),
  ('module.ventas.clientes__clientes_foto_medidor_ide_imagen.view'),
  ('module.ventas.clientes__clientes_search.view'),
  ('module.ventas.clientes__clientes_search_paged.view'),
  ('module.ventas.clientes__clientes_id.view'),
  ('module.ventas.clientes__clientes_id.edit'),
  ('module.ventas.clientes__clientes_id_configuracion_tarifa.edit'),
  ('module.ventas.clientes__clientes_id_configuracion_tarifa_agregar.create'),
  ('module.ventas.clientes__clientes_id_configuracion_tarifa_detalle.view'),
  ('module.ventas.clientes__clientes_id_configuracion_tarifa_header.view'),
  ('module.ventas.clientes__clientes_id_estado_cuenta.view'),
  ('module.ventas.clientes__clientes_id_foto_medidor.view'),
  ('module.ventas.clientes__clientes_id_foto_medidor_header.view'),
  ('module.ventas.clientes__clientes_id_historico_consumo.view'),
  ('module.ventas.clientes__clientes_id_historico_consumo_paged.view'),
  ('module.ventas.clientes__clientes_id_movimientos.view'),
  ('module.ventas.clientes__clientes_id_movimientos_paged.view'),
  ('module.ventas.clientes__clientes_id_tarifas.view'),
  ('module.ventas.cobranza__cobranza_clientes_clave_bloqueo.view'),
  ('module.ventas.cobranza__cobranza_clientes_clave_saldos.view'),
  ('module.ventas.cobranza__cobranza_numero_letras.view'),
  ('module.ventas.cobranza__cobranza_planes.view'),
  ('module.ventas.cobranza__cobranza_planes.create'),
  ('module.ventas.cobranza__cobranza_planes_calcular.view'),
  ('module.ventas.cobranza__cobranza_planes_correlativo.view'),
  ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_categorias.view'),
  ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes.view'),
  ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes_clave.view'),
  ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos.create'),
  ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos_numero.view'),
  ('module.ventas.notas_credito_debito__facturacion_notas.create'),
  ('module.ventas.notas_credito_debito__facturacion_notas_clientes.view'),
  ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave.view'),
  ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave_configuracion.view'),
  ('module.ventas.notas_credito_debito__facturacion_notas_motivos.view'),
  ('module.ventas.notas_credito_debito__facturacion_notas_motivos_id.view')
) AS v(perm)
WHERE NOT EXISTS (
  SELECT 1 FROM identity."AspNetRoleClaims" rc
  WHERE rc."RoleId" = r."Id" AND rc."ClaimType" = 'permission' AND rc."ClaimValue" = v.perm
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
--    DELETE FROM identity."AspNetRoleClaims" WHERE "RoleId" IN (SELECT "Id" FROM identity."AspNetRoles" WHERE "NormalizedName" = 'SUPER ADMINISTRADOR');
--    DELETE FROM identity."AspNetUsers" WHERE "NormalizedUserName" = 'ADMIN@SIAD-DEMO.COM';
--    DELETE FROM identity."AspNetRoles" WHERE "NormalizedName" = 'SUPER ADMINISTRADOR';
--    DELETE FROM public.con_empresa_configuracion WHERE company_id = (SELECT company_id FROM public.cfg_company WHERE code = 'APC');
--    DELETE FROM public.cfg_company WHERE code = 'APC';
-- =========================================================================
