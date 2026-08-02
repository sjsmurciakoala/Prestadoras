using apc.Client.Services;
using SIAD.Core.DTOs.Auditoria;

namespace apc.Client.Services.Auditoria;

public sealed class AuditoriaConfigClient
{
    private readonly HttpClient _http;
    public AuditoriaConfigClient(HttpClient http) => _http = http;

    public async Task<List<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<AuditoriaConfigItemDto>>("api/auditoria/configuracion", ct) ?? new();

    public async Task GuardarAsync(List<AuditoriaConfigItemDto> items, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsyncWithAuthCheck("api/auditoria/configuracion", items, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct));
    }

    public async Task<List<CatalogoTablaDisponibleDto>> TablasDisponiblesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<CatalogoTablaDisponibleDto>>("api/auditoria/configuracion/tablas-disponibles", ct) ?? new();

    public async Task AgregarAsync(AgregarMaestroDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck("api/auditoria/configuracion/catalogo", dto, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct));
    }

    public async Task DesactivarAsync(string tabla, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/auditoria/configuracion/catalogo/{Uri.EscapeDataString(tabla)}", ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct));
    }
}
