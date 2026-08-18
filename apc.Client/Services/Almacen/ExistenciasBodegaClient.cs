using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class ExistenciasBodegaClient
{
    private readonly HttpClient _http;

    public ExistenciasBodegaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ExistenciaBodegaItemDto>> GetAsync(ExistenciasBodegaFilterDto filtro, CancellationToken ct = default)
    {
        var url = $"api/almacen/existencias-bodega{QueryString(filtro)}";
        return await _http.GetFromJsonAsyncWithAuthCheck<List<ExistenciaBodegaItemDto>>(url, ct) ?? new List<ExistenciaBodegaItemDto>();
    }

    /// <summary>URL del PDF imprimible del reporte (inline); se muestra embebido en la vista.</summary>
    public string GetPdfUrl(ExistenciasBodegaFilterDto filtro) => $"api/almacen/existencias-bodega/pdf{QueryString(filtro)}";

    private static string QueryString(ExistenciasBodegaFilterDto f)
    {
        var p = new List<string>();
        if (f.BodegaId.HasValue) p.Add($"bodegaId={f.BodegaId.Value}");
        if (f.TipoArticuloId.HasValue) p.Add($"tipoArticuloId={f.TipoArticuloId.Value}");
        if (f.IncluirSinExistencia) p.Add("incluirSinExistencia=true");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        return p.Count > 0 ? $"?{string.Join("&", p)}" : string.Empty;
    }
}
