using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SIAD.Core.Constants;

namespace apc.Security;

/// <summary>
/// Inyecta en cada petición los permisos que dan los roles del usuario, y después aplica la
/// validación de empresa de <see cref="TenantCompanyClaimTransformation"/>.
///
/// Por qué aquí y no en la cookie: <see cref="SiadUserClaimsPrincipalFactory"/> deja los permisos
/// FUERA de la cookie de sesión a propósito. Si viajaran dentro, un usuario con varios roles
/// llegaba a una cookie de 33 KB y el servidor devolvía HTTP 431. Resolviéndolos aquí la cookie
/// queda en ~1 KB y deja de importar cuánto crezca el catálogo de permisos.
///
/// ASP.NET Core resuelve UNA sola <see cref="IClaimsTransformation"/>, por eso esta clase encadena
/// explícitamente la validación de tenant en vez de registrarse una segunda.
/// </summary>
public sealed class PermissionsClaimsTransformation : IClaimsTransformation
{
    private readonly RolePermissionCache _permisos;
    private readonly TenantCompanyClaimTransformation _tenant;

    public PermissionsClaimsTransformation(
        RolePermissionCache permisos,
        TenantCompanyClaimTransformation tenant)
    {
        _permisos = permisos;
        _tenant = tenant;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated == true &&
            principal.Identity is ClaimsIdentity identity)
        {
            var yaTiene = principal
                .FindAll(PermissionClaimTypes.Permission)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Se materializa antes del bucle: añadir claims dentro invalidaría el enumerador.
            var roles = principal.FindAll(identity.RoleClaimType).Select(c => c.Value).ToList();

            foreach (var rol in roles)
            {
                foreach (var permiso in await _permisos.ObtenerPermisosAsync(rol))
                {
                    // TransformAsync puede ejecutarse más de una vez por petición.
                    if (yaTiene.Add(permiso))
                    {
                        identity.AddClaim(new Claim(PermissionClaimTypes.Permission, permiso));
                    }
                }
            }
        }

        return await _tenant.TransformAsync(principal);
    }
}
