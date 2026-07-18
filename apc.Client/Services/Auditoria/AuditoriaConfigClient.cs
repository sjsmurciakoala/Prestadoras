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
}
