using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Tenant;

namespace apc.Client.Services.Tenant;

public sealed class TenantSessionClient
{
    private readonly HttpClient httpClient;

    public TenantSessionClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<long> SwitchCompanyAsync(long companyId, CancellationToken ct = default)
    {
        var request = new TenantCompanySwitchRequest
        {
            CompanyId = companyId
        };

        var response = await httpClient.PostAsJsonAsync("api/tenant/switch", request, ct);
        response.EnsureSuccessStatusCode();

        var payload =
            await response.Content.ReadFromJsonAsync<TenantCompanySwitchResponse>(cancellationToken: ct);

        return payload?.CompanyId > 0 ? payload!.CompanyId : companyId;
    }
}
