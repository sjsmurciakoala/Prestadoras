using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasBase;

namespace apc.Client.Services.TarifasBase;

public sealed class TarifasBaseClient
{
    private readonly HttpClient _http;

    public TarifasBaseClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TarifaBaseListItemDto>> GetAsync(TarifaBaseFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new TarifaBaseFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Tipo.HasValue)
        {
            parameters.Add($"tipo={filter.Tipo.Value}");
        }

        if (filter.CategoriaId.HasValue)
        {
            parameters.Add($"categoriaId={filter.CategoriaId.Value}");
        }

        var url = parameters.Count > 0
            ? $"api/tarifas-base?{string.Join("&", parameters)}"
            : "api/tarifas-base";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<TarifaBaseListItemDto>>(ct) ?? new List<TarifaBaseListItemDto>();
    }

    public async Task<PagedResult<TarifaBaseListItemDto>?> GetPagedAsync(
        TarifaBaseFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new TarifaBaseFilterDto();

        var parameters = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Max(1, take)}"
        };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Tipo.HasValue)
        {
            parameters.Add($"tipo={filter.Tipo.Value}");
        }

        if (filter.CategoriaId.HasValue)
        {
            parameters.Add($"categoriaId={filter.CategoriaId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/tarifas-base/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<TarifaBaseListItemDto>>(ct);
    }

    public async Task<TarifaBaseEditDto?> GetByIdAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default)
    {
        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var code = Uri.EscapeDataString(codigo);
        var response = await _http.GetAsync($"api/tarifas-base/{tipo}/{categoriaId}/{code}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<TarifaBaseEditDto>(ct);
    }

    public async Task<TarifaBaseEditDto> CreateAsync(TarifaBaseEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/tarifas-base", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<TarifaBaseEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La tarifa devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<TarifaBaseEditDto> UpdateAsync(int tipo, int categoriaId, string codigo, TarifaBaseEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("La clave de la tarifa debe ser valida.");
        }

        var code = Uri.EscapeDataString(codigo);
        var response = await _http.PutAsJsonAsync($"api/tarifas-base/{tipo}/{categoriaId}/{code}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<TarifaBaseEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La tarifa devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default)
    {
        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("La clave de la tarifa debe ser valida.");
        }

        var code = Uri.EscapeDataString(codigo);
        var response = await _http.DeleteAsync($"api/tarifas-base/{tipo}/{categoriaId}/{code}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
