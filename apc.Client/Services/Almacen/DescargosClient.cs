using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class DescargosClient
{
    private readonly HttpClient _http;

    public DescargosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<DescargoListItemDto>> GetAsync(DescargoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new DescargoFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Departamento))
        {
            parameters.Add($"departamento={Uri.EscapeDataString(filter.Departamento)}");
        }

        if (filter.NumeroRequisicion.HasValue)
        {
            parameters.Add($"numeroRequisicion={filter.NumeroRequisicion.Value}");
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
            ? $"api/almacen/descargos?{string.Join("&", parameters)}"
            : "api/almacen/descargos";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<DescargoListItemDto>>(ct) ?? new List<DescargoListItemDto>();
    }

    public async Task<List<string>> GetDepartamentosAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/descargos/departamentos", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new List<string>();
    }
}
