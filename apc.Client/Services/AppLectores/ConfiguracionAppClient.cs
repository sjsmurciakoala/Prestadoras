using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.AppLectores;

public sealed class ConfiguracionAppClient
{
    private readonly HttpClient _http;

    public ConfiguracionAppClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ConfiguracionAppListItemDto>> GetAsync(ConfiguracionAppFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new ConfiguracionAppFilterDto();
        var parameters = BuildFilterParameters(filter);

        var url = parameters.Count > 0
            ? $"api/configuraciones-app?{string.Join("&", parameters)}"
            : "api/configuraciones-app";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ConfiguracionAppListItemDto>>(ct)
               ?? new List<ConfiguracionAppListItemDto>();
    }

    public async Task<PagedResult<ConfiguracionAppListItemDto>?> GetPagedAsync(
        ConfiguracionAppFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new ConfiguracionAppFilterDto();
        var parameters = BuildFilterParameters(filter);
        parameters.Insert(0, $"take={Math.Max(1, take)}");
        parameters.Insert(0, $"skip={Math.Max(0, skip)}");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/configuraciones-app/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<ConfiguracionAppListItemDto>>(ct);
    }

    public async Task<ConfiguracionAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/configuraciones-app/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionAppEditDto>(ct);
    }

    public async Task<ConfiguracionAppEditDto> CreateAsync(ConfiguracionAppEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/configuraciones-app", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionAppEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La configuracion devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConfiguracionAppEditDto> UpdateAsync(int id, ConfiguracionAppEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/configuraciones-app/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionAppEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La configuracion devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID de configuracion debe ser valido.", nameof(id));
        }

        var response = await _http.DeleteAsync($"api/configuraciones-app/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }

    private static List<string> BuildFilterParameters(ConfiguracionAppFilterDto filter)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        return parameters;
    }
}
