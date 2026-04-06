using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Ciclos;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.Ciclos;

public sealed class CiclosClient
{
    private readonly HttpClient _http;

    public CiclosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CicloListItemDto>> GetAsync(CicloFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new CicloFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }

        var url = parameters.Count > 0
            ? $"api/ciclos?{string.Join("&", parameters)}"
            : "api/ciclos";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CicloListItemDto>>(ct) ?? new List<CicloListItemDto>();
    }

    public async Task<PagedResult<CicloListItemDto>?> GetPagedAsync(
        CicloFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new CicloFilterDto();

        var parameters = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Max(1, take)}"
        };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/ciclos/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<CicloListItemDto>>(ct);
    }

    public async Task<CicloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/ciclos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<CicloEditDto>(ct);
    }

    public async Task<CicloEditDto> CreateAsync(CicloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/ciclos", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<CicloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<CicloEditDto> UpdateAsync(int id, CicloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del ciclo debe ser valido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/ciclos/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<CicloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del ciclo debe ser valido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/ciclos/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
