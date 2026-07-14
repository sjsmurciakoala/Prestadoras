using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Impuestos;

namespace apc.Client.Services.Impuestos;

/// <summary>Cliente HTTP del mantenimiento de impuestos (catálogo global) y sus tasas.</summary>
public sealed class ImpuestosClient
{
    private const string BaseUrl = "api/impuestos";

    private readonly HttpClient _http;
    public ImpuestosClient(HttpClient http) => _http = http;

    // ----------------------------------------------------------------- impuesto

    public async Task<List<ImpuestoListItemDto>> GetAsync(ImpuestoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ImpuestoFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ImpuestoListItemDto>>(ct) ?? new();
    }

    public async Task<ImpuestoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoEditDto>(ct);
    }

    public async Task<ImpuestoDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}/detalle", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoDetalleDto>(ct);
    }

    public async Task<ImpuestoEditDto> CreateAsync(ImpuestoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(BaseUrl, dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoEditDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<ImpuestoEditDto> UpdateAsync(int id, ImpuestoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoEditDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    // --------------------------------------------------------------------- tasas

    public async Task<List<ImpuestoTasaDto>> GetTasasAsync(int impuestoId, CancellationToken ct = default)
    {
        if (impuestoId <= 0) return new();
        var r = await _http.GetAsync($"{BaseUrl}/{impuestoId}/tasas", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ImpuestoTasaDto>>(ct) ?? new();
    }

    public async Task<ImpuestoTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default)
    {
        if (tasaId <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/tasas/{tasaId}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoTasaDto>(ct);
    }

    /// <summary>Tasas vigentes a una fecha (por defecto hoy). Lookup para el motor y para selectores.</summary>
    public async Task<List<ImpuestoTasaLookupDto>> GetTasasVigentesAsync(DateOnly? fecha = null, CancellationToken ct = default)
    {
        var url = fecha.HasValue
            ? $"{BaseUrl}/tasas/vigentes?fecha={fecha.Value:yyyy-MM-dd}"
            : $"{BaseUrl}/tasas/vigentes";

        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ImpuestoTasaLookupDto>>(ct) ?? new();
    }

    public async Task<ImpuestoTasaDto> CreateTasaAsync(ImpuestoTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/tasas", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoTasaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<ImpuestoTasaDto> UpdateTasaAsync(int tasaId, ImpuestoTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/tasas/{tasaId}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoTasaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateTasaAsync(int tasaId, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/tasas/{tasaId}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    /// <summary>
    /// Cambio de tasa por decreto: cierra la vigente y crea la nueva, en una transacción
    /// del lado del servidor. Devuelve la tasa nueva.
    /// </summary>
    public async Task<ImpuestoTasaDto> CambiarTasaAsync(CambiarTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/tasas/cambiar", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ImpuestoTasaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }
}
