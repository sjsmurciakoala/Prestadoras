using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class EstantesClient
{
    private readonly HttpClient _http;
    public EstantesClient(HttpClient http) => _http = http;

    public async Task<List<EstanteListItemDto>> GetAsync(UbicacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new UbicacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        if (f.EstanteriaId.HasValue) p.Add($"estanteriaId={f.EstanteriaId.Value}");
        if (f.BodegaId.HasValue) p.Add($"bodegaId={f.BodegaId.Value}");
        var url = p.Count > 0 ? $"api/almacen/estantes?{string.Join("&", p)}" : "api/almacen/estantes";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EstanteListItemDto>>(ct) ?? new();
    }

    public async Task<List<EstanteLookupDto>> GetLookupAsync(int estanteriaId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"api/almacen/estantes/lookup?estanteriaId={estanteriaId}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EstanteLookupDto>>(ct) ?? new();
    }

    public async Task<EstanteEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/estantes/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteEditDto>(ct);
    }

    public async Task<EstanteEditDto> CreateAsync(EstanteEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/estantes", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<EstanteEditDto> UpdateAsync(int id, EstanteEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/estantes/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<EstanteEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/estantes/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
