using System;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Rutas;

namespace apc.Client.Services.Rutas;

public class RutasClient
{
    private readonly HttpClient _http;

    public RutasClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<RutaListItemDto>> GetAsync(RutaFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new RutaFilterDto();

        var parameters = new List<string>();

        if (filter.CodCiclo.HasValue)
        {
            parameters.Add($"codCiclo={filter.CodCiclo.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.CodRuta))
        {
            parameters.Add($"codRuta={Uri.EscapeDataString(filter.CodRuta)}");
        }

        var url = parameters.Count > 0
            ? $"api/rutas?{string.Join("&", parameters)}"
            : "api/rutas";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<RutaListItemDto>>(ct) ?? new List<RutaListItemDto>();
    }

    public async Task<PagedResult<RutaListItemDto>?> GetPagedAsync(
        RutaFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new RutaFilterDto();

        var parameters = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Max(1, take)}"
        };

        if (filter.CodCiclo.HasValue)
        {
            parameters.Add($"codCiclo={filter.CodCiclo.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.CodRuta))
        {
            parameters.Add($"codRuta={Uri.EscapeDataString(filter.CodRuta)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
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

        var url = $"api/rutas/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<RutaListItemDto>>(ct);
    }

    public async Task<RutaDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/rutas/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<RutaDetailDto>(ct);
    }

    public async Task<RutaDetailDto> CreateAsync(RutaUpsertDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/rutas", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<RutaDetailDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La ruta devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<RutaDetailDto> UpdateAsync(int id, RutaUpsertDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/rutas/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<RutaDetailDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La ruta devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<List<CicloLookupDto>> GetCiclosAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/rutas/ciclos", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CicloLookupDto>>(ct) ?? new List<CicloLookupDto>();
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID de la ruta debe ser valido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/rutas/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }
}
