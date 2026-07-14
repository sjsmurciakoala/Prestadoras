using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class KardexClient
{
    private readonly HttpClient _http;

    public KardexClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<KardexArticuloDto?> GetByArticuloAsync(KardexFilterDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (!filtro.ArticuloId.HasValue && string.IsNullOrWhiteSpace(filtro.CodigoArticulo))
        {
            return null;
        }

        var parameters = new List<string>();
        if (filtro.ArticuloId.HasValue)
        {
            parameters.Add($"articuloId={filtro.ArticuloId.Value}");
        }
        else
        {
            parameters.Add($"codigoArticulo={Uri.EscapeDataString(filtro.CodigoArticulo)}");
        }

        if (filtro.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}");
        }

        if (filtro.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoTransaccion))
        {
            parameters.Add($"tipoTransaccion={Uri.EscapeDataString(filtro.TipoTransaccion)}");
        }

        if (filtro.BodegaId.HasValue)
        {
            parameters.Add($"bodegaId={filtro.BodegaId.Value}");
        }

        var url = $"api/almacen/kardex?{string.Join("&", parameters)}";

        var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<KardexArticuloDto>(ct);
    }

    public async Task<List<TipoMovimientoDto>> GetTiposMovimientoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/kardex/tipos", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<TipoMovimientoDto>>(ct) ?? new List<TipoMovimientoDto>();
    }
}
