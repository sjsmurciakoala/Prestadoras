-- =============================================================================
-- Presupuesto: permisos del rol "Contabilidad" (el módulo NO se delega por permisos)
-- Fecha: 2026-09-07
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_rol_presupuesto_permisos.sql
--
-- ⚠️ LEER ESTO ANTES QUE EL SQL
-- El módulo de Presupuesto **no tiene ni un permiso**. Sus tres controladores abren con
-- `[Authorize(Policy = AuthorizationPolicies.Contabilidad)]`, y esa política es
-- `RequireRole(Admin, Contabilidad)`: mira el ROL, no los claims. Lo mismo hacen 4 pantallas
-- del cliente (las de configuración de presupuestos) y las 4 de compromisos de proveedor.
--
-- Consecuencia: **un rol nuevo llamado "Presupuesto" no abre nada**, por más claims que le
-- siembres. La base ya tiene un rol `Presupuesto` (medido el 2026-08-20 contra siad_v4) que
-- por esta razón no sirve para entrar. Este script NO lo toca ni lo borra.
--
-- Hoy, desde SQL, solo hay una forma de dar acceso al presupuesto: **que el usuario pertenezca
-- al rol Contabilidad** (o Admin). Eso le abre TODO el módulo de contabilidad, no solo el
-- presupuesto. Si eso no sirve, hace falta un cambio de código; está al pie de este archivo.
--
-- QUÉ HACE ESTE SCRIPT
--   1. Se asegura de que el rol `Contabilidad` exista.
--   2. Le siembra los 4 permisos de `module.contabilidad`, que sí hacen falta para los
--      endpoints contables que usan ModuleAuthorize: integración contable, lote de
--      facturación, saldos oficiales y el endpoint de ejecución presupuestaria que exige
--      `module.contabilidad.edit`.
--   3. Deja comentada la asignación de usuarios.
--
-- Los permisos del punto 2 son necesarios pero NO suficientes: sin la MEMBRESÍA en el rol,
-- las pantallas de presupuesto siguen cerradas. Los dos van juntos.
--
-- ADITIVO e IDEMPOTENTE: solo INSERT sobre `identity`, todos con guarda NOT EXISTS.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear el rol Contabilidad si falta.
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Contabilidad', 'CONTABILIDAD', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'CONTABILIDAD'
);

-- ---------------------------------------------------------------------------
-- 2. Permisos de módulo (4 claims; cubren integración, lote y saldos por cascada).
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'CONTABILIDAD'
),
permisos(valor) AS (VALUES
    ('module.contabilidad'),
    ('module.contabilidad.view'),
    ('module.contabilidad.create'),
    ('module.contabilidad.edit')
    -- `module.contabilidad.delete` queda FUERA: borrar partidas y cierres no va por rol.
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
-- 3. Membresía — esto es lo que de verdad abre el presupuesto.
--    DESCOMENTAR y poner los correos reales, en MAYÚSCULAS.
-- ---------------------------------------------------------------------------
-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('CONTABILIDAD@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'CONTABILIDAD'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación.
-- ---------------------------------------------------------------------------
\echo '=== Permisos del rol Contabilidad ==='
SELECT rc."ClaimValue" AS permiso
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" = 'CONTABILIDAD'
ORDER BY rc."ClaimValue";

\echo '=== Quiénes pueden entrar al presupuesto hoy (miembros de Admin o Contabilidad) ==='
SELECT r."Name" AS rol, u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" IN ('ADMIN', 'CONTABILIDAD')
ORDER BY r."Name", u."Email";

\echo '=== El rol Presupuesto de la base y sus claims (inertes: la politica pide ROL) ==='
SELECT r."Name" AS rol, count(rc."Id") AS claims
FROM identity."AspNetRoles" r
LEFT JOIN identity."AspNetRoleClaims" rc ON rc."RoleId" = r."Id"
WHERE r."NormalizedName" = 'PRESUPUESTO'
GROUP BY r."Name";

-- =============================================================================
-- SI SE QUIERE UN ROL "PRESUPUESTO" QUE SÍ ABRA (cambio de CÓDIGO, no de base)
-- =============================================================================
-- Son dos líneas en apc/Program.cs, dentro de AddAuthorization. Hoy dicen:
--
--   options.AddPolicy(AuthorizationPolicies.Contabilidad,
--       policy => policy.RequireRole(RoleNames.Admin, RoleNames.Contabilidad));
--   options.AddPolicy(AuthorizationPolicies.PresupuestoAprobacion,
--       policy => policy.RequireRole(RoleNames.Admin, RoleNames.Contabilidad));
--
-- Notar que las DOS piden los mismos dos roles: la política de aprobar el presupuesto
-- existe con nombre propio pero hoy no separa aprobar de editar. Agregar un rol
-- `Presupuesto` a la primera, y dejar la segunda solo para quien aprueba, sería el
-- arreglo mínimo. El cambio de fondo es migrar el módulo a ModuleAuthorize con su
-- propio recurso, como ya están Inventario, Compras y Proveedores.
--
-- ⚠️ Ojo adicional: `apc.Client/Program.cs` NO registra la política `CanContabilidad`
-- (solo las de permisos y la de Super Administrador). Funciona porque el portal renderiza
-- en InteractiveServer, donde sí está registrada; una pantalla que corriera en WASM se
-- caería. Si se toca esa política, revisar los dos Program.cs.
