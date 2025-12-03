using SIAD.Core.DTOs.Tenant;

namespace SIAD.Services.Tenancy;

public interface ITenantCompanyService
{
    Task<IReadOnlyList<TenantCompanyDto>> ObtenerEmpresasAsync(CancellationToken ct = default);

    Task<bool> ExisteEmpresaAsync(long companyId, CancellationToken ct = default);
}
