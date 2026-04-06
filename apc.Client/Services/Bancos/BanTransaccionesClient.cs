using System.Net;
using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class BanTransaccionesClient
{
    private readonly HttpClient httpClient;

    public BanTransaccionesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(
        long companyId,
        long? bancoId = null,
        long? bancoCuentaId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        bool incluirAnuladas = false,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var url = $"api/bancos/transacciones?companyId={companyId}";

        if (bancoId.HasValue)
        {
            url += $"&bancoId={bancoId.Value}";
        }

        if (bancoCuentaId.HasValue)
        {
            url += $"&bancoCuentaId={bancoCuentaId.Value}";
        }

        if (fechaDesde.HasValue)
        {
            url += $"&fechaDesde={fechaDesde.Value:yyyy-MM-dd}";
        }

        if (fechaHasta.HasValue)
        {
            url += $"&fechaHasta={fechaHasta.Value:yyyy-MM-dd}";
        }

        if (incluirAnuladas)
        {
            url += "&incluirAnuladas=true";
        }

        var response = await httpClient.GetAsync(url, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BanTransaccionListDto>>(ct);
        return result ?? new List<BanTransaccionListDto>();
    }

    public async Task<BanTransaccionListDto?> GetByIdAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(banKardexId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync(
            $"api/bancos/transacciones/{banKardexId}?companyId={companyId}",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<BanTransaccionListDto>(ct);
    }

    public async Task<BanTransaccionDetalleDto?> GetDetalleAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(banKardexId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync(
            $"api/bancos/transacciones/{banKardexId}/detalle?companyId={companyId}",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<BanTransaccionDetalleDto>(ct);
    }

    public async Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(
        BanTransaccionCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/transacciones", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = $"Error {response.StatusCode}: {response.ReasonPhrase}";
            }

            throw new HttpRequestException(detail);
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<(long, decimal)>(ct);
        return result;
    }

    public async Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(
        BanTransaccionAnularDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/transacciones/anular", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = $"Error {response.StatusCode}: {response.ReasonPhrase}";
            }

            throw new HttpRequestException(detail);
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<(long, decimal)>(ct);
        return result;
    }
}

