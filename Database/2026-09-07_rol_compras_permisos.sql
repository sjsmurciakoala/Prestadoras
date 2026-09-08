-- =============================================================================
-- Roles "Compras" y "Compras Jefatura" + permisos del módulo Compras
-- Fecha: 2026-09-07
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_rol_compras_permisos.sql
--
-- POR QUÉ
-- `module.compras` tiene CERO claims en toda la base (medido el 2026-08-20 contra siad_v4).
-- El rol "Compras" existe en el código (RoleNames.Compras) pero su permiso nunca se sembró.
--
-- LOS DOS ROLES
--   * "Compras"          -> captura órdenes, registra recepciones y facturas de compra, y
--                           opera cuentas por pagar. NO firma la orden.
--   * "Compras Jefatura" -> ve las órdenes y las APRUEBA. Nada más.
-- `module.compras.ordenes.aprobar` es el único permiso del módulo que NO hereda de
-- `module.compras.edit`: se verifica en el controlador, sin fallback. Es lo que separa a
-- quien captura de quien firma. Si querés que la misma persona haga las dos cosas, asignale
-- los dos roles en vez de mezclar los permisos.
--
-- ⚠️ LO QUE NO SE PUEDE SEPARAR HOY (limitación del código, no de este script)
-- Órdenes, recepciones, facturas de compra, pagos y la pantalla de cuentas por pagar
-- comparten los MISMOS cuatro permisos de módulo: no existe un recurso fino para pagos.
-- Quien puede registrar una factura de compra puede pagarla. Separarlo exige un recurso
-- nuevo en PermissionResources.Compras y su atributo en el controlador, es decir binario.
--
-- ⚠️ El rol operativo necesita LEER inventario y proveedores para armar una orden, y esos
-- dos permisos de lectura abren todas las pantallas de consulta de esos módulos, incluidos
-- los catálogos comerciales que comparten `module.inventario` (hallazgo 3 de
-- docs/PENDIENTES_ROLES_Y_MENU_2026-08-05.md).
--
-- ⚠️ El panel de presupuesto que aparece al aprobar la orden lo sirve el propio controlador
-- de órdenes, así que NO hace falta el rol Contabilidad para verlo. Pero el módulo de
-- Presupuesto en sí abre por ROL, no por permiso: ver 2026-09-07_rol_presupuesto_permisos.sql.
--
-- ADITIVO e IDEMPOTENTE: solo INSERT sobre `identity`, todos con guarda NOT EXISTS.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear los dos roles si faltan.
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Compras', 'COMPRAS', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMPRAS'
);

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Compras Jefatura', 'COMPRAS JEFATURA', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMPRAS JEFATURA'
);

-- ---------------------------------------------------------------------------
-- 2. Permisos del rol operativo "Compras" (8 claims).
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMPRAS'
),
permisos(valor) AS (VALUES
    -- Órdenes, recepciones, facturas de compra, pagos y cuentas por pagar.
    ('module.compras'),
    ('module.compras.view'),
    ('module.compras.create'),
    ('module.compras.edit'),
    -- Leer el catálogo de artículos y las existencias para armar la orden.
    ('module.inventario'),
    ('module.inventario.view'),
    -- Leer la ficha del proveedor, su estado de cuenta y su antigüedad de saldos.
    ('module.proveedores'),
    ('module.proveedores.view')
)
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT rol.role_id, 'permission', p.valor
FROM rol CROSS JOIN permisos p
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoleClaims" rc
    WHERE rc."RoleId" = rol.role_id
      AND rc."ClaimType" = 'permission'
      AND rc."ClaimValue" = p.valor
);

-- ---------------------------------------------------------------------------
-- 3. Permisos del rol "Compras Jefatura" (3 claims).
--    Deliberadamente sin create/edit: quien firma no captura.
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMPRAS JEFATURA'
),
permisos(valor) AS (VALUES
    ('module.compras'),
    ('module.compras.view'),
    -- El permiso de firmar. Sin fallback: es el único que habilita aprobar la orden.
    ('module.compras.ordenes.aprobar')
)
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT rol.role_id, 'permission', p.valor
FROM rol CROSS JOIN permisos p
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoleClaims" rc
    WHERE rc."RoleId" = rol.role_id
      AND rc."ClaimType" = 'permission'
      AND rc."ClaimValue" = p.valor
);

-- ---------------------------------------------------------------------------
-- 4. Asignación a usuarios — DESCOMENTAR y poner los correos reales, en MAYÚSCULAS.
-- ---------------------------------------------------------------------------
-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('COMPRAS@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'COMPRAS'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('GERENCIA@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'COMPRAS JEFATURA'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación.
-- ---------------------------------------------------------------------------
\echo '=== Permisos de Compras y Compras Jefatura ==='
SELECT r."Name" AS rol, rc."ClaimValue" AS permiso
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" IN ('COMPRAS', 'COMPRAS JEFATURA')
ORDER BY r."Name", rc."ClaimValue";

\echo '=== Usuarios asignados ==='
SELECT r."Name" AS rol, u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" IN ('COMPRAS', 'COMPRAS JEFATURA')
ORDER BY r."Name", u."Email";
