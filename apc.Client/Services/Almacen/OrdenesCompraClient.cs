using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Core.DTOs.Presupuesto;

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

    /// <summary>URL del comprobante de la orden de compra en PDF (inline); se muestra embebido en la vista.</summary>
    public static string GetComprobantePdfUrl(int id) => $"/{BaseUrl}/{id}/comprobante/pdf";

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

    /// <summary>Rechaza una orden en Borrador. El motivo es obligatorio.</summary>
    public Task<bool> RechazarAsync(int id, string motivo, CancellationToken ct = default)
        => EjecutarAccionConMotivoAsync($"{BaseUrl}/{id}/rechazar", motivo, ct);

    /// <summary>
    /// Cancela una orden aprobada o recibida en parte. Libera el presupuesto comprometido que
    /// quedaba pendiente. El motivo es obligatorio.
    /// </summary>
    public Task<bool> CancelarAsync(int id, string motivo, CancellationToken ct = default)
        => EjecutarAccionConMotivoAsync($"{BaseUrl}/{id}/cancelar", motivo, ct);

    /// <summary>Cierra anticipadamente una orden recibida en parte. El motivo es obligatorio.</summary>
    public Task<bool> CerrarAsync(int id, string motivo, CancellationToken ct = default)
        => EjecutarAccionConMotivoAsync($"{BaseUrl}/{id}/cerrar", motivo, ct);

    // ── Aprobación por niveles ───────────────────────────────────────────────
    // Solo aplican con el control encendido (cfg_aprobacion_control). Con la escalera apagada la
    // pantalla usa AprobarAsync y estos endpoints devuelven un error explicativo.

    /// <summary>Envía una orden en Borrador a la escalera de firmas.</summary>
    public Task<bool> EnviarAAprobacionAsync(int id, CancellationToken ct = default)
        => EjecutarAccionAsync($"{BaseUrl}/{id}/enviar-aprobacion", ct);

    /// <summary>
    /// Firma el nivel pendiente. Devuelve qué pasó (nivel firmado, si completó la escalera, si
    /// reservó presupuesto) para que la pantalla lo diga con precisión.
    /// </summary>
    public async Task<OrdenCompraAprobacionResultadoDto?> FirmarAsync(
        int id, string? comentario = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck(
            $"{BaseUrl}/{id}/firmar", new { Comentario = comentario }, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo firmar la orden.");
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<OrdenCompraAprobacionResultadoDto>(ct);
    }

    /// <summary>Devuelve la orden a Borrador: borra las firmas y libera lo reservado.</summary>
    public Task<bool> DevolverAsync(int id, string motivo, CancellationToken ct = default)
        => EjecutarAccionConMotivoAsync($"{BaseUrl}/{id}/devolver", motivo, ct);

    /// <summary>Estado de la escalera de una orden: niveles, firmas y si puedo firmar ahora.</summary>
    public async Task<AprobacionEstadoDto> ObtenerAprobacionesAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/{id}/aprobaciones", ct);
        if (!response.IsSuccessStatusCode)
        {
            // Igual que el panel de presupuesto: es informativo, no debe tumbar la pantalla.
            return new AprobacionEstadoDto();
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<AprobacionEstadoDto>(ct) ?? new AprobacionEstadoDto();
    }

    /// <summary>
    /// Qué debe ofrecer la pantalla: si la empresa exige escalera y si este usuario puede firmar.
    /// Se consulta una vez por carga; ante cualquier fallo devuelve "apagado", que es el
    /// comportamiento histórico y el más seguro.
    /// </summary>
    public async Task<AprobacionConfigVista> ObtenerAprobacionConfigAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/aprobacion-config", ct);
        if (!response.IsSuccessStatusCode) return new AprobacionConfigVista();

        return await response.ReadFromJsonAsyncWithAuthCheck<AprobacionConfigVista>(ct)
               ?? new AprobacionConfigVista();
    }

    /// <summary>
    /// Por cada orden en aprobación, si hay alguien con límite suficiente. Alimenta el aviso del
    /// listado cuando nadie puede autorizarla.
    /// </summary>
    public async Task<List<CapacidadAprobacionDto>> ObtenerCapacidadAprobacionAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/aprobacion-capacidad", ct);
        if (!response.IsSuccessStatusCode) return new List<CapacidadAprobacionDto>();

        return await response.ReadFromJsonAsyncWithAuthCheck<List<CapacidadAprobacionDto>>(ct)
               ?? new List<CapacidadAprobacionDto>();
    }

    /// <summary>Bandeja: órdenes esperando mi firma. Vacío si no tengo el permiso de aprobar.</summary>
    public async Task<List<PendienteAprobacionDto>> PendientesAprobacionAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/pendientes-aprobacion", ct);
        if (!response.IsSuccessStatusCode)
        {
            return new List<PendienteAprobacionDto>();
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<List<PendienteAprobacionDto>>(ct)
               ?? new List<PendienteAprobacionDto>();
    }

    /// <summary>
    /// Cómo quedaría el presupuesto si se aprobara la orden. Informativo: la validación real corre
    /// al aprobar. Si el control está apagado devuelve <c>Modo = 0</c> y la pantalla no muestra nada.
    /// </summary>
    public async Task<PresupuestoPrevioDto> ObtenerPresupuestoAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/{id}/presupuesto", ct);
        if (!response.IsSuccessStatusCode)
        {
            // El panel es una ayuda: si no se puede consultar, la pantalla sigue funcionando y la
            // validación de verdad ocurre al aprobar.
            return new PresupuestoPrevioDto();
        }
        return await response.ReadFromJsonAsyncWithAuthCheck<PresupuestoPrevioDto>(ct)
            ?? new PresupuestoPrevioDto();
    }

    /// <summary>Lo que la pantalla necesita saber del control de aprobación, en una sola llamada.</summary>
    public sealed class AprobacionConfigVista
    {
        /// <summary>La empresa exige escalera de firmas para las órdenes de compra.</summary>
        public bool Encendido { get; set; }

        /// <summary>Este usuario tiene el permiso de firmar (no si le toca a él, eso es por nivel).</summary>
        public bool PuedoFirmar { get; set; }
    }

    /// <summary>POST con motivo obligatorio (rechazar, cancelar, cerrar).</summary>
    private async Task<bool> EjecutarAccionConMotivoAsync(string url, string motivo, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck(url, new { Motivo = motivo }, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.ObtenerMensajeErrorAsync());
        }
        return true;
    }

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
