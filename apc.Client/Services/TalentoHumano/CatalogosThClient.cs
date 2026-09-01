using System.Net;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.TalentoHumano;

namespace apc.Client.Services.TalentoHumano;

/// <summary>
/// Cliente de los catálogos simples de Talento Humano. El segmento de recurso
/// (<c>"cargos"</c> / <c>"departamentos"</c>) lo fija la página, no el usuario.
/// </summary>
public sealed class CatalogosThClient
{
    public const string Cargos = "cargos";
    public const string Departamentos = "departamentos";

    private readonly HttpClient _http;
    public CatalogosThClient(HttpClient http) => _http = http;

    private static string Base(string recurso) => $"api/talentohumano/{recurso}";

    public async Task<List<CatalogoThListItemDto>> GetAsync(string recurso, CatalogoThFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new CatalogoThFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"{Base(recurso)}?{string.Join("&", p)}" : Base(recurso);
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CatalogoThListItemDto>>(ct) ?? new();
    }

    public async Task<List<CatalogoThLookupDto>> GetLookupAsync(string recurso, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{Base(recurso)}/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CatalogoThLookupDto>>(ct) ?? new();
    }

    public async Task<CatalogoThEditDto?> GetByIdAsync(string recurso, int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{Base(recurso)}/{id}", ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<CatalogoThEditDto>(ct);
    }

    public async Task<CatalogoThEditDto> CreateAsync(string recurso, CatalogoThEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(Base(recurso), dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<CatalogoThEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<CatalogoThEditDto> UpdateAsync(string recurso, int id, CatalogoThEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{Base(recurso)}/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<CatalogoThEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(string recurso, int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{Base(recurso)}/{id}/desactivar", null, ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
