using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class ValuacionInventarioClient
{
    private readonly HttpClient _http;

    public ValuacionInventarioClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ExistenciaBodegaItemDto>> GetAsync(ValuacionInventarioFilterDto filtro, CancellationToken ct = default)
    {
        var url = $"api/almacen/valuacion-inventario{QueryString(filtro)}";
        return await _http.GetFromJsonAsyncWithAuthCheck<List<ExistenciaBodegaItemDto>>(url, ct) ?? new List<ExistenciaBodegaItemDto>();
    }

    /// <summary>URL del PDF de la valuación (inline); se muestra embebido en la vista.</summary>
    public string GetPdfUrl(ValuacionInventarioFilterDto filtro) => $"api/almacen/valuacion-inventario/pdf{QueryString(filtro)}";

    private static string QueryString(ValuacionInventarioFilterDto f)
    {
        var p = new List<string>();
        if (f.FechaCorte.HasValue) p.Add($"fechaCorte={f.FechaCorte.Value:yyyy-MM-dd}");
        if (f.BodegaId.HasValue) p.Add($"bodegaId={f.BodegaId.Value}");
        if (f.TipoArticuloId.HasValue) p.Add($"tipoArticuloId={f.TipoArticuloId.Value}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        return p.Count > 0 ? $"?{string.Join("&", p)}" : string.Empty;
    }
}
