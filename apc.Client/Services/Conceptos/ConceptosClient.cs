using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Conceptos;

namespace apc.Client.Services.Conceptos;

public sealed class ConceptosClient
{
    private readonly HttpClient _http;

    public ConceptosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ConceptoListItemDto>> GetAsync(ConceptoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new ConceptoFilterDto();

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
            ? $"api/conceptos?{string.Join("&", parameters)}"
            : "api/conceptos";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ConceptoListItemDto>>(ct) ?? new List<ConceptoListItemDto>();
    }

    public async Task<PagedResult<ConceptoListItemDto>?> GetPagedAsync(
        ConceptoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new ConceptoFilterDto();

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

        var url = $"api/conceptos/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<ConceptoListItemDto>>(ct);
    }

    public async Task<ConceptoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/conceptos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ConceptoEditDto>(ct);
    }

    public async Task<ConceptoEditDto> CreateAsync(ConceptoEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/conceptos", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConceptoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El concepto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConceptoEditDto> UpdateAsync(int id, ConceptoEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/conceptos/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConceptoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El concepto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del concepto debe ser valido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/conceptos/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }
}
