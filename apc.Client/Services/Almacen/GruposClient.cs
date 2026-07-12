using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class GruposClient
{
    private readonly HttpClient _http;
    public GruposClient(HttpClient http) => _http = http;

    public async Task<List<GrupoListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/grupos?{string.Join("&", p)}" : "api/almacen/grupos";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<GrupoListItemDto>>(ct) ?? new();
    }

    public async Task<List<GrupoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/grupos/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<GrupoLookupDto>>(ct) ?? new();
    }

    public async Task<GrupoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/grupos/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<GrupoEditDto>(ct);
    }

    public async Task<GrupoEditDto> CreateAsync(GrupoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/grupos", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<GrupoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<GrupoEditDto> UpdateAsync(int id, GrupoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/grupos/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<GrupoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/grupos/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
