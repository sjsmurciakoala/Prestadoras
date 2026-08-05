using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente HTTP del documento de descargo (Fase 6, <c>api/almacen/descargos/documentos</c>): entregar
/// (crea y postea) y anular. Aparte de <see cref="DescargosClient"/>, que consulta el histórico plano.
/// </summary>
public sealed class DescargosDocumentoClient
{
    private const string BaseUrl = "api/almacen/descargos/documentos";
    private readonly HttpClient _http;

    public DescargosDocumentoClient(HttpClient http) => _http = http;

    /// <summary>URL del comprobante (vale de salida) en PDF, para abrirlo en una pestaña nueva.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

    public async Task<List<DescargoDocumentoListItemDto>> GetAsync(
        DescargoDocumentoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new DescargoDocumentoFilterDto();
        var p = new List<string>();
        if (f.BodegaId is > 0) p.Add($"bodegaId={f.BodegaId}");
        if (f.Estado.HasValue) p.Add($"estado={f.Estado.Value}");
        if (f.RequisicionHdrId is > 0) p.Add($"requisicionHdrId={f.RequisicionHdrId}");
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<DescargoDocumentoListItemDto>>(ct) ?? new();
    }

    public async Task<DescargoDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<DescargoDocumentoDto>(ct);
    }

    /// <summary>Entrega (crea y postea) el descargo. Lanza con el mensaje del servidor si se rechaza.</summary>
    public async Task<DescargoDocumentoDto> EntregarAsync(DescargoDocumentoDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar la entrega.");
        return (await r.ReadFromJsonAsyncWithAuthCheck<DescargoDocumentoDto>(ct))!;
    }

    public async Task<bool> AnularAsync(int id, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}/anular", new { Motivo = motivo }, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo anular el descargo.");
        return true;
    }
}
