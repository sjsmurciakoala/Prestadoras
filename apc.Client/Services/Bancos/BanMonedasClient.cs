using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class BanMonedasClient
{
    private readonly HttpClient httpClient;

    public BanMonedasClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BanMonedaLookupDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync($"api/bancos/monedas?companyId={companyId}", ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BanMonedaLookupDto>>(ct);
        return result ?? new List<BanMonedaLookupDto>();
    }
}
