using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class BodegasClient
{
    private readonly HttpClient _http;
    public BodegasClient(HttpClient http) => _http = http;

    public async Task<List<BodegaListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/bodegas?{string.Join("&", p)}" : "api/almacen/bodegas";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<BodegaListItemDto>>(ct) ?? new();
    }

    public async Task<List<BodegaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/bodegas/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<BodegaLookupDto>>(ct) ?? new();
    }

    public async Task<BodegaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/bodegas/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<BodegaEditDto>(ct);
    }

    public async Task<BodegaEditDto> CreateAsync(BodegaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/bodegas", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<BodegaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<BodegaEditDto> UpdateAsync(int id, BodegaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/bodegas/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<BodegaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/bodegas/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
