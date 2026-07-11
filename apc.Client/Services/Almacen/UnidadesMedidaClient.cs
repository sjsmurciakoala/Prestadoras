using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class UnidadesMedidaClient
{
    private readonly HttpClient _http;

    public UnidadesMedidaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<UnidadMedidaListItemDto>> GetAsync(UnidadMedidaFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new UnidadMedidaFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Categoria))
        {
            parameters.Add($"categoria={Uri.EscapeDataString(filter.Categoria)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/unidades-medida?{string.Join("&", parameters)}"
            : "api/almacen/unidades-medida";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<UnidadMedidaListItemDto>>(ct) ?? new List<UnidadMedidaListItemDto>();
    }

    public async Task<List<UnidadMedidaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/unidades-medida/lookup", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<UnidadMedidaLookupDto>>(ct) ?? new List<UnidadMedidaLookupDto>();
    }

    public async Task<List<string>> GetCategoriasAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/unidades-medida/categorias", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new List<string>();
    }

    public async Task<UnidadMedidaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/almacen/unidades-medida/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<UnidadMedidaEditDto>(ct);
    }

    public async Task<UnidadMedidaEditDto> CreateAsync(UnidadMedidaEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/almacen/unidades-medida", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<UnidadMedidaEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<UnidadMedidaEditDto> UpdateAsync(int id, UnidadMedidaEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID de la unidad debe ser válido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/almacen/unidades-medida/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<UnidadMedidaEditDto>(ct);
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
            throw new ArgumentException("El ID de la unidad debe ser válido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/almacen/unidades-medida/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
