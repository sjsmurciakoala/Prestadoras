using apc.Client.Services;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP del scorecard de proveedores. Usa las extensiones auth-aware (lanzan
/// <see cref="UnauthorizedAccessException"/> ante 401 o redirección a login) y devuelve el
/// mensaje del servidor tal cual en los errores de negocio, para que la pantalla lo muestre.
/// </summary>
public sealed class EvaluacionProveedorClient
{
    private const string BaseUrl = "api/proveedores/evaluacion";

    private readonly HttpClient _http;

    public EvaluacionProveedorClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<EvaluacionPeriodoDto>> ObtenerPeriodosAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<EvaluacionPeriodoDto>>($"{BaseUrl}/periodos", ct)
           ?? new List<EvaluacionPeriodoDto>();

    public async Task<EvaluacionPeriodoDto?> ObtenerPeriodoAsync(int periodoId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/periodos/{periodoId}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionPeriodoDto>(ct);
    }

    public async Task<EvaluacionPeriodoDto> CrearPeriodoAsync(
        EvaluacionPeriodoUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/periodos", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo completar la operación.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionPeriodoDto>(ct))!;
    }

    public async Task<EvaluacionCalculoResultadoDto> CalcularAsync(int periodoId, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(
            $"{BaseUrl}/periodos/{periodoId}/calcular", new { }, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo completar la operación.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionCalculoResultadoDto>(ct))!;
    }

    public async Task CerrarPeriodoAsync(int periodoId, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(
            $"{BaseUrl}/periodos/{periodoId}/cerrar", new { }, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo completar la operación.");
        }
    }

    public async Task<IReadOnlyList<EvaluacionRankingItemDto>> ObtenerRankingAsync(
        int periodoId, string? search = null, string? clase = null, decimal? comprasMinimas = null,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/periodos/{periodoId}/ranking"
                + Query(("search", search), ("clase", clase),
                        ("comprasMinimas", comprasMinimas?.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        return await _http.GetFromJsonAsyncWithAuthCheck<List<EvaluacionRankingItemDto>>(url, ct)
               ?? new List<EvaluacionRankingItemDto>();
    }

    public async Task<EvaluacionFichaDto?> ObtenerFichaAsync(
        int periodoId, string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var r = await _http.GetAsync(
            $"{BaseUrl}/periodos/{periodoId}/proveedores/{Uri.EscapeDataString(codigo)}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionFichaDto>(ct);
    }

    public async Task<EvaluacionFichaDto> CapturarAsync(
        int periodoId, string codigo, EvaluacionCapturaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsyncWithAuthCheck(
            $"{BaseUrl}/periodos/{periodoId}/proveedores/{Uri.EscapeDataString(codigo)}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo completar la operación.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionFichaDto>(ct))!;
    }

    public async Task<IReadOnlyList<EvaluacionCriterioDto>> ObtenerCriteriosAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<EvaluacionCriterioDto>>($"{BaseUrl}/criterios", ct)
           ?? new List<EvaluacionCriterioDto>();

    public async Task<IReadOnlyList<EvaluacionClaseDto>> ObtenerClasesAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<EvaluacionClaseDto>>($"{BaseUrl}/clases", ct)
           ?? new List<EvaluacionClaseDto>();

    // ── Impresión (F5) ───────────────────────────────────────────────────────

    /// <summary>URL del PDF de la ficha; la página lo abre en pestaña nueva.</summary>
    public static string GetFichaPdfUrl(int periodoId, string codigo)
        => $"{BaseUrl}/periodos/{periodoId}/proveedores/{Uri.EscapeDataString(codigo)}/pdf";

    /// <summary>URL del PDF del cuadro comparativo, con los mismos filtros del ranking.</summary>
    public static string GetComparativoPdfUrl(
        int periodoId, string? search = null, string? clase = null, decimal? comprasMinimas = null)
        => $"{BaseUrl}/periodos/{periodoId}/pdf"
         + Query(("search", search), ("clase", clase),
                 ("comprasMinimas", comprasMinimas?.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    // ── Catálogo (F3) ────────────────────────────────────────────────────────

    /// <summary>Catálogo completo, con los inactivos incluidos.</summary>
    public async Task<IReadOnlyList<EvaluacionCriterioDto>> ObtenerCatalogoCriteriosAsync(
        CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<EvaluacionCriterioDto>>(
               $"{BaseUrl}/criterios/catalogo", ct)
           ?? new List<EvaluacionCriterioDto>();

    public async Task<EvaluacionCriterioDto> GuardarCriterioAsync(
        int? id, EvaluacionCriterioUpsertDto dto, CancellationToken ct = default)
    {
        var r = id.HasValue
            ? await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/criterios/{id.Value}", dto, ct)
            : await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/criterios", dto, ct);

        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo guardar el criterio.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionCriterioDto>(ct))!;
    }

    public async Task EliminarCriterioAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"{BaseUrl}/criterios/{id}", ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo eliminar el criterio.");
        }
    }

    public async Task<EvaluacionClaseDto> GuardarClaseAsync(
        int? id, EvaluacionClaseUpsertDto dto, CancellationToken ct = default)
    {
        var r = id.HasValue
            ? await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/clases/{id.Value}", dto, ct)
            : await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/clases", dto, ct);

        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo guardar la clase.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<EvaluacionClaseDto>(ct))!;
    }

    public async Task EliminarClaseAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"{BaseUrl}/clases/{id}", ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo eliminar la clase.");
        }
    }

    private static string Query(params (string Nombre, string? Valor)[] partes)
    {
        var p = new List<string>();
        foreach (var (nombre, valor) in partes)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                p.Add($"{nombre}={Uri.EscapeDataString(valor)}");
            }
        }

        return p.Count == 0 ? string.Empty : "?" + string.Join("&", p);
    }
}
