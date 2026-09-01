-- =============================================================================
-- Rol "Cobranzas" + permisos + asignación a cobranzas@aguasdepuertocortes.com
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql -U postgres -d siad_v4 -f 2026-08-05_rol_cobranzas_permisos.sql
--
-- El rol está definido en el código (RoleNames.Cobranzas) pero NUNCA se creó
-- en la BD. Este script: (1) crea el rol si falta, (2) le siembra TODOS los
-- permisos de cobranza y adyacentes (clientes, caja, notas C/D, captación),
-- (3) se lo asigna al usuario. Idempotente: se puede repetir sin duplicar.
--
-- Los claims son ClaimType='permission'. La autorización del portal es
-- IsInRole(SuperAdministrador) OR HasClaim(permission), y los claims del rol
-- fluyen al usuario al iniciar sesión.

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear el rol "Cobranzas" si no existe (Id text = GUID).
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Cobranzas', 'COBRANZAS', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COBRANZAS'
);

-- ---------------------------------------------------------------------------
-- 2. Sembrar los permisos del rol (solo los que falten).
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COBRANZAS'
),
permisos(valor) AS (VALUES
    -- Acceso base al módulo Ventas (para que la sección aparezca en el menú)
    ('module.ventas'),
    ('module.ventas.view'),
    -- Cobranza (el corazón del rol)
    ('module.ventas.cobranza.view'),
    ('module.ventas.cobranza.create'),
    ('module.ventas.cobranza.edit'),
    ('module.ventas.cobranza.delete'),
    -- Clientes (ficha, estado de cuenta, editar, no cortable)
    ('module.ventas.clientes.view'),
    ('module.ventas.clientes.create'),
    ('module.ventas.clientes.edit'),
    ('module.ventas.clientes.delete'),
    ('module.ventas.clientes.no_cortable.edit'),
    -- Caja / cobrar (incluye abono por banco)
    ('module.ventas.caja.view'),
    ('module.ventas.caja.create'),
    ('module.ventas.caja.edit'),
    ('module.ventas.caja.delete'),
    ('module.ventas.caja.abono.banco'),
    -- Notas de crédito/débito (gestión legal, ajustes)
    ('module.ventas.notas_credito_debito.view'),
    ('module.ventas.notas_credito_debito.create'),
    ('module.ventas.notas_credito_debito.edit'),
    ('module.ventas.notas_credito_debito.delete'),
    -- Captación de pagos (legacy, por compatibilidad)
    ('module.ventas.captacion_pagos.view'),
    ('module.ventas.captacion_pagos.create'),
    ('module.ventas.captacion_pagos.edit'),
    ('module.ventas.captacion_pagos.delete')
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
-- 3. Asignar el rol al usuario cobranzas@aguasdepuertocortes.com.
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM identity."AspNetUsers" u
CROSS JOIN identity."AspNetRoles" r
WHERE u."NormalizedEmail" = 'COBRANZAS@AGUASDEPUERTOCORTES.COM'
  AND r."NormalizedName" = 'COBRANZAS'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetUserRoles" ur
      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación (debe listar el rol, sus permisos y el usuario asignado).
-- ---------------------------------------------------------------------------
\echo '=== Permisos del rol Cobranzas ==='
SELECT rc."ClaimValue"
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" = 'COBRANZAS'
ORDER BY rc."ClaimValue";

\echo '=== Usuarios con el rol Cobranzas ==='
SELECT u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" = 'COBRANZAS';
