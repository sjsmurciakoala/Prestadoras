-- =============================================================================
-- Rol "Comercial" + permisos + asignación a comercial@aguasdepuertocortes.com
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql -U postgres -d siad_v4 -f 2026-08-05_rol_comercial_permisos.sql
--
-- Set LIMPIO (Opción A, ensayo 2026-08-05): incluye solo lo que está bien
-- separado por permisos. NO incluye Solicitudes / Ciclos / Libretas /
-- Medidores porque en el código comparten module.inventario con el Almacén
-- (darlos abriría el almacén). Esos se suman cuando se separe el permiso
-- (Opción C) en un publish posterior.
--
-- Comercial puede: Clientes (+ Tarifario/cliente-servicio, que se protege con
-- el permiso de Clientes), Facturación misceláneos, Notas C/D, Condiciones de
-- lectura, Cobranza SOLO VER, Reportería. Idempotente.

BEGIN;

-- 1. Crear el rol "Comercial" si no existe.
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Comercial', 'COMERCIAL', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMERCIAL'
);

-- 2. Sembrar los permisos (solo los que falten).
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'COMERCIAL'
),
permisos(valor) AS (VALUES
    -- Acceso base al módulo Ventas (para que la sección aparezca)
    ('module.ventas'),
    ('module.ventas.view'),
    -- Clientes COMPLETO (cubre también Tarifario/cliente-servicio, que se
    -- protege con el permiso de Clientes)
    ('module.ventas.clientes.view'),
    ('module.ventas.clientes.create'),
    ('module.ventas.clientes.edit'),
    ('module.ventas.clientes.delete'),
    ('module.ventas.clientes.no_cortable.edit'),
    -- Facturación misceláneos COMPLETO
    ('module.ventas.facturacion_miscelaneos.view'),
    ('module.ventas.facturacion_miscelaneos.create'),
    ('module.ventas.facturacion_miscelaneos.edit'),
    ('module.ventas.facturacion_miscelaneos.delete'),
    -- Notas Crédito/Débito COMPLETO
    ('module.ventas.notas_credito_debito.view'),
    ('module.ventas.notas_credito_debito.create'),
    ('module.ventas.notas_credito_debito.edit'),
    ('module.ventas.notas_credito_debito.delete'),
    -- Condiciones de lectura (permiso derivado del ModuleAuthorize)
    ('module.ventas.condiciones_lectura.view'),
    ('module.ventas.condiciones_lectura.edit'),
    -- Cobranza SOLO VER (consulta el estado, no gestiona cobros)
    ('module.ventas.cobranza.view'),
    -- Reportería (ver informes comerciales)
    ('module.reporteria'),
    ('module.reporteria.view')
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

-- 3. Asignar el rol al usuario comercial@aguasdepuertocortes.com.
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM identity."AspNetUsers" u
CROSS JOIN identity."AspNetRoles" r
WHERE u."NormalizedEmail" = 'COMERCIAL@AGUASDEPUERTOCORTES.COM'
  AND r."NormalizedName" = 'COMERCIAL'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetUserRoles" ur
      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );

COMMIT;

\echo '=== Permisos del rol Comercial ==='
SELECT rc."ClaimValue"
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" = 'COMERCIAL'
ORDER BY rc."ClaimValue";

\echo '=== Usuarios con el rol Comercial ==='
SELECT u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" = 'COMERCIAL';
