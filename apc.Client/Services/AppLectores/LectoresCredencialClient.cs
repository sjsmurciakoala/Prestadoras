using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.AppLectores;

/// <summary>Cliente HTTP del mantenimiento de credenciales de lectores V3.</summary>
public sealed class LectoresCredencialClient
{
    private const string Base = "api/lectores-credenciales";
    private readonly HttpClient _http;

    public LectoresCredencialClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LectorCredencialListItemDto>> GetAsync(LectorCredencialFilterDto? filtro = null, CancellationToken ct = default)
    {
        var parameters = BuildFilterParameters(filtro ?? new LectorCredencialFilterDto());
        var url = parameters.Count > 0 ? $"{Base}?{string.Join("&", parameters)}" : Base;
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<LectorCredencialListItemDto>>(ct) ?? new();
    }

    public async Task<PagedResult<LectorCredencialListItemDto>?> GetPagedAsync(
        LectorCredencialFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        var parameters = BuildFilterParameters(filtro ?? new LectorCredencialFilterDto());
        parameters.Insert(0, $"take={Math.Max(1, take)}");
        parameters.Insert(0, $"skip={Math.Max(0, skip)}");
        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }
        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var response = await _http.GetAsync($"{Base}/paged?{string.Join("&", parameters)}", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<LectorCredencialListItemDto>>(ct);
    }

    public async Task<LectorCredencialEditDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{Base}/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<LectorCredencialEditDto>(ct);
    }

    public async Task<LectorCredencialEditDto> CreateAsync(LectorCredencialEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(Base, dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<LectorCredencialEditDto>(ct)
               ?? throw new InvalidOperationException("El lector devolvió una respuesta vacía.");
    }

    public async Task<LectorCredencialEditDto> UpdateAsync(long id, LectorCredencialEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"{Base}/{id}", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<LectorCredencialEditDto>(ct)
               ?? throw new InvalidOperationException("El lector devolvió una respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del lector debe ser válido.", nameof(id));
        }
        var response = await _http.PostAsync($"{Base}/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }

    private static List<string> BuildFilterParameters(LectorCredencialFilterDto filter)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }
        if (!string.IsNullOrWhiteSpace(filter.Ruta))
        {
            parameters.Add($"ruta={Uri.EscapeDataString(filter.Ruta)}");
        }
        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }
        return parameters;
    }
}
