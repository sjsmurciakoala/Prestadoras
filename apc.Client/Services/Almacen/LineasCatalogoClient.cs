using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class LineasCatalogoClient
{
    private readonly HttpClient _http;
    public LineasCatalogoClient(HttpClient http) => _http = http;

    public async Task<List<LineaListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/lineas?{string.Join("&", p)}" : "api/almacen/lineas";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<LineaListItemDto>>(ct) ?? new();
    }

    public async Task<List<LineaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/lineas/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<LineaLookupDto>>(ct) ?? new();
    }

    public async Task<LineaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/lineas/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<LineaEditDto>(ct);
    }

    public async Task<LineaEditDto> CreateAsync(LineaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/lineas", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<LineaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<LineaEditDto> UpdateAsync(int id, LineaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/lineas/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<LineaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/lineas/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
