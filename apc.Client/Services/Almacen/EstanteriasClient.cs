using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class EstanteriasClient
{
    private readonly HttpClient _http;
    public EstanteriasClient(HttpClient http) => _http = http;

    public async Task<List<EstanteriaListItemDto>> GetAsync(UbicacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new UbicacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        if (f.BodegaId.HasValue) p.Add($"bodegaId={f.BodegaId.Value}");
        var url = p.Count > 0 ? $"api/almacen/estanterias?{string.Join("&", p)}" : "api/almacen/estanterias";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EstanteriaListItemDto>>(ct) ?? new();
    }

    public async Task<List<EstanteriaLookupDto>> GetLookupAsync(int bodegaId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"api/almacen/estanterias/lookup?bodegaId={bodegaId}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EstanteriaLookupDto>>(ct) ?? new();
    }

    public async Task<EstanteriaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/estanterias/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteriaEditDto>(ct);
    }

    public async Task<EstanteriaEditDto> CreateAsync(EstanteriaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/estanterias", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteriaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<EstanteriaEditDto> UpdateAsync(int id, EstanteriaEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/estanterias/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteriaEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/estanterias/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
