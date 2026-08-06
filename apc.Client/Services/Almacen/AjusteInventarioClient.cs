using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente HTTP de los ajustes de inventario — <b>solo lectura del histórico</b>. La captura de
/// ajustes quedó deprecada en la Fase 4: se registra como movimiento de almacén
/// (<see cref="MovimientosAlmacenClient"/>). Este cliente conserva la consulta del histórico.
/// </summary>
public sealed class AjusteInventarioClient
{
    private readonly HttpClient _http;

    public AjusteInventarioClient(HttpClient http) => _http = http;

    public async Task<List<AjusteInventarioDto>> GetPorParAsync(int articuloId, int bodegaId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"api/almacen/ajustes?articuloId={articuloId}&bodegaId={bodegaId}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<AjusteInventarioDto>>(ct) ?? new();
    }
}
