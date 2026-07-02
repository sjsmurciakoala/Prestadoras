using System.Globalization;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.DTOs.Rutas;

namespace apc.Client.Services.Informes;

public sealed class InformesClient
{
    private readonly HttpClient _http;

    public InformesClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<InformeCatalogoItemDto>> GetCatalogoAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<InformeCatalogoItemDto>>("api/informes/catalogo", ct)
            ?? [];
    }

    public async Task<IReadOnlyList<ServicioCategoriaLookupDto>> GetCategoriasServicioAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<ServicioCategoriaLookupDto>>("api/informes/catalogos/categorias-servicio", ct)
            ?? [];
    }

    public async Task<IReadOnlyList<CicloLookupDto>> GetCiclosAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<CicloLookupDto>>("api/informes/catalogos/ciclos", ct)
            ?? [];
    }

    public async Task<IReadOnlyList<UsuarioInformeLookupDto>> GetUsuariosRecibosAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<UsuarioInformeLookupDto>>("api/informes/catalogos/usuarios-recibos", ct)
            ?? [];
    }

    public async Task<PartidasInformeResultadoDto> ConsultarPartidasAsync(PartidasInformeFiltroDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var query = new List<string>();

        Append(query, "periodId", filtro.PeriodId);
        Append(query, "journalId", filtro.JournalId);
        Append(query, "typeId", filtro.TypeId);
        Append(query, "status", filtro.Status);
        Append(query, "fechaDesde", filtro.FechaDesde?.ToString("O", CultureInfo.InvariantCulture));
        Append(query, "fechaHasta", filtro.FechaHasta?.ToString("O", CultureInfo.InvariantCulture));
        Append(query, "search", filtro.Search);
        Append(query, "skip", filtro.Skip);
        Append(query, "take", filtro.Take);

        var url = "api/informes/consultas/partidas-contabilidad";
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        return await _http.GetFromJsonAsync<PartidasInformeResultadoDto>(url, ct)
            ?? new PartidasInformeResultadoDto([], 0, 0m, 0m);
    }

    public async Task<IReadOnlyList<ReporteDisenoCatalogoItemDto>> GetReportesDisenoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/informes/reportes", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ReporteDisenoCatalogoItemDto>>(ct) ?? [];
    }

    public async Task<IReadOnlyList<ReporteDatasetCatalogoItemDto>> GetReportesDatasetsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/informes/reportes/datasets", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ReporteDatasetCatalogoItemDto>>(ct) ?? [];
    }

    public async Task<ReporteDatasetDetalleDto> GetReporteDatasetAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/informes/reportes/datasets/{Uri.EscapeDataString(codigo)}", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDatasetDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del dataset.");
    }

    public async Task<ReporteDisenoDetalleDto> GetReporteDisenoAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/informes/reportes/{Uri.EscapeDataString(codigo)}", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDisenoDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del reporte.");
    }

    public async Task<ReporteDatasetDetalleDto> CreateReporteDatasetAsync(ReporteDatasetCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/informes/reportes/datasets", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDatasetDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del dataset creado.");
    }

    public async Task<ReporteDatasetDetalleDto> UpdateReporteDatasetAsync(string codigo, ReporteDatasetCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PutAsJsonAsyncWithAuthCheck($"api/informes/reportes/datasets/{Uri.EscapeDataString(codigo)}", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDatasetDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del dataset actualizado.");
    }

    public async Task DeleteReporteDatasetAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/informes/reportes/datasets/{Uri.EscapeDataString(codigo)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(message ?? "No fue posible eliminar el dataset.");
        }
    }

    public async Task<ReporteDatasetPreviewResultDto> PreviewReporteDatasetAsync(string codigo, ReporteDatasetPreviewRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsyncWithAuthCheck($"api/informes/reportes/datasets/{Uri.EscapeDataString(codigo)}/probar", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDatasetPreviewResultDto>(ct)
            ?? throw new HttpRequestException("No se recibió el resultado del preview.");
    }

    public async Task<ReporteDisenoDetalleDto> CreateReporteDisenoAsync(ReporteDisenoCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/informes/reportes", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDisenoDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del reporte creado.");
    }

    public async Task<ReporteDisenoDetalleDto> PublishReporteDisenoAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/informes/reportes/{Uri.EscapeDataString(codigo)}/publicar", null, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDisenoDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del reporte publicado.");
    }

    public async Task<ReporteDisenoDetalleDto> RegenerateReporteDraftAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/informes/reportes/{Uri.EscapeDataString(codigo)}/regenerar-borrador", null, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ReporteDisenoDetalleDto>(ct)
            ?? throw new HttpRequestException("No se recibió la definición del reporte regenerado.");
    }

    public async Task DeleteReporteDisenoAsync(string codigo, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/informes/reportes/{Uri.EscapeDataString(codigo)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Su sesion ha expirado. Por favor, inicie sesion nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(message ?? "No fue posible eliminar el reporte.");
        }
    }

    private static void Append(List<string> query, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        var raw = value switch
        {
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(raw)}");
    }
}
