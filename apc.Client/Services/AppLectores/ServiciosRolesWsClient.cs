using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.AppLectores;

public sealed class ServiciosRolesWsClient
{
    private readonly HttpClient _http;

    public ServiciosRolesWsClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ServicioRolWsListItemDto>> GetAsync(ServicioRolWsFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new ServicioRolWsFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Rol))
        {
            parameters.Add($"rol={Uri.EscapeDataString(filter.Rol)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }

        var url = parameters.Count > 0
            ? $"api/servicios-roles-ws?{string.Join("&", parameters)}"
            : "api/servicios-roles-ws";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ServicioRolWsListItemDto>>(ct) ?? new List<ServicioRolWsListItemDto>();
    }

    public async Task<PagedResult<ServicioRolWsListItemDto>?> GetPagedAsync(
        ServicioRolWsFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new ServicioRolWsFilterDto();
        var parameters = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Max(1, take)}"
        };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Rol))
        {
            parameters.Add($"rol={Uri.EscapeDataString(filter.Rol)}");
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

        var url = $"api/servicios-roles-ws/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<ServicioRolWsListItemDto>>(ct);
    }

    public async Task<ServicioRolWsEditDto?> GetByIdAsync(string rol, string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rol) || string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var response = await _http.GetAsync($"api/servicios-roles-ws/{Uri.EscapeDataString(rol)}/{Uri.EscapeDataString(codigo)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ServicioRolWsEditDto>(ct);
    }

    public async Task<ServicioRolWsEditDto> CreateAsync(ServicioRolWsEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/servicios-roles-ws", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ServicioRolWsEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ServicioRolWsEditDto> UpdateAsync(string rol, string codigo, ServicioRolWsEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(rol) || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("El rol y codigo son obligatorios.");
        }

        var response = await _http.PutAsJsonAsync(
            $"api/servicios-roles-ws/{Uri.EscapeDataString(rol)}/{Uri.EscapeDataString(codigo)}",
            dto,
            ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ServicioRolWsEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string rol, string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rol) || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("El rol y codigo son obligatorios.");
        }

        var response = await _http.DeleteAsync($"api/servicios-roles-ws/{Uri.EscapeDataString(rol)}/{Uri.EscapeDataString(codigo)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }
}
