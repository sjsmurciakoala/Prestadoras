using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SIAD.Core.Constants;

namespace apc.Security;

public sealed class TenantCompanyClaimTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        if (EsIdentidadIntermedia(principal))
        {
            return Task.FromResult(principal);
        }

        if (principal.IsInRole(RoleNames.SuperAdministrador))
        {
            return Task.FromResult(principal);
        }

        var companyClaim = principal.FindFirst(TenantClaimTypes.CompanyId)?.Value;
        if (long.TryParse(companyClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId) && companyId > 0)
        {
            return Task.FromResult(principal);
        }

        throw new InvalidOperationException(
            $"El usuario autenticado no tiene el claim {TenantClaimTypes.CompanyId} requerido.");
    }

    /// <summary>
    /// Identidades intermedias de Identity: el segundo factor, el "recordar este equipo" y el
    /// login externo. Llevan solo el id del usuario, nunca pasan por la resolucion de empresa y
    /// no representan una sesion todavia. Exigirles el claim de empresa rompia el inicio de
    /// sesion en dos pasos: <c>SignInManager.GetTwoFactorAuthenticationUserAsync</c> autentica
    /// ese esquema, esta transformacion lanzaba, y quien activaba el 2FA quedaba fuera.
    /// </summary>
    private static bool EsIdentidadIntermedia(ClaimsPrincipal principal)
    {
        var esquema = principal.Identity?.AuthenticationType;
        return esquema == IdentityConstants.TwoFactorUserIdScheme
            || esquema == IdentityConstants.TwoFactorRememberMeScheme
            || esquema == IdentityConstants.ExternalScheme;
    }
}
