using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class RequisicionesClient
{
    private readonly HttpClient _http;

    public RequisicionesClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<RequisicionListItemDto>> GetAsync(RequisicionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new RequisicionFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Estatus))
        {
            parameters.Add($"estatus={Uri.EscapeDataString(filter.Estatus)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Departamento))
        {
            parameters.Add($"departamento={Uri.EscapeDataString(filter.Departamento)}");
        }

        if (filter.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={filter.FechaDesde.Value:yyyy-MM-dd}");
        }

        if (filter.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={filter.FechaHasta.Value:yyyy-MM-dd}");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/requisiciones?{string.Join("&", parameters)}"
            : "api/almacen/requisiciones";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<RequisicionListItemDto>>(ct) ?? new List<RequisicionListItemDto>();
    }

    public async Task<List<string>> GetDepartamentosAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/requisiciones/departamentos", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new List<string>();
    }
}
