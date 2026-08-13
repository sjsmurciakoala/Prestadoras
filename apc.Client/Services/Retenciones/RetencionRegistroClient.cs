using apc.Client.Services;
using SIAD.Core.DTOs.Retenciones;

namespace apc.Client.Services.Retenciones;

/// <summary>
/// Cliente HTTP de la consulta del registro fiscal de retenciones aplicadas (F4): libro hdr/dtl.
/// Solo lectura. Calcado del patrón de <c>RetencionesClient</c> (extensiones auth-aware).
/// </summary>
public sealed class RetencionRegistroClient
{
    private const string BaseUrl = "api/proveedores/retenciones";

    private readonly HttpClient _http;
    public RetencionRegistroClient(HttpClient http) => _http = http;

    public async Task<RetencionRegistroPagedResultDto> BuscarAsync(
        RetencionRegistroFilterDto filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new RetencionRegistroFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) p.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (f.EstadoId.HasValue) p.Add($"estadoId={f.EstadoId.Value}");
        if (f.Folio.HasValue) p.Add($"folio={f.Folio.Value}");
        p.Add($"skip={f.Skip}");
        p.Add($"take={f.Take}");
        if (!string.IsNullOrWhiteSpace(f.SortField)) p.Add($"sortField={Uri.EscapeDataString(f.SortField)}");
        p.Add($"sortDescending={(f.SortDescending ? "true" : "false")}");

        var url = $"{BaseUrl}?{string.Join("&", p)}";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionRegistroPagedResultDto>(ct) ?? new();
    }

    public async Task<RetencionRegistroDetalleDto?> GetDetalleAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionRegistroDetalleDto>(ct);
    }

    /// <summary>URL (relativa a la app) del PDF de la constancia; se abre en pestaña nueva con JS.</summary>
    public string GetConstanciaPdfUrl(long id) => $"{BaseUrl}/{id}/constancia/pdf";

    /// <summary>Reporte mensual para la declaración (F5): filas a nivel detalle en el rango de fechas.</summary>
    public async Task<IReadOnlyList<RetencionDeclaracionLineaDto>> BuscarDeclaracionAsync(
        RetencionDeclaracionFilterDto filtro, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/declaracion{DeclaracionQuery(filtro ?? new RetencionDeclaracionFilterDto())}";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<RetencionDeclaracionLineaDto>>(ct)
               ?? new List<RetencionDeclaracionLineaDto>();
    }

    /// <summary>URL (relativa a la app) del PDF del reporte mensual; se abre en pestaña nueva. Mismo filtro que la pantalla.</summary>
    public string GetDeclaracionPdfUrl(RetencionDeclaracionFilterDto filtro)
        => $"{BaseUrl}/declaracion/pdf{DeclaracionQuery(filtro ?? new RetencionDeclaracionFilterDto())}";

    private static string DeclaracionQuery(RetencionDeclaracionFilterDto f)
    {
        var p = new List<string>();
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (f.EstadoId.HasValue) p.Add($"estadoId={f.EstadoId.Value}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        return p.Count > 0 ? $"?{string.Join("&", p)}" : string.Empty;
    }
}
