# Pruebas de validación del claim `TenantClaimTypes.CompanyId`

Estas pruebas cubren los escenarios manuales requeridos para garantizar que la UI no funcione sin un `CompanyId` válido en el token.

## 1. El token siempre debe contener el claim al autenticarse
1. Inicie la API y el front-end (`apc`).
2. Cree o utilice un usuario válido en Identity.
3. Inicie sesión normalmente en `/Account/Login`.
4. Abra las herramientas de desarrollo del navegador y revise las claims del usuario (por ejemplo, inspeccionando la cookie `.AspNetCore.Identity.Application` o utilizando un punto de interrupción en `PersistingServerAuthenticationStateProvider`).
5. Confirme que existe el claim `tenant_company` (`TenantClaimTypes.CompanyId`) y que su valor es mayor que cero.

## 2. Un usuario sin claim no puede navegar hasta renovar su token
1. En la base de datos `identity.AspNetUserClaims`, elimine cualquier fila con `ClaimType = 'tenant_company'` para el usuario de prueba.
2. Sin cerrar la sesión actual, intente navegar a cualquier módulo (por ejemplo, Contabilidad > Plan de Cuentas).
3. La solicitud fallará porque `TenantProvider.GetCompanyIdAsync` lanza `InvalidOperationException`, bloqueando la UI.
4. Vuelva a iniciar sesión (o asigne nuevamente el claim en la base de datos) y confirme que la navegación vuelve a funcionar.

## 3. Selección de empresa
1. Actualice el claim `tenant_company` con el identificador de una empresa válida.
2. Fuerce la renovación del token (cerrar sesión e iniciar nuevamente).
3. Acceda al módulo de Contabilidad y confirme que los servicios usan el nuevo `CompanyId`.

> Nota: Estos pasos garantizan que cualquier usuario sin claim debe renovar su token o seleccionar manualmente una empresa válida antes de continuar.
