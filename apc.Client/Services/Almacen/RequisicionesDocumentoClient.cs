using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente HTTP del documento de requisición (Fase 6, <c>api/almacen/requisiciones/documentos</c>):
/// crear/editar borrador, enviar a revisión, aprobar/rechazar, anular. Aparte de
/// <see cref="RequisicionesClient"/>, que consulta el histórico plano de SIMAFI.
/// </summary>
public sealed class RequisicionesDocumentoClient
{
    private const string BaseUrl = "api/almacen/requisiciones/documentos";
    private readonly HttpClient _http;

    public RequisicionesDocumentoClient(HttpClient http) => _http = http;

    /// <summary>URL del comprobante (vale) en PDF (inline); se muestra embebido en la vista.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

    public async Task<List<RequisicionDocumentoListItemDto>> GetAsync(
        RequisicionDocumentoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new RequisicionDocumentoFilterDto();
        var p = new List<string>();
        if (f.BodegaId is > 0) p.Add($"bodegaId={f.BodegaId}");
        if (f.Estado.HasValue) p.Add($"estado={f.Estado.Value}");
        if (f.Tipo.HasValue) p.Add($"tipo={f.Tipo.Value}");
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<RequisicionDocumentoListItemDto>>(ct) ?? new();
    }

    public async Task<RequisicionDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RequisicionDocumentoDto>(ct);
    }

    public Task<RequisicionDocumentoDto> CrearAsync(RequisicionDocumentoDto dto, CancellationToken ct = default)
        => PostDocAsync(BaseUrl, dto, "No se pudo crear la requisición.", ct);

    public async Task<RequisicionDocumentoDto> ActualizarAsync(int id, RequisicionDocumentoDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}", dto, ct);
        return await LeerDocAsync(r, "No se pudo actualizar la requisición.", ct);
    }

    public Task<RequisicionDocumentoDto> EnviarAsync(int id, CancellationToken ct = default)
        => PostAccionAsync($"{BaseUrl}/{id}/enviar", (object?)null, "No se pudo enviar a revisión.", ct);

    public Task<RequisicionDocumentoDto> AprobarAsync(int id, CancellationToken ct = default)
        => PostAccionAsync($"{BaseUrl}/{id}/aprobar", (object?)null, "No se pudo aprobar la requisición.", ct);

    public Task<RequisicionDocumentoDto> RechazarAsync(int id, string motivo, CancellationToken ct = default)
        => PostAccionAsync($"{BaseUrl}/{id}/rechazar", new { Motivo = motivo }, "No se pudo rechazar la requisición.", ct);

    public async Task<bool> AnularAsync(int id, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}/anular", new { Motivo = motivo }, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo anular la requisición.");
        return true;
    }

    private async Task<RequisicionDocumentoDto> PostDocAsync(string url, object body, string errorFallback, CancellationToken ct)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(url, body, ct);
        return await LeerDocAsync(r, errorFallback, ct);
    }

    private async Task<RequisicionDocumentoDto> PostAccionAsync(string url, object? body, string errorFallback, CancellationToken ct)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(url, body ?? new { }, ct);
        return await LeerDocAsync(r, errorFallback, ct);
    }

    private static async Task<RequisicionDocumentoDto> LeerDocAsync(HttpResponseMessage r, string errorFallback, CancellationToken ct)
    {
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? errorFallback);
        return (await r.ReadFromJsonAsyncWithAuthCheck<RequisicionDocumentoDto>(ct))!;
    }
}
