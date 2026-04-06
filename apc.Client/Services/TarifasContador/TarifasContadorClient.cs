using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasContador;

namespace apc.Client.Services.TarifasContador;

public sealed class TarifasContadorClient
{
    private readonly HttpClient _http;

    public TarifasContadorClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TarifaContadorListItemDto>> GetAsync(TarifaContadorFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new TarifaContadorFilterDto();
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
            ? $"api/tarifas-contador?{string.Join("&", parameters)}"
            : "api/tarifas-contador";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<TarifaContadorListItemDto>>(ct) ?? new List<TarifaContadorListItemDto>();
    }

    public async Task<PagedResult<TarifaContadorListItemDto>?> GetPagedAsync(
        TarifaContadorFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new TarifaContadorFilterDto();

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

        var url = $"api/tarifas-contador/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<TarifaContadorListItemDto>>(ct);
    }

    public async Task<TarifaContadorEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/tarifas-contador/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<TarifaContadorEditDto>(ct);
    }

    public async Task<TarifaContadorEditDto> CreateAsync(TarifaContadorEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/tarifas-contador", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<TarifaContadorEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La tarifa devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<TarifaContadorEditDto> UpdateAsync(int id, TarifaContadorEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID debe ser valido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/tarifas-contador/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<TarifaContadorEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La tarifa devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID debe ser valido.", nameof(id));
        }

        var response = await _http.DeleteAsync($"api/tarifas-contador/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
