-- Reasigna al usuario de facturación del rol 'User' (que quedó sin permisos) al rol 'Ventas'.
--
-- Contexto: al unificar la autorización en permisos, el rol 'User' quedó vacío a propósito.
-- Esta cuenta lo tenía como único rol, así que se habría quedado sin acceso.
-- Idempotente: se puede re-ejecutar sin efecto.
BEGIN;

-- 1. darle el rol Ventas si aún no lo tiene
INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM identity."AspNetUsers" u
CROSS JOIN identity."AspNetRoles" r
WHERE u."Email" = 'aguasdepuertocortesfacturacion@gmail.com'
  AND r."Name"  = 'Ventas'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetUserRoles" ur
      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );

-- 2. quitarle el rol User, que ya no aporta nada
DELETE FROM identity."AspNetUserRoles" ur
USING identity."AspNetUsers" u, identity."AspNetRoles" r
WHERE ur."UserId" = u."Id"
  AND ur."RoleId" = r."Id"
  AND u."Email" = 'aguasdepuertocortesfacturacion@gmail.com'
  AND r."Name"  = 'User';

COMMIT;
