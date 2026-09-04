using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Mantenimientos;

namespace apc.Client.Services.Mantenimientos;

public sealed class FormatosFiscalesClient
{
    private const string BaseUrl = "api/mantenimientos/formatos-fiscales";

    private readonly HttpClient _http;

    public FormatosFiscalesClient(HttpClient http) => _http = http;

    public async Task<List<FormatoFiscalListItemDto>> GetAsync(FormatoFiscalFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new FormatoFiscalFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<FormatoFiscalListItemDto>>(ct) ?? new();
    }

    /// <summary>Formatos activos. Endpoint sin permiso de módulo: lo consumen las vistas que capturan el dato.</summary>
    public async Task<List<FormatoFiscalLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<FormatoFiscalLookupDto>>(ct) ?? new();
    }

    public async Task<FormatoFiscalEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<FormatoFiscalEditDto>(ct);
    }

    public async Task<FormatoFiscalEditDto> CreateAsync(FormatoFiscalEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar el formato.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<FormatoFiscalEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<FormatoFiscalEditDto> UpdateAsync(int id, FormatoFiscalEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar el formato.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<FormatoFiscalEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
