using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente HTTP del traslado entre bodegas (<c>api/almacen/traslados</c>): envío (con recepción o
/// directo), recepción parcial y anulación.
/// </summary>
public sealed class TrasladosClient
{
    private const string BaseUrl = "api/almacen/traslados";
    private readonly HttpClient _http;

    public TrasladosClient(HttpClient http) => _http = http;

    /// <summary>URL del comprobante (vale) del traslado —el envío— en PDF, para abrirlo en una pestaña nueva.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

    /// <summary>URL del comprobante de una recepción del traslado en PDF.</summary>
    public static string GetRecepcionComprobantePdfUrl(int id, int recepcionId)
        => $"/{BaseUrl}/{id}/recepciones/{recepcionId}/comprobante/pdf";

    public async Task<List<TrasladoListItemDto>> GetAsync(TrasladoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new TrasladoFilterDto();
        var p = new List<string>();
        if (f.BodegaOrigenId is > 0) p.Add($"bodegaOrigenId={f.BodegaOrigenId}");
        if (f.BodegaDestinoId is > 0) p.Add($"bodegaDestinoId={f.BodegaDestinoId}");
        if (f.Estado.HasValue) p.Add($"estado={f.Estado.Value}");
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TrasladoListItemDto>>(ct) ?? new();
    }

    public async Task<TrasladoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<TrasladoDto>(ct);
    }

    /// <summary>Envía (crea) el traslado. Lanza con el mensaje del servidor si algo se rechaza.</summary>
    public async Task<TrasladoDto> EnviarAsync(TrasladoDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar el traslado.");
        return (await r.ReadFromJsonAsyncWithAuthCheck<TrasladoDto>(ct))!;
    }

    /// <summary>Recibe una tanda (recepción parcial).</summary>
    public async Task<TrasladoDto> RecibirAsync(int trasladoId, RecepcionTrasladoDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{trasladoId}/recibir", dto, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar la recepción.");
        return (await r.ReadFromJsonAsyncWithAuthCheck<TrasladoDto>(ct))!;
    }

    /// <summary>Anula el traslado (reversa). Devuelve false si ya no existe.</summary>
    public async Task<bool> AnularAsync(int id, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}/anular", new { Motivo = motivo }, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo anular el traslado.");
        return true;
    }
}
