using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class ComprasClient
{
    private readonly HttpClient _http;

    public ComprasClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CompraListItemDto>> GetAsync(CompraFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new CompraFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Proveedor))
        {
            parameters.Add($"proveedor={Uri.EscapeDataString(filter.Proveedor)}");
        }

        if (filter.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={filter.FechaDesde.Value:yyyy-MM-dd}");
        }

        if (filter.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={filter.FechaHasta.Value:yyyy-MM-dd}");
        }

        if (filter.TipoCompra.HasValue)
        {
            parameters.Add($"tipoCompra={filter.TipoCompra.Value}");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/compras?{string.Join("&", parameters)}"
            : "api/almacen/compras";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CompraListItemDto>>(ct) ?? new List<CompraListItemDto>();
    }

    public async Task<List<string>> GetProveedoresAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/compras/proveedores", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new List<string>();
    }
}
