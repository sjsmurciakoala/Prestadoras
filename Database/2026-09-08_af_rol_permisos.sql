-- =============================================================================
-- Roles "Activos Fijos" y "Activos Fijos Jefatura" + permisos del módulo
-- Fecha: 2026-09-08
-- Depende de: el módulo Activos Fijos publicado (2026-09-08_af_activos_fijos_f1_registro.sql)
--             y del portal con los permisos `module.activosfijos.*` en PermissionNames.
-- =============================================================================
-- Correr sobre la base ACTIVA (siad_v4 en el servidor, siad_v3_restore en el mirror):
--   psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-08_af_rol_permisos.sql
--
-- POR QUÉ
-- El módulo nace invisible: `module.activosfijos.*` no lo tiene ningún rol, así que hoy
-- solo el Super Administrador ve la sección, por su bypass. Este script crea los dos roles
-- del módulo y les siembra sus permisos.
--
-- LOS DOS ROLES Y POR QUÉ SON DOS
--   * "Activos Fijos"          -> quien lleva el control del patrimonio: registra activos,
--                                 corrige su ficha y los reasigna. NO toca los catálogos.
--   * "Activos Fijos Jefatura" -> además mantiene los tipos de activo y las ubicaciones.
-- La separación no es cosmética. El TIPO de activo lleva las CUENTAS CONTABLES, la vida útil
-- y el porcentaje residual que heredan todos sus activos: cambiar un tipo mueve la contabilidad
-- de decenas de activos a la vez. Quien captura un activo no tiene por qué poder hacer eso.
--
-- ⚠️ LA TRAMPA DE LA CASCADA (leer antes de agregar permisos a mano)
-- ModuleAuthorize resuelve del más específico al más general y el permiso de MÓDULO está
-- siempre en la cadena: endpoint -> recurso base -> módulo -> legacy (este último solo en View).
-- Es decir, `module.activosfijos.edit` habilitaría TODOS los recursos finos, catálogos incluidos.
-- Por eso NINGUNO de los dos roles lo lleva: cada uno recibe solo los permisos finos que necesita.
-- Concedérselo al rol operativo volvería inútil la separación de arriba.
--
-- ⚠️ `module.activosfijos.view` sí se concede a los dos, y abre la LECTURA de todo el módulo,
-- catálogos incluidos. Es deliberado: para registrar un activo hay que poder elegir su tipo y
-- su ubicación de la lista.
--
-- ⚠️ Lo que NINGUNO de los dos roles lleva, a propósito:
--   * `module.activosfijos.delete` — queda con el Super Administrador.
--   * Borrar un tipo de activo o una ubicación no existe como permiso: se DESACTIVAN, y eso
--     entra por `catalogos.edit`. Borrarlos dejaría huérfanos los activos que los referencian.
--
-- ADITIVO: solo INSERT sobre el esquema `identity`. No borra ni modifica nada.
-- IDEMPOTENTE: todos los INSERT llevan guarda NOT EXISTS. Se puede repetir.
--
-- Los claims son ClaimType='permission'. La autorización del portal es
-- IsInRole(SuperAdministrador) OR HasClaim(permission), y los claims del rol fluyen al
-- usuario al iniciar sesión: quien ya tenga sesión abierta debe volver a entrar.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Crear los dos roles si faltan (Id text = GUID).
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Activos Fijos', 'ACTIVOS FIJOS', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ACTIVOS FIJOS'
);

INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
SELECT gen_random_uuid()::text, 'Activos Fijos Jefatura', 'ACTIVOS FIJOS JEFATURA', gen_random_uuid()::text
WHERE NOT EXISTS (
    SELECT 1 FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ACTIVOS FIJOS JEFATURA'
);

-- ---------------------------------------------------------------------------
-- 2. Permisos del rol operativo "Activos Fijos" (4 claims).
--    Sin create/edit de módulo: cada acción se concede por su recurso fino.
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ACTIVOS FIJOS'
),
permisos(valor) AS (VALUES
    -- Acceso base: hace visible la sección del menú y abre la lectura del maestro,
    -- de los tipos y de las ubicaciones.
    ('module.activosfijos.view'),
    -- Alta y corrección de la ficha del activo.
    ('module.activosfijos.activos.create'),
    ('module.activosfijos.activos.edit'),
    -- Reasignar responsable, ubicación y centro de costo. Permiso propio porque el
    -- control físico del inventario puede recaer en alguien que no deba tocar valores
    -- de compra ni cuentas contables.
    ('module.activosfijos.activos.asignar')
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
-- 3. Permisos del rol "Activos Fijos Jefatura" (6 claims).
--    Lo mismo que el operativo MÁS el mantenimiento de los catálogos.
-- ---------------------------------------------------------------------------
WITH rol AS (
    SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'ACTIVOS FIJOS JEFATURA'
),
permisos(valor) AS (VALUES
    ('module.activosfijos.view'),
    ('module.activosfijos.activos.create'),
    ('module.activosfijos.activos.edit'),
    ('module.activosfijos.activos.asignar'),
    -- Tipos de activo y ubicaciones. Aquí vive la definición contable del módulo:
    -- las cuentas, la vida útil y el porcentaje residual que heredan los activos.
    ('module.activosfijos.catalogos.create'),
    ('module.activosfijos.catalogos.edit')
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
-- 4. OPCIONAL — lectura para Contabilidad. DESCOMENTAR si se quiere.
--    Contabilidad necesita consultar el patrimonio y las cuentas de cada tipo, pero
--    no registra activos. Es solo lectura: no incluye create, edit ni asignar.
--    Se deja apagado porque toca un rol que ya existe y eso lo decide el usuario.
-- ---------------------------------------------------------------------------
-- WITH rol AS (
--     SELECT "Id" AS role_id FROM identity."AspNetRoles" WHERE "NormalizedName" = 'CONTABILIDAD'
-- )
-- INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
-- SELECT rol.role_id, 'permission', 'module.activosfijos.view'
-- FROM rol
-- WHERE NOT EXISTS (
--     SELECT 1 FROM identity."AspNetRoleClaims" rc
--     WHERE rc."RoleId" = rol.role_id
--       AND rc."ClaimType" = 'permission'
--       AND rc."ClaimValue" = 'module.activosfijos.view'
-- );

-- ---------------------------------------------------------------------------
-- 5. Asignación a usuarios.
--    Esta es la asignación REAL que se hizo en desarrollo el 2026-09-08 y que por lo
--    tanto debe repetirse en el servidor: el módulo se probó con este usuario.
--    El correo va en MAYÚSCULAS porque se compara contra NormalizedEmail.
--
--    ⚠️ OJO CON EL DOMINIO: el correo de contabilidad dice `aguasdepuestocortes.com`,
--    no `aguasdepuertocortes.com` como el resto de la empresa. Está así en la base y
--    se respeta tal cual. Si arriba estuviera bien escrito, este INSERT no encontraría
--    al usuario y no haría NADA, en silencio: verificar con la consulta del final.
-- ---------------------------------------------------------------------------
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM identity."AspNetUsers" u
CROSS JOIN identity."AspNetRoles" r
WHERE u."NormalizedEmail" IN ('CONTABILIDAD@AGUASDEPUESTOCORTES.COM')
  AND r."NormalizedName" = 'ACTIVOS FIJOS JEFATURA'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetUserRoles" ur
      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );

--    Plantilla para el rol operativo. DESCOMENTAR y poner el correo real cuando se
--    decida quién lleva el control físico del inventario de activos.
-- INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
-- SELECT u."Id", r."Id"
-- FROM identity."AspNetUsers" u
-- CROSS JOIN identity."AspNetRoles" r
-- WHERE u."NormalizedEmail" IN ('INVENTARIOS@AGUASDEPUERTOCORTES.COM')
--   AND r."NormalizedName" = 'ACTIVOS FIJOS'
--   AND NOT EXISTS (
--       SELECT 1 FROM identity."AspNetUserRoles" ur
--       WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
--   );

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificación.
-- ---------------------------------------------------------------------------
\echo '=== Permisos de Activos Fijos y Activos Fijos Jefatura ==='
SELECT r."Name" AS rol, rc."ClaimValue" AS permiso
FROM identity."AspNetRoleClaims" rc
JOIN identity."AspNetRoles" r ON r."Id" = rc."RoleId"
WHERE r."NormalizedName" IN ('ACTIVOS FIJOS', 'ACTIVOS FIJOS JEFATURA')
ORDER BY r."Name", rc."ClaimValue";

\echo '=== Usuarios asignados ==='
SELECT r."Name" AS rol, u."Email"
FROM identity."AspNetUserRoles" ur
JOIN identity."AspNetUsers" u ON u."Id" = ur."UserId"
JOIN identity."AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."NormalizedName" IN ('ACTIVOS FIJOS', 'ACTIVOS FIJOS JEFATURA')
ORDER BY r."Name", u."Email";
