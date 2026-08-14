using System;
using Microsoft.AspNetCore.Http;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Services.Tenancy;

public sealed class CurrentCompanyService : ICurrentCompanyService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCompanyService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long GetCompanyId()
    {
        // Override de batch/background (procesos sin sesión, p. ej. el barrido diario de alertas de
        // stock): tiene prioridad sobre el claim. En un request normal nadie lo fija. Ver TenantOverride.
        if (TenantOverride.CompanyId is long empresaFijada && empresaFijada > 0)
        {
            return empresaFijada;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var claimValue = user?.FindFirst(TenantClaimTypes.CompanyId)?.Value;
        if (long.TryParse(claimValue, out var companyId) && companyId > 0)
        {
            return companyId;
        }

        if (user?.Identity?.IsAuthenticated == true)
        {
            throw new InvalidOperationException(
                $"El usuario autenticado no tiene el claim {TenantClaimTypes.CompanyId}.");
        }

        // Requests sin usuario autenticado (p.ej. login, branding) no deberian explotar; devolvemos
        // 0 para desactivar el scoping de tenant en esos casos y permitir endpoints anonimos.
        return 0;
    }
}
