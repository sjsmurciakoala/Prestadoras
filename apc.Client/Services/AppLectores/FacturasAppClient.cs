using apc.Client.Services;
using SIAD.Core.DTOs.AppLectores;

namespace apc.Client.Services.AppLectores;

/// <summary>Cliente HTTP de la consulta de facturas subidas desde la app de lectores V3.</summary>
public sealed class FacturasAppClient
{
    private const string Base = "api/facturas-app";
    private readonly HttpClient _http;

    public FacturasAppClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FacturaAppListItemDto>> GetAsync(FacturaAppFilterDto? filtro = null, CancellationToken ct = default)
    {
        var parameters = BuildFilterParameters(filtro ?? new FacturaAppFilterDto());
        var url = parameters.Count > 0 ? $"{Base}?{string.Join("&", parameters)}" : Base;
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<FacturaAppListItemDto>>(ct) ?? new();
    }

    private static List<string> BuildFilterParameters(FacturaAppFilterDto filtro)
    {
        var parameters = new List<string>();
        if (filtro.Anio.HasValue)
        {
            parameters.Add($"anio={filtro.Anio.Value}");
        }
        if (filtro.Mes.HasValue)
        {
            parameters.Add($"mes={filtro.Mes.Value}");
        }
        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filtro.Search)}");
        }
        if (!string.IsNullOrWhiteSpace(filtro.Lector))
        {
            parameters.Add($"lector={Uri.EscapeDataString(filtro.Lector)}");
        }
        if (!string.IsNullOrWhiteSpace(filtro.Condicion))
        {
            parameters.Add($"condicion={Uri.EscapeDataString(filtro.Condicion)}");
        }
        if (!string.IsNullOrWhiteSpace(filtro.EstadoSync))
        {
            parameters.Add($"estadoSync={Uri.EscapeDataString(filtro.EstadoSync)}");
        }
        if (filtro.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}");
        }
        if (filtro.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}");
        }
        return parameters;
    }
}
