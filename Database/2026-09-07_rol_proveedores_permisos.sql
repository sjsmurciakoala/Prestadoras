-- =============================================================================
-- Rol "Proveedores" + permisos del módulo Proveedores
-- Fecha: 2026-09-07
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_rol_proveedores_permisos.sql
--
-- POR QUÉ
-- `module.proveedores` solo tiene los 4 permisos genéricos del Super Administrador (medido el
-- 2026-08-20 contra siad_v4). El módulo existe completo en el portal y nadie más lo puede abrir.
--
-- QUÉ ABRE ESTE ROL
--   * Maestro de proveedores: ficha, contactos, cuentas bancarias, tipos.
--   * Estado de cuenta y antigüedad de saldos.
--   * Registro de retenciones y su declaración.
--   * Evaluación de proveedores y sus criterios.
--   * Incidencias de recepción.
--
-- ⚠️ CUATRO CLAIMS, NO ONCE. El módulo define 11 permisos, pero los 7 finos caen por cascada
-- en los de módulo: `module.proveedores.view` ya habilita retenciones, estado de cuenta y
-- antigüedad (todos son solo de lectura), y `module.proveedores.edit` ya habilita evaluación
-- e incidencias. Sembrar los 11 no restringe más y engorda la cookie de permisos, que ya dio
-- HTTP 400 con 208 claims (hallazgo 4 de docs/PENDIENTES_ROLES_Y_MENU_2026-08-05.md).
-- Si querés que los finos se vean marcados en /parametros/roles, agregalos ahí, no aquí.
--
-- ⚠️ QUÉ **NO** ABRE, aunque la pantalla viva bajo /proveedores:
--   * Cuentas por pagar y los pagos: son `module.compras` (ver el script de Compras).
--   * Compromisos de proveedor: sus 4 pantallas exigen el ROL Admin o Contabilidad, no un
--     permiso. Un rol nuevo NO las abre por más claims que tenga.
--   * Catálogo de retenciones (/mantenimientos/retenciones): es
--     `module.configuracion.retenciones`, que hoy tiene cero claims en toda la base.
--   * El botón "emitir cheque manual" de la lista: exige el rol Bancos.
--
-- ADITIVO e IDEMPOTENTE: solo INSERT sobre `identity`, todos con guarda NOT EXISTS.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear el rol si falta.
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Proveedores', 'PROVEEDORES', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'PROVEEDORES'
);

-- ---------------------------------------------------------------------------
-- 2. Permisos del rol (4 claims que cubren los 11 por cascada).
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'PROVEEDORES'
),
permisos(valor) AS (VALUES
    -- Acceso base al módulo (permiso legacy: solo lectura).
    ('module.proveedores'),
    -- Cubre ver: maestro, retenciones, estado de cuenta, antigüedad, evaluación, incidencias.
    ('module.proveedores.view'),
    -- Alta de proveedores, contactos y cuentas bancarias.
    ('module.proveedores.create'),
    -- Cubre editar: maestro, evaluación e incidencias de recepción.
    ('module.proveedores.edit')
    -- `module.proveedores.delete` queda FUERA a propósito: borrar un proveedor con historial
    -- de compras y retenciones se decide caso por caso, no por rol.
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
-- 3. Asignación a usuarios — DESCOMENTAR y poner los correos reales, en MAYÚSCULAS.
-- ---------------------------------------------------------------------------
-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('PROVEEDORES@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'PROVEEDORES'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación.
-- ---------------------------------------------------------------------------
\echo '=== Permisos del rol Proveedores ==='
SELECT rc."ClaimValue" AS permiso
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" = 'PROVEEDORES'
ORDER BY rc."ClaimValue";

\echo '=== Usuarios con el rol Proveedores ==='
SELECT u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" = 'PROVEEDORES'
ORDER BY u."Email";
