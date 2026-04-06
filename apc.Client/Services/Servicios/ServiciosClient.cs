using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Servicios;

namespace apc.Client.Services.Servicios;

public sealed class ServiciosClient
{
    private readonly HttpClient _http;

    public ServiciosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ServicioListItemDto>> GetAsync(ServicioFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new ServicioFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        }

        if (filter.FacturableApp.HasValue)
        {
            parameters.Add($"facturableApp={(filter.FacturableApp.Value ? "true" : "false")}");
        }

        var url = parameters.Count > 0
            ? $"api/servicios?{string.Join("&", parameters)}"
            : "api/servicios";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ServicioListItemDto>>(ct) ?? new List<ServicioListItemDto>();
    }

    public async Task<PagedResult<ServicioListItemDto>?> GetPagedAsync(
        ServicioFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new ServicioFilterDto();

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

        if (filter.FacturableApp.HasValue)
        {
            parameters.Add($"facturableApp={(filter.FacturableApp.Value ? "true" : "false")}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/servicios/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<ServicioListItemDto>>(ct);
    }

    public async Task<ServicioEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/servicios/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ServicioEditDto>(ct);
    }

    public async Task<ServicioEditDto> CreateAsync(ServicioEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/servicios", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ServicioEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ServicioEditDto> UpdateAsync(int id, ServicioEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del servicio debe ser valido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/servicios/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ServicioEditDto>(ct);
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
            throw new ArgumentException("El ID del servicio debe ser valido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/servicios/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await _http.GetFromJsonAsync<PlanCuentaDto[]>(
            "api/contabilidad/catalogos/plan-cuentas", ct) ?? Array.Empty<PlanCuentaDto>();

        return cuentas
            .Where(c => c.AllowsPosting
                && (string.IsNullOrWhiteSpace(c.Status)
                    || string.Equals(c.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Status, "ACTIVO", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.AccountId,
                Code = c.Code ?? string.Empty,
                Description = c.Name ?? string.Empty
            })
            .ToList();
    }
}
