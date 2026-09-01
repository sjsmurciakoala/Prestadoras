using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Services.Tenancy;

namespace SIAD.Tests.Tenancy;

/// <summary>
/// Override de tenant por flujo async (base del barrido diario de alertas de stock, que corre sin
/// sesión). Puro, sin BD.
/// </summary>
public class TenantOverrideTests
{
    [Fact]
    public void Begin_FijaYRestaura_Anidando()
    {
        Assert.Null(TenantOverride.CompanyId);

        using (TenantOverride.Begin(7))
        {
            Assert.Equal(7, TenantOverride.CompanyId);

            using (TenantOverride.Begin(9))
            {
                Assert.Equal(9, TenantOverride.CompanyId);
            }

            Assert.Equal(7, TenantOverride.CompanyId); // restaura el valor anterior al liberar el anidado
        }

        Assert.Null(TenantOverride.CompanyId);
    }

    [Fact]
    public void CurrentCompanyService_HonraElOverride_SobreLaSesion()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null); // sin sesión
        var servicio = new CurrentCompanyService(accessor);

        Assert.Equal(0, servicio.GetCompanyId()); // sin override ni sesión → 0 (anónimo)

        using (TenantOverride.Begin(42))
        {
            Assert.Equal(42, servicio.GetCompanyId()); // el override manda
        }

        Assert.Equal(0, servicio.GetCompanyId()); // liberado → vuelve a 0
    }
}
