using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>Cliente HTTP del interruptor de existencia negativa por empresa.</summary>
public sealed class NegativoInventarioConfigClient
{
    private readonly HttpClient _http;
    public NegativoInventarioConfigClient(HttpClient http) => _http = http;

    public async Task<NegativoInventarioConfigDto> ObtenerAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/existencia-negativa", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<NegativoInventarioConfigDto>(ct) ?? new NegativoInventarioConfigDto();
    }

    public async Task<NegativoInventarioConfigDto> GuardarAsync(NegativoInventarioConfigDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync("api/almacen/existencia-negativa", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<NegativoInventarioConfigDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
}
