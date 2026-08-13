using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente HTTP del documento de movimiento de almacén (<c>api/almacen/movimientos</c>):
/// entradas y salidas manuales. Equivalente del <c>dlgTransaccionesGenericasINV</c> de Centura.
/// </summary>
public sealed class MovimientosAlmacenClient
{
    private const string BaseUrl = "api/almacen/movimientos";
    private readonly HttpClient _http;

    public MovimientosAlmacenClient(HttpClient http) => _http = http;

    /// <summary>URL del comprobante del movimiento en PDF, para abrirlo en una pestaña nueva.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

    /// <summary>URL de la partida contable del movimiento en PDF (existe solo si el movimiento generó asiento).</summary>
    public static string GetPartidaPdfUrl(int id) => $"/{BaseUrl}/{id}/partida/pdf";

    public async Task<List<MovimientoAlmacenListItemDto>> GetAsync(
        MovimientoAlmacenFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new MovimientoAlmacenFilterDto();
        var p = new List<string>();
        if (f.TipoMovimientoId is > 0) p.Add($"tipoMovimientoId={f.TipoMovimientoId}");
        if (f.BodegaId is > 0) p.Add($"bodegaId={f.BodegaId}");
        if (f.Estado.HasValue) p.Add($"estado={f.Estado.Value}");
        if (f.Desde.HasValue) p.Add($"desde={f.Desde.Value:yyyy-MM-dd}");
        if (f.Hasta.HasValue) p.Add($"hasta={f.Hasta.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");

        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<MovimientoAlmacenListItemDto>>(ct) ?? new();
    }

    public async Task<MovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<MovimientoAlmacenDto>(ct);
    }

    /// <summary>Crea y postea el documento. Lanza con el mensaje del servidor si algo se rechaza.</summary>
    public async Task<MovimientoAlmacenDto> CrearAsync(MovimientoAlmacenDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar el movimiento.");
        }
        return (await r.ReadFromJsonAsyncWithAuthCheck<MovimientoAlmacenDto>(ct))!;
    }

    /// <summary>Anula el documento (reversa por renglón). Devuelve false si ya no existe.</summary>
    public async Task<bool> AnularAsync(int id, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}/anular", new { Motivo = motivo }, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo anular el movimiento.");
        }
        return true;
    }
}
