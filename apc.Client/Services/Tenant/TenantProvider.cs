using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using SIAD.Core.Constants;

namespace apc.Client.Services.Tenant;

public sealed class TenantProvider : ITenantProvider
{
    private readonly AuthenticationStateProvider authenticationStateProvider;

    public TenantProvider(AuthenticationStateProvider authenticationStateProvider)
    {
        this.authenticationStateProvider = authenticationStateProvider;
    }

    public async ValueTask<long> GetCompanyIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var claimValue = state.User.FindFirst(TenantClaimTypes.CompanyId)?.Value;

        if (long.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId) &&
            companyId > 0)
        {
            return companyId;
        }

        throw new InvalidOperationException(
            $"El token actual no contiene el claim {TenantClaimTypes.CompanyId}. Seleccione una empresa o vuelva a iniciar sesión.");
    }
}
