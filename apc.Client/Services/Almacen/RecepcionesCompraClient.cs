using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>Cliente HTTP de recepciones de compra (api/almacen/recepciones-compra).</summary>
public sealed class RecepcionesCompraClient
{
    private const string BaseUrl = "api/almacen/recepciones-compra";

    private readonly HttpClient _http;

    public RecepcionesCompraClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>URL del comprobante de la factura de compra en PDF (inline); se muestra embebido en la vista.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

    public async Task<List<RecepcionCompraListItemDto>> GetAsync(
        RecepcionCompraFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new RecepcionCompraFilterDto();
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
        if (f.OrdenCompraId.HasValue)
        {
            parameters.Add($"ordenCompraId={f.OrdenCompraId.Value}");
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
        return await response.ReadFromJsonAsyncWithAuthCheck<List<RecepcionCompraListItemDto>>(ct)
               ?? new List<RecepcionCompraListItemDto>();
    }

    public async Task<RecepcionCompraDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<RecepcionCompraDto>(ct);
    }

    /// <summary>Órdenes que todavía admiten recepción (una O/C puede recibirse en varias facturas).</summary>
    public async Task<List<OrdenCompraListItemDto>> ObtenerOrdenesRecibiblesAsync(
        string? codProveedor = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/ordenes-recibibles";
        if (!string.IsNullOrWhiteSpace(codProveedor))
        {
            url += $"?codProveedor={Uri.EscapeDataString(codProveedor)}";
        }

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenCompraListItemDto>>(ct)
               ?? new List<OrdenCompraListItemDto>();
    }

    /// <summary>Renglones de la O/C con lo que falta por recibir.</summary>
    public async Task<List<OrdenCompraPendienteDto>> ObtenerPendientesAsync(
        int ordenCompraId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/ordenes/{ordenCompraId}/pendientes", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenCompraPendienteDto>>(ct)
               ?? new List<OrdenCompraPendienteDto>();
    }

    /// <summary>
    /// Anula la factura: revierte sus asientos del kardex y devuelve lo recibido a la O/C.
    /// Devuelve false si la recepción ya no existe.
    /// </summary>
    public async Task<bool> AnularAsync(int id, string? motivo = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}/anular", new { Motivo = motivo }, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo anular la factura.");
        }
        return true;
    }

    public async Task<RecepcionCompraDto> CrearAsync(RecepcionCompraDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo registrar la factura.");
        }
        return (await response.ReadFromJsonAsyncWithAuthCheck<RecepcionCompraDto>(ct))!;
    }
}
