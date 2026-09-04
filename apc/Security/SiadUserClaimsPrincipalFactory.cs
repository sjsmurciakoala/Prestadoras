using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SIAD.Core.Constants;
using apc.Data;

namespace apc.Security;

/// <summary>
/// Construye la identidad que se serializa en la cookie de sesión, SIN los permisos de los roles.
///
/// El comportamiento por defecto de Identity mete en la cookie todos los claims de todos los roles
/// del usuario. Con ~140 permisos en el catálogo eso producía cookies de hasta 33 KB y peticiones
/// rechazadas con HTTP 431. Aquí se retiran: los repone en cada petición
/// <see cref="PermissionsClaimsTransformation"/>, leyéndolos de <see cref="RolePermissionCache"/>.
///
/// El nombre del rol SÍ se conserva en la cookie: es corto y es lo que permite reponer los permisos.
/// </summary>
public sealed class SiadUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public SiadUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        foreach (var claim in identity.FindAll(PermissionClaimTypes.Permission).ToList())
        {
            identity.RemoveClaim(claim);
        }

        return identity;
    }
}
