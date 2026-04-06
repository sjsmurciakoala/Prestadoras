using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Abogados;

namespace apc.Client.Services.Abogados;

public sealed class AbogadosClient
{
    private readonly HttpClient _http;

    public AbogadosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<AbogadoListItemDto>> GetAsync(AbogadoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new AbogadoFilterDto();
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
            ? $"api/abogados?{string.Join("&", parameters)}"
            : "api/abogados";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<AbogadoListItemDto>>(ct) ?? new List<AbogadoListItemDto>();
    }

    public async Task<AbogadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/abogados/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<AbogadoEditDto>(ct);
    }

    public async Task<AbogadoEditDto> CreateAsync(AbogadoEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/abogados", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<AbogadoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<AbogadoEditDto> UpdateAsync(int id, AbogadoEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del abogado debe ser válido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/abogados/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<AbogadoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del abogado debe ser válido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/abogados/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
