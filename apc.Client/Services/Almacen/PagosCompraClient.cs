using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>Cliente HTTP de pagos a proveedores (api/almacen/pagos-compra).</summary>
public sealed class PagosCompraClient
{
    private const string BaseUrl = "api/almacen/pagos-compra";

    private readonly HttpClient _http;

    public PagosCompraClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CompraCxpListItemDto>> ListarAsync(CompraCxpFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new CompraCxpFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) p.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        if (f.EstadoId.HasValue) p.Add($"estadoId={f.EstadoId.Value}");
        if (f.SoloVencidas) p.Add("soloVencidas=true");
        if (f.VenceDesde.HasValue) p.Add($"venceDesde={f.VenceDesde.Value:yyyy-MM-dd}");
        if (f.VenceHasta.HasValue) p.Add($"venceHasta={f.VenceHasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CompraCxpListItemDto>>(ct) ?? new();
    }

    public async Task<List<CompraCxpAbonoListItemDto>> ListarAbonosAsync(int cxpId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/{cxpId}/abonos", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CompraCxpAbonoListItemDto>>(ct) ?? new();
    }

    public async Task<List<CuentaBancariaLookupDto>> ListarCuentasBancariasAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/cuentas-bancarias", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CuentaBancariaLookupDto>>(ct) ?? new();
    }

    public async Task<List<CompraCuentaContableLookupDto>> ListarCuentasContablesAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/cuentas-contables", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CompraCuentaContableLookupDto>>(ct) ?? new();
    }

    public async Task<bool> ObtenerContabilidadActivaAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/contabilidad-activa", ct);
        var estado = await r.ReadFromJsonAsyncWithAuthCheck<CompraContabilidadEstadoDto>(ct);
        return estado?.ContabilidadActiva ?? false;
    }

    /// <summary>Devuelve la partida contable de un pago, o null si el pago no generó asiento.</summary>
    public async Task<CompraCxpPartidaDto?> ObtenerPartidaAbonoAsync(int cxpId, int numeroAbono, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/{cxpId}/abonos/{numeroAbono}/partida", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<CompraCxpPartidaDto>(ct);
    }

    /// <summary>URL del comprobante de pago (PDF inline). Se abre con window.open, como los demás comprobantes.</summary>
    public static string GetComprobantePagoPdfUrl(int cxpId, int numeroAbono)
        => $"/{BaseUrl}/{cxpId}/abonos/{numeroAbono}/comprobante/pdf";

    /// <summary>URL del comprobante de la partida contable del pago (PDF inline).</summary>
    public static string GetPartidaPagoPdfUrl(int cxpId, int numeroAbono)
        => $"/{BaseUrl}/{cxpId}/abonos/{numeroAbono}/partida/pdf";

    public async Task<CompraCxpAbonoResultadoDto> RegistrarAbonoAsync(int cxpId, CompraCxpAbonoUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{cxpId}/abonos", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar el pago.");
        }
        return (await r.ReadFromJsonAsyncWithAuthCheck<CompraCxpAbonoResultadoDto>(ct))!;
    }

    public async Task<bool> AnularAbonoAsync(int cxpId, int numeroAbono, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{cxpId}/abonos/{numeroAbono}/anular", new { Motivo = motivo }, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo anular el pago.");
        }
        return true;
    }
}
