using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>Cliente HTTP de órdenes de compra (api/almacen/ordenes-compra).</summary>
public sealed class OrdenesCompraClient
{
    private const string BaseUrl = "api/almacen/ordenes-compra";

    private readonly HttpClient _http;

    public OrdenesCompraClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<OrdenCompraListItemDto>> GetAsync(OrdenCompraFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new OrdenCompraFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(f.Search)}");
        }
        if (!string.IsNullOrWhiteSpace(f.CodProveedor))
        {
            parameters.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        }
        if (f.Estado.HasValue)
        {
            parameters.Add($"estado={f.Estado.Value}");
        }
        if (f.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={f.FechaDesde.Value:yyyy-MM-dd}");
        }
        if (f.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={f.FechaHasta.Value:yyyy-MM-dd}");
        }

        var url = parameters.Count > 0 ? $"{BaseUrl}?{string.Join("&", parameters)}" : BaseUrl;
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenCompraListItemDto>>(ct) ?? new List<OrdenCompraListItemDto>();
    }

    /// <summary>Artículos que se le pueden comprar al proveedor (los que tienen su código).</summary>
    public async Task<List<OrdenCompraArticuloLookupDto>> BuscarArticulosProveedorAsync(
        string codProveedor, string? search = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/articulos-proveedor?codProveedor={Uri.EscapeDataString(codProveedor)}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenCompraArticuloLookupDto>>(ct)
               ?? new List<OrdenCompraArticuloLookupDto>();
    }

    public async Task<OrdenCompraDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<OrdenCompraDto>(ct);
    }

    public async Task<OrdenCompraDto> CrearAsync(OrdenCompraDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo crear la orden de compra.");
        }
        return (await response.ReadFromJsonAsyncWithAuthCheck<OrdenCompraDto>(ct))!;
    }

    public async Task<OrdenCompraDto> ActualizarAsync(int id, OrdenCompraDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo guardar la orden de compra.");
        }
        return (await response.ReadFromJsonAsyncWithAuthCheck<OrdenCompraDto>(ct))!;
    }

    public async Task<bool> AprobarAsync(int id, CancellationToken ct = default)
        => await EjecutarAccionAsync($"{BaseUrl}/{id}/aprobar", ct);

    public async Task<bool> AnularAsync(int id, CancellationToken ct = default)
        => await EjecutarAccionAsync($"{BaseUrl}/{id}/anular", ct);

    /// <summary>POST sin cuerpo (transiciones de estado). Traduce el error del API a excepción con mensaje.</summary>
    private async Task<bool> EjecutarAccionAsync(string url, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck(url, new { }, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo completar la acción.");
        }
        return true;
    }
}
