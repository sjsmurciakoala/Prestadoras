using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class BancoConfiguracionClient
{
    private readonly HttpClient httpClient;

    public BancoConfiguracionClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<BancoConfiguracionDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/bancos/configuracion/{companyId}", ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoConfiguracionDto>(ct);
        return result ?? new BancoConfiguracionDto();
    }

    public async Task<BancoConfiguracionDto> GuardarAsync(long companyId, BancoConfiguracionDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsyncWithAuthCheck($"api/bancos/configuracion/{companyId}", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detalle = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detalle ?? "Error al guardar la configuracion.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoConfiguracionDto>(ct);
        return result ?? dto;
    }
}
