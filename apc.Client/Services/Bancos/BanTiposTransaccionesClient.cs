using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class BanTiposTransaccionesClient
{
    private readonly HttpClient httpClient;

    public BanTiposTransaccionesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BanTipoTransaccionListDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync(
            $"api/bancos/tipos-transacciones?companyId={companyId}",
            ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BanTipoTransaccionListDto>>(ct);
        return result ?? new List<BanTipoTransaccionListDto>();
    }
}
