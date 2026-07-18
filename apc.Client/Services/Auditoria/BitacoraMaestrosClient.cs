using apc.Client.Services;
using SIAD.Core.DTOs.Auditoria;

namespace apc.Client.Services.Auditoria;

public sealed class BitacoraMaestrosClient
{
    private readonly HttpClient _http;
    public BitacoraMaestrosClient(HttpClient http) => _http = http;

    public async Task<List<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto f, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (f.Desde is { } d) qs.Add($"desde={d:yyyy-MM-dd}");
        if (f.Hasta is { } h) qs.Add($"hasta={h:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Modulo)) qs.Add($"modulo={Uri.EscapeDataString(f.Modulo)}");
        if (!string.IsNullOrWhiteSpace(f.Tabla)) qs.Add($"tabla={Uri.EscapeDataString(f.Tabla)}");
        if (!string.IsNullOrWhiteSpace(f.Accion)) qs.Add($"accion={Uri.EscapeDataString(f.Accion)}");
        if (!string.IsNullOrWhiteSpace(f.Usuario)) qs.Add($"usuario={Uri.EscapeDataString(f.Usuario)}");
        var url = "api/auditoria/bitacora-maestros" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsyncWithAuthCheck<List<BitacoraMaestroListItemDto>>(url, ct) ?? new();
    }
}
