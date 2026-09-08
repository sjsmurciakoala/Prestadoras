-- =============================================================================
-- Roles "Almacen" y "Almacen Jefatura" + permisos del módulo Inventario
-- Fecha: 2026-09-07
-- =============================================================================
-- Correr en el servidor sobre la base ACTIVA (siad_v4):
--   psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_rol_almacen_permisos.sql
--
-- POR QUÉ
-- Medido el 2026-08-20 contra siad_v4: `module.inventario` solo tiene los 4 permisos
-- genéricos del Super Administrador. Hoy nadie más puede usar Almacén. Este script crea
-- los dos roles del módulo y les siembra sus permisos.
--
-- LOS DOS ROLES Y POR QUÉ SON DOS
--   * "Almacen"          -> el bodeguero: captura movimientos, traslados, requisiciones y
--                           descargos. NO firma, NO toca catálogos, NO ajusta existencia.
--   * "Almacen Jefatura" -> supervisa: mantiene catálogos, ajusta existencia, APRUEBA
--                           requisiciones y AUTORIZA movimientos sensibles.
-- Aprobar la requisición y autorizar movimientos sensibles son los dos únicos permisos del
-- módulo que NO heredan de `module.inventario.edit`: se verifican en el controlador. Son la
-- razón de que existan dos roles; si van juntos, quien captura firma su propio documento.
--
-- ⚠️ LA TRAMPA DE LA CASCADA (leer antes de agregar permisos a mano)
-- ModuleAuthorize resuelve del más específico al más general y el permiso de MÓDULO está
-- siempre en la cadena. Es decir: `module.inventario.edit` habilita TODOS los recursos finos
-- (ajustes, carga inicial, conceptos de movimiento). Por eso el rol operativo NO lo lleva:
-- lleva solo los permisos finos que necesita. Agregarle `module.inventario.edit` le abriría
-- el módulo entero y volvería inútil la separación.
--
-- ⚠️ `module.inventario.view` abre TAMBIÉN los catálogos comerciales (medidores, clases de
-- medidor), que comparten el permiso de módulo con Almacén. Es el hallazgo 3 de
-- docs/PENDIENTES_ROLES_Y_MENU_2026-08-05.md y no se arregla desde SQL.
--
-- ⚠️ Lo que NINGUNO de los dos roles lleva, a propósito:
--   * `module.inventario.delete` — borrar documentos de almacén queda con el Super Administrador.
--   * `module.configuracion` — CERRAR y REABRIR el corte de inventario lo exige, y conceder
--     configuración abriría todo el módulo de configuración.
--
-- ADITIVO: solo INSERT sobre el esquema `identity`. No borra ni modifica nada.
-- IDEMPOTENTE: todos los INSERT llevan guarda NOT EXISTS. Se puede repetir.
--
-- Los claims son ClaimType='permission'. La autorización del portal es
-- IsInRole(SuperAdministrador) OR HasClaim(permission), y los claims del rol fluyen al
-- usuario al iniciar sesión.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear los dos roles si faltan (Id text = GUID).
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Almacen', 'ALMACEN', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ALMACEN'
);

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Almacen Jefatura', 'ALMACEN JEFATURA', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ALMACEN JEFATURA'
);

-- ---------------------------------------------------------------------------
-- 2. Permisos del rol operativo "Almacen" (10 claims).
--    Sin create/edit de módulo: cada documento se concede por su recurso fino.
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ALMACEN'
),
permisos(valor) AS (VALUES
    -- Acceso base al módulo (para que la sección aparezca y para leer artículos,
    -- kardex, existencias por bodega y valuación).
    ('module.inventario'),
    ('module.inventario.view'),
    -- Movimientos manuales de entrada y salida.
    ('module.inventario.movimientos.create'),
    ('module.inventario.movimientos.edit'),
    -- Traslados entre bodegas (envío y recepción).
    ('module.inventario.traslados.create'),
    ('module.inventario.traslados.edit'),
    -- Requisiciones: las CAPTURA, no las aprueba.
    ('module.inventario.requisiciones.create'),
    ('module.inventario.requisiciones.edit'),
    -- Descargos: la salida real que postea al kardex.
    ('module.inventario.descargos.create'),
    ('module.inventario.descargos.edit')
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
-- 3. Permisos del rol "Almacen Jefatura" (6 claims).
--    Aquí SÍ va create/edit de módulo: la jefatura mantiene catálogos y ajusta.
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ALMACEN JEFATURA'
),
permisos(valor) AS (VALUES
    ('module.inventario'),
    ('module.inventario.view'),
    -- Cubre por cascada tipos de artículo, bodegas, unidades, conceptos de movimiento,
    -- términos de pago, ISV en compras, ajustes de inventario y carga inicial.
    ('module.inventario.create'),
    ('module.inventario.edit'),
    -- Los dos permisos que NO heredan de nada: se verifican en el controlador.
    ('module.inventario.requisiciones.aprobar'),
    ('module.inventario.movimientos.autorizar_sensibles')
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
-- 4. Asignación a usuarios — DESCOMENTAR y poner los correos reales.
--    El correo va en MAYÚSCULAS (es NormalizedEmail). Si el correo no existe el
--    INSERT no hace nada, así que dejarlo mal escrito falla en silencio: verificar
--    con la consulta del final.
-- ---------------------------------------------------------------------------
-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('BODEGA@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'ALMACEN'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('JEFEALMACEN@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'ALMACEN JEFATURA'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación.
-- ---------------------------------------------------------------------------
\echo '=== Permisos de Almacen y Almacen Jefatura ==='
SELECT r."Name" AS rol, rc."ClaimValue" AS permiso
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" IN ('ALMACEN', 'ALMACEN JEFATURA')
ORDER BY r."Name", rc."ClaimValue";

\echo '=== Usuarios asignados ==='
SELECT r."Name" AS rol, u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" IN ('ALMACEN', 'ALMACEN JEFATURA')
ORDER BY r."Name", u."Email";
