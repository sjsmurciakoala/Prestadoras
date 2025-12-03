using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
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

        var companyClaim = principal.FindFirst(TenantClaimTypes.CompanyId)?.Value;
        if (long.TryParse(companyClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId) && companyId > 0)
        {
            return Task.FromResult(principal);
        }

        throw new InvalidOperationException(
            $"El usuario autenticado no tiene el claim {TenantClaimTypes.CompanyId} requerido.");
    }
}
