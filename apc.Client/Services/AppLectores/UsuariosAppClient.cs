using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.AppLectores;

public sealed class UsuariosAppClient
{
    private readonly HttpClient _http;

    public UsuariosAppClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<UsuarioAppListItemDto>> GetAsync(UsuarioAppFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new UsuarioAppFilterDto();
        var parameters = BuildFilterParameters(filter);

        var url = parameters.Count > 0
            ? $"api/usuarios-app?{string.Join("&", parameters)}"
            : "api/usuarios-app";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<UsuarioAppListItemDto>>(ct) ?? new List<UsuarioAppListItemDto>();
    }

    public async Task<PagedResult<UsuarioAppListItemDto>?> GetPagedAsync(
        UsuarioAppFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new UsuarioAppFilterDto();
        var parameters = BuildFilterParameters(filter);
        parameters.Insert(0, $"take={Math.Max(1, take)}");
        parameters.Insert(0, $"skip={Math.Max(0, skip)}");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/usuarios-app/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<UsuarioAppListItemDto>>(ct);
    }

    public async Task<UsuarioAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/usuarios-app/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<UsuarioAppEditDto>(ct);
    }

    public async Task<UsuarioAppEditDto> CreateAsync(UsuarioAppEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/usuarios-app", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<UsuarioAppEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El usuario devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<UsuarioAppEditDto> UpdateAsync(int id, UsuarioAppEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/usuarios-app/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<UsuarioAppEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El usuario devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del usuario debe ser valido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/usuarios-app/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }

    private static List<string> BuildFilterParameters(UsuarioAppFilterDto filter)
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
