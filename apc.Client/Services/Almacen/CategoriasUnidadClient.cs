using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class CategoriasUnidadClient
{
    private readonly HttpClient _http;
    public CategoriasUnidadClient(HttpClient http) => _http = http;

    public async Task<List<CategoriaUnidadListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/categorias-unidad?{string.Join("&", p)}" : "api/almacen/categorias-unidad";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CategoriaUnidadListItemDto>>(ct) ?? new();
    }

    public async Task<List<CategoriaUnidadLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/categorias-unidad/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CategoriaUnidadLookupDto>>(ct) ?? new();
    }

    public async Task<CategoriaUnidadEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/categorias-unidad/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<CategoriaUnidadEditDto>(ct);
    }

    public async Task<CategoriaUnidadEditDto> CreateAsync(CategoriaUnidadEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/categorias-unidad", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar la categoría.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<CategoriaUnidadEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<CategoriaUnidadEditDto> UpdateAsync(int id, CategoriaUnidadEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/categorias-unidad/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar la categoría.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<CategoriaUnidadEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/categorias-unidad/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
