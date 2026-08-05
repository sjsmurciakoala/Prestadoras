using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class IsvCompraConfigClient
{
    private readonly HttpClient _http;
    public IsvCompraConfigClient(HttpClient http) => _http = http;

    public async Task<IsvCompraConfigDto> ObtenerAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/isv-compras", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<IsvCompraConfigDto>(ct) ?? new IsvCompraConfigDto();
    }

    public async Task<IsvCompraConfigDto> GuardarAsync(IsvCompraConfigDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync("api/almacen/isv-compras", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<IsvCompraConfigDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
}
