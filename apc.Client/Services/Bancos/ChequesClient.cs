using System.Net;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class ChequesClient
{
    private readonly HttpClient httpClient;

    public ChequesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ChequeListItemDto>> BuscarAsync(
        long? bancoId,
        long? bancoCuentaId,
        string? estado,
        DateTime? desde,
        DateTime? hasta,
        decimal? numeroCheque,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (bancoId.HasValue)
        {
            query.Add($"bancoId={bancoId.Value}");
        }

        if (bancoCuentaId is > 0)
        {
            query.Add($"bancoCuentaId={bancoCuentaId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query.Add($"estado={Uri.EscapeDataString(estado)}");
        }

        if (desde.HasValue)
        {
            query.Add($"desde={desde.Value:yyyy-MM-dd}");
        }

        if (hasta.HasValue)
        {
            query.Add($"hasta={hasta.Value:yyyy-MM-dd}");
        }

        if (numeroCheque is > 0)
        {
            query.Add($"numeroCheque={numeroCheque.Value}");
        }

        var url = "api/bancos/cheques" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var response = await httpClient.GetAsync(url, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<ChequeListItemDto>>(ct);
        return result ?? new List<ChequeListItemDto>();
    }

    public async Task<IReadOnlyList<ChequeBitacoraListItemDto>> BuscarBitacoraAsync(
        long? bancoId,
        long? bancoCuentaId,
        string? accion,
        DateTime? desde,
        DateTime? hasta,
        decimal? numeroCheque,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (bancoId.HasValue)
        {
            query.Add($"bancoId={bancoId.Value}");
        }

        if (bancoCuentaId is > 0)
        {
            query.Add($"bancoCuentaId={bancoCuentaId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(accion))
        {
            query.Add($"accion={Uri.EscapeDataString(accion)}");
        }

        if (desde.HasValue)
        {
            query.Add($"desde={desde.Value:yyyy-MM-dd}");
        }

        if (hasta.HasValue)
        {
            query.Add($"hasta={hasta.Value:yyyy-MM-dd}");
        }

        if (numeroCheque is > 0)
        {
            query.Add($"numeroCheque={numeroCheque.Value}");
        }

        var url = "api/bancos/cheques/bitacora" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        var response = await httpClient.GetAsync(url, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<ChequeBitacoraListItemDto>>(ct);
        return result ?? new List<ChequeBitacoraListItemDto>();
    }

    public async Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        var response = await httpClient.GetAsync($"api/bancos/cheques/proximo/{bancoCuentaId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ProximoChequeDto>(ct);
    }

    /// <summary>cheque_id vigente ligado a un movimiento bancario (para reimprimir); null si no hay cheque.</summary>
    public async Task<long?> GetChequeIdPorKardexAsync(long banKardexId, CancellationToken ct = default)
    {
        if (banKardexId <= 0)
        {
            return null;
        }

        var response = await httpClient.GetAsync($"api/bancos/cheques/por-kardex/{banKardexId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var dto = await response.ReadFromJsonAsyncWithAuthCheck<ChequePorKardexResponse>(ct);
        return dto?.ChequeId;
    }

    private sealed class ChequePorKardexResponse
    {
        public long? ChequeId { get; set; }
    }

    public async Task<decimal> AnularSiguienteAsync(long bancoCuentaId, string motivo, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck(
            $"api/bancos/cheques/{bancoCuentaId}/anular-siguiente",
            new AnularNumeroChequeDto { Motivo = motivo },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible anular el siguiente numero de cheque.");
        }

        return await response.Content.ReadFromJsonAsync<decimal>(cancellationToken: ct);
    }

    /// <summary>Emite un cheque manual (suelto): movimiento bancario + partida + cheque.</summary>
    public async Task<ChequeManualResultadoDto> EmitirManualAsync(
        ChequeManualCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/cheques/manual", dto, ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible emitir el cheque manual.");
        }

        var resultado = await response.Content.ReadFromJsonAsync<ChequeManualResultadoDto>(cancellationToken: ct);
        return resultado ?? throw new HttpRequestException("El servidor no devolvió el resultado del cheque.");
    }

    /// <summary>URL del comprobante interno del cheque (PDF, formato COMPAGOL) para abrir con window.open.</summary>
    public static string GetComprobantePdfUrl(long chequeId)
        => $"/api/bancos/cheques/{chequeId}/comprobante/pdf";

    /// <summary>URL del cheque CORTO para el cliente (PDF, formato COMPAGOLG) para abrir con window.open.</summary>
    public static string GetChequePdfUrl(long chequeId)
        => $"/api/bancos/cheques/{chequeId}/cheque/pdf";

    /// <summary>URL del cheque LARGO con detalle (PDF, cheque + comprobante en una hoja) para window.open.</summary>
    public static string GetChequeDetallePdfUrl(long chequeId)
        => $"/api/bancos/cheques/{chequeId}/cheque-detalle/pdf";
}
