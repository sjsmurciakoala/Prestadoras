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
        try
        {
            var result = await httpClient.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<TenantCompanyDto>>(
                "api/tenant/companies", ct);
            return result ?? Array.Empty<TenantCompanyDto>();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener la lista de empresas.", ex);
        }
    }
}
