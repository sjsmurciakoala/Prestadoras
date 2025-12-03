using System.Net.Http.Json;
using SIAD.Core.DTOs.Tenant;

namespace apc.Client.Services.Tenant;

public sealed class TenantCompaniesClient
{
    private readonly HttpClient httpClient;

    public TenantCompaniesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TenantCompanyDto>> ObtenerAsync(CancellationToken ct = default)
    {
        var result = await httpClient.GetFromJsonAsync<IReadOnlyList<TenantCompanyDto>>("api/tenant/companies",
            cancellationToken: ct);
        return result ?? Array.Empty<TenantCompanyDto>();
    }
}
