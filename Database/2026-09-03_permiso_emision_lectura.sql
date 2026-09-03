-- Permiso de la emisión de factura de lectura desde el portal (2026-09-03).
--
-- El permiso `module.ventas.emision_lectura.*` nació DESPUÉS de
-- 2026-09-01_permisos_por_rol.sql, así que ningún rol lo tenía y la pantalla quedaba
-- inalcanzable salvo para el Super Administrador, que la ve por el bypass global.
--
-- Ninguno de los dos permisos hereda de `module.ventas.*`, y es a propósito: la pantalla no
-- tiene nada que consultar —solo emite—, y cada emisión consume un correlativo CAI que deja un
-- documento fiscal. Si `view` cayera a `module.ventas.view`, los roles de solo lectura verían
-- una opción de menú que al pulsar «Emitir» les respondería 403.
--
-- Se concede al rol **Ventas**, que es el que ya tiene `module.ventas.create` y agrupa a las
-- cuentas de facturación, y al **Super Administrador** por coherencia con el resto del catálogo
-- (aunque el bypass global lo haría innecesario).
--
-- Deliberadamente NO se concede a Cobranzas ni a Comercial: hoy solo tienen `module.ventas.view`
-- sobre el módulo, o sea consulta. Si se decide que deben facturar, se les agrega aquí.
--
-- ADITIVO / bajo riesgo. IDEMPOTENTE: se puede re-ejecutar (INSERT … WHERE NOT EXISTS).
-- Aplica sobre el esquema identity. Requiere que los roles ya existan.
--
-- ⚠️ Los permisos de rol se cachean 10 minutos en memoria (RolePermissionCache): tras aplicar
--    esto hay que REINICIAR el portal para que el cambio se vea de inmediato.

BEGIN;

INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.ventas.emision_lectura.view'),
        ('module.ventas.emision_lectura.create')
) AS v(permiso)
WHERE r."Name" IN ('Ventas', 'Super Administrador')
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id"
        AND c."ClaimType" = 'permission'
        AND c."ClaimValue" = v.permiso);

COMMIT;

-- Verificación:
--   SELECT r."Name", c."ClaimValue"
--   FROM identity."AspNetRoles" r
--   JOIN identity."AspNetRoleClaims" c ON c."RoleId" = r."Id"
--   WHERE c."ClaimValue" LIKE '%emision_lectura%'
--   ORDER BY 1, 2;
--
-- Debe devolver 4 filas: view y create para Ventas y para Super Administrador.
