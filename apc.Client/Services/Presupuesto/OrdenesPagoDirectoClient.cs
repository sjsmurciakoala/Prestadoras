using System.Net;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;

namespace apc.Client.Services.Presupuesto;

public sealed class OrdenesPagoDirectoClient
{
    private readonly HttpClient _http;

    public OrdenesPagoDirectoClient(HttpClient http)
    {
        _http = http;
    }

    public static string GetPdfUrl(int numeroOrden)
        => $"/api/presupuesto/ordenes-pago-directo/{numeroOrden}/pdf";

    public static string GetComprobanteAbonoUrl(int numeroOrden, int numeroAbono)
        => $"/api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos/{numeroAbono}/comprobante/pdf";

    public async Task<List<OrdenPagoDirectoListItemDto>> GetAsync(
        OrdenPagoDirectoFilterDto? filtro = null,
        CancellationToken ct = default)
    {
        var parameters = new List<string>();

        if (filtro?.NumeroOrden is > 0)
        {
            parameters.Add($"numeroOrden={filtro.NumeroOrden.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filtro?.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filtro.Search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filtro?.CodigoProveedor))
        {
            parameters.Add($"codigoProveedor={Uri.EscapeDataString(filtro.CodigoProveedor.Trim())}");
        }

        if (filtro?.IncludeProcessed == true)
        {
            parameters.Add("includeProcessed=true");
        }

        var url = parameters.Count == 0
            ? "api/presupuesto/ordenes-pago-directo"
            : $"api/presupuesto/ordenes-pago-directo?{string.Join("&", parameters)}";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenPagoDirectoListItemDto>>(ct)
            ?? new List<OrdenPagoDirectoListItemDto>();
    }

    public async Task<OrdenPagoDirectoDetalleDto?> GetByNumeroOrdenAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/presupuesto/ordenes-pago-directo/{numeroOrden}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<OrdenPagoDirectoDetalleDto>(ct);
    }

    public async Task<List<OrdenPagoDirectoCentroCostoLookupDto>> GetCentrosCostoAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/ordenes-pago-directo/centros-costo", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<OrdenPagoDirectoCentroCostoLookupDto>>(ct)
            ?? new List<OrdenPagoDirectoCentroCostoLookupDto>();
    }

    public async Task<List<CuentaContableLookupDto>> GetCuentasContablesAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/ordenes-pago-directo/cuentas-contables", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CuentaContableLookupDto>>(ct)
            ?? new List<CuentaContableLookupDto>();
    }

    public async Task<List<CuentaContableLookupDto>> GetCuentasContraProcesamientoAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/ordenes-pago-directo/cuentas-contra-procesamiento", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CuentaContableLookupDto>>(ct)
            ?? new List<CuentaContableLookupDto>();
    }

    public async Task<List<CuentaContableLookupDto>> GetCuentasGastoAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/ordenes-pago-directo/cuentas-gasto", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<CuentaContableLookupDto>>(ct)
            ?? new List<CuentaContableLookupDto>();
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> CrearAsync(
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/presupuesto/ordenes-pago-directo", dto, ct);
        return await ReadOperationResultAsync(response, ct);
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> ActualizarAsync(
        int numeroOrden,
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));
        }

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PutAsJsonAsync($"api/presupuesto/ordenes-pago-directo/{numeroOrden}", dto, ct);
        return await ReadOperationResultAsync(response, ct);
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> EliminarAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));
        }

        var response = await _http.DeleteAsync($"api/presupuesto/ordenes-pago-directo/{numeroOrden}", ct);
        return await ReadOperationResultAsync(response, ct);
    }

    public Task<OrdenPagoDirectoOperacionResultadoDto> ProcesarAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        return ProcesarAsync(numeroOrden, new ProcesarOrdenPagoDirectoDto(), ct);
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> ProcesarAsync(
        int numeroOrden,
        ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/procesar",
            dto,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<OrdenPagoDirectoOperacionResultadoDto>(ct);
        if (result is null)
            throw new InvalidOperationException("La respuesta del proceso vino vacia.");

        return result;
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/generar-partida",
            new { },
            ct);
        return await ReadOperationResultAsync(response, ct);
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> AnularAsync(
        int numeroOrden,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/anular",
            dto,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<OrdenPagoDirectoOperacionResultadoDto>(ct);
        if (result is null)
            throw new InvalidOperationException("La respuesta de la anulacion vino vacia.");

        return result;
    }

    public async Task<CompromisoSaldoDto?> GetSaldoConAbonosAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/saldo", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<CompromisoSaldoDto>(ct);
    }

    public async Task<AbonoCompromisoResultadoDto> RegistrarAbonoAsync(
        int numeroOrden,
        AbonoCompromisoUpsertDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos", dto, ct);
        return await ReadAbonoResultAsync(response, ct);
    }

    public async Task<AbonoCompromisoResultadoDto> AnularAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        if (numeroAbono <= 0)
            throw new ArgumentException("El numero de abono no es valido.", nameof(numeroAbono));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos/{numeroAbono}/anular", dto, ct);
        return await ReadAbonoResultAsync(response, ct);
    }

    private static async Task<OrdenPagoDirectoOperacionResultadoDto> ReadOperationResultAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var result = await response.ReadFromJsonAsyncWithAuthCheck<OrdenPagoDirectoOperacionResultadoDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La respuesta de la operacion vino vacia.");
        }

        return result;
    }

    private static async Task<AbonoCompromisoResultadoDto> ReadAbonoResultAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        // 409 (Conflict) y 400 (BadRequest) son RECHAZOS DE NEGOCIO: el servicio devuelve
        // Ok/Conflict segun result.Success, y ademas el controller mapea ArgumentException/
        // InvalidOperationException a BadRequest/Conflict. El cuerpo trae el mensaje (ya sea un
        // AbonoCompromisoResultadoDto o un { message }). No hay que lanzar: se traduce a un
        // resultado con Success=false para que la pantalla muestre el motivo real del rechazo.
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            return new AbonoCompromisoResultadoDto
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(mensaje) ? "No se pudo completar la operacion." : mensaje
            };
        }

        // 2xx normal (y 401/403/redirect -> UnauthorizedAccessException; 5xx -> HttpRequestException).
        var result = await response.ReadFromJsonAsyncWithAuthCheck<AbonoCompromisoResultadoDto>(ct);
        return result ?? new AbonoCompromisoResultadoDto
        {
            Success = false,
            Message = "No se recibio respuesta del servidor."
        };
    }
}
