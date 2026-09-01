using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Retenciones;

namespace apc.Client.Services.Retenciones;

/// <summary>
/// Cliente HTTP del mantenimiento de retenciones a proveedores: catálogo (concepto + tasas) y la
/// cuenta del pasivo por empresa. Calcado de <c>ImpuestosClient</c>.
/// </summary>
public sealed class RetencionesClient
{
    private const string BaseUrl = "api/retenciones";

    private readonly HttpClient _http;
    public RetencionesClient(HttpClient http) => _http = http;

    // ---------------------------------------------------------------- retención

    public async Task<List<RetencionListItemDto>> GetAsync(RetencionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new RetencionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(f.TipoImpuesto)) p.Add($"tipoImpuesto={Uri.EscapeDataString(f.TipoImpuesto)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<RetencionListItemDto>>(ct) ?? new();
    }

    public async Task<RetencionEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionEditDto>(ct);
    }

    public async Task<RetencionDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}/detalle", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionDetalleDto>(ct);
    }

    public async Task<RetencionEditDto> CreateAsync(RetencionEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(BaseUrl, dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionEditDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<RetencionEditDto> UpdateAsync(int id, RetencionEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionEditDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    // -------------------------------------------------------------------- tasas

    public async Task<List<RetencionTasaDto>> GetTasasAsync(int retencionId, CancellationToken ct = default)
    {
        if (retencionId <= 0) return new();
        var r = await _http.GetAsync($"{BaseUrl}/{retencionId}/tasas", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<RetencionTasaDto>>(ct) ?? new();
    }

    public async Task<RetencionTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default)
    {
        if (tasaId <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/tasas/{tasaId}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionTasaDto>(ct);
    }

    /// <summary>Retenciones con la tasa vigente a una fecha (por defecto hoy). Lookup (lo consumirá F2).</summary>
    public async Task<List<RetencionTasaLookupDto>> GetTasasVigentesAsync(DateOnly? fecha = null, CancellationToken ct = default)
    {
        var url = fecha.HasValue
            ? $"{BaseUrl}/tasas/vigentes?fecha={fecha.Value:yyyy-MM-dd}"
            : $"{BaseUrl}/tasas/vigentes";

        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<RetencionTasaLookupDto>>(ct) ?? new();
    }

    public async Task<RetencionTasaDto> CreateTasaAsync(RetencionTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/tasas", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionTasaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<RetencionTasaDto> UpdateTasaAsync(int tasaId, RetencionTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/tasas/{tasaId}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionTasaDto>(ct)
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
    /// Cambio de tasa por decreto: cierra la vigente y crea la nueva, en una transacción del lado
    /// del servidor. Devuelve la tasa nueva.
    /// </summary>
    public async Task<RetencionTasaDto> CambiarTasaAsync(CambiarTasaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/tasas/cambiar", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionTasaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    // --------------------------------------------- cuenta del pasivo por empresa

    /// <summary>Cuentas posteables del plan de la empresa actual, para el desplegable de selección.</summary>
    public async Task<List<CuentaPosteableDto>> GetCuentasPosteablesAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/cuentas-posteables", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CuentaPosteableDto>>(ct) ?? new();
    }

    /// <summary>La cuenta configurada para esta retención en la empresa actual (null si no hay).</summary>
    public async Task<RetencionCuentaDto?> GetCuentaAsync(int retencionId, CancellationToken ct = default)
    {
        if (retencionId <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{retencionId}/cuenta", ct);
        if (r.StatusCode is System.Net.HttpStatusCode.NoContent or System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionCuentaDto>(ct);
    }

    /// <summary>Configura (alta o actualización) la cuenta del pasivo para esta retención en la empresa actual.</summary>
    public async Task<RetencionCuentaDto> SetCuentaAsync(int retencionId, RetencionCuentaDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/{retencionId}/cuenta", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<RetencionCuentaDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía.");
    }
}
