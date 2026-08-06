using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;

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

    // ── Libro de movimientos de bodega ───────────────────────────────────────

    /// <summary>Página del libro de movimientos de una bodega (server-side paging/sort sobre el filtro).</summary>
    public async Task<PagedResult<MovimientoBodegaItemDto>> GetMovimientosBodegaPagedAsync(
        MovimientosBodegaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (!filtro.BodegaId.HasValue)
        {
            return new PagedResult<MovimientoBodegaItemDto>();
        }

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        var parameters = BuildBodegaParams(filtro);
        parameters.Add($"skip={skip}");
        parameters.Add($"take={take}");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc)
            {
                parameters.Add("sortDesc=true");
            }
        }

        var url = $"api/almacen/kardex/bodega?{string.Join("&", parameters)}";
        return await _http.GetFromJsonAsyncWithAuthCheck<PagedResult<MovimientoBodegaItemDto>>(url, ct)
               ?? new PagedResult<MovimientoBodegaItemDto>();
    }

    /// <summary>Totales (movimientos, ingresos, salidas, valor) del libro de bodega para el filtro.</summary>
    public async Task<MovimientosBodegaResumenDto> GetResumenBodegaAsync(MovimientosBodegaFilterDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (!filtro.BodegaId.HasValue)
        {
            return new MovimientosBodegaResumenDto();
        }

        var url = $"api/almacen/kardex/bodega/resumen?{string.Join("&", BuildBodegaParams(filtro))}";
        return await _http.GetFromJsonAsyncWithAuthCheck<MovimientosBodegaResumenDto>(url, ct)
               ?? new MovimientosBodegaResumenDto();
    }

    /// <summary>URL del PDF del kardex de un artículo (se abre en pestaña nueva).</summary>
    public string GetPdfArticuloUrl(KardexFilterDto filtro)
    {
        var p = new List<string>();
        if (filtro.ArticuloId.HasValue) p.Add($"articuloId={filtro.ArticuloId.Value}");
        else if (!string.IsNullOrWhiteSpace(filtro.CodigoArticulo)) p.Add($"codigoArticulo={Uri.EscapeDataString(filtro.CodigoArticulo)}");
        if (filtro.FechaDesde.HasValue) p.Add($"fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}");
        if (filtro.FechaHasta.HasValue) p.Add($"fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(filtro.TipoTransaccion)) p.Add($"tipoTransaccion={Uri.EscapeDataString(filtro.TipoTransaccion)}");
        if (filtro.BodegaId.HasValue) p.Add($"bodegaId={filtro.BodegaId.Value}");
        return $"api/almacen/kardex/pdf?{string.Join("&", p)}";
    }

    /// <summary>URL del PDF del libro de movimientos de una bodega (se abre en pestaña nueva).</summary>
    public string GetPdfBodegaUrl(MovimientosBodegaFilterDto filtro)
        => $"api/almacen/kardex/bodega/pdf?{string.Join("&", BuildBodegaParams(filtro))}";

    private static List<string> BuildBodegaParams(MovimientosBodegaFilterDto f)
    {
        var p = new List<string>();
        if (f.BodegaId.HasValue) p.Add($"bodegaId={f.BodegaId.Value}");
        if (f.ArticuloId.HasValue) p.Add($"articuloId={f.ArticuloId.Value}");
        if (f.FechaDesde.HasValue) p.Add($"fechaDesde={f.FechaDesde.Value:yyyy-MM-dd}");
        if (f.FechaHasta.HasValue) p.Add($"fechaHasta={f.FechaHasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.TipoTransaccion)) p.Add($"tipoTransaccion={Uri.EscapeDataString(f.TipoTransaccion)}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        return p;
    }
}
