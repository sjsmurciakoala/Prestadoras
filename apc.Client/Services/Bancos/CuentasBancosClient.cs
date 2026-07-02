using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Bancos;

public sealed class CuentasBancosClient
{
    private readonly HttpClient httpClient;

    public CuentasBancosClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BancoCuentaListDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync($"api/bancos/cuentas?companyId={companyId}", ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoCuentaListDto>>(ct);
        return result ?? new List<BancoCuentaListDto>();
    }

    public async Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliacionAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly? fechaHasta = null,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        var url = $"api/bancos/cuentas/conciliacion?companyId={companyId}&bancoCuentaId={bancoCuentaId}";

        if (fechaHasta.HasValue)
        {
            url += $"&fechaHasta={fechaHasta.Value:yyyy-MM-dd}";
        }

        var response = await httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<BancoCuentaConciliacionDto>();
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoCuentaConciliacionDto>>(ct);
        return result ?? new List<BancoCuentaConciliacionDto>();
    }

    public async Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliadasAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        var url = $"api/bancos/cuentas/conciliacion/conciliadas?companyId={companyId}&bancoCuentaId={bancoCuentaId}";

        if (fechaDesde.HasValue)
        {
            url += $"&fechaDesde={fechaDesde.Value:yyyy-MM-dd}";
        }

        if (fechaHasta.HasValue)
        {
            url += $"&fechaHasta={fechaHasta.Value:yyyy-MM-dd}";
        }

        var response = await httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<BancoCuentaConciliacionDto>();
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoCuentaConciliacionDto>>(ct);
        return result ?? new List<BancoCuentaConciliacionDto>();
    }

    public async Task<DateTime> GetServerDateTimeAsync(CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync("api/bancos/cuentas/server-datetime", ct);
        var payload = await response.ReadFromJsonAsyncWithAuthCheck<JsonElement>(ct);

        if (payload.ValueKind == JsonValueKind.Object &&
            (payload.TryGetProperty("serverDateTime", out var serverDateTimeElement) ||
             payload.TryGetProperty("ServerDateTime", out serverDateTimeElement)) &&
            TryParseDateTime(serverDateTimeElement, out var serverDateTime))
        {
            return serverDateTime;
        }

        if (TryParseDateTime(payload, out serverDateTime))
        {
            return serverDateTime;
        }

        throw new InvalidOperationException("No se pudo obtener la fecha y hora actual del servidor.");
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasContablesAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await httpClient.GetAsync($"api/bancos/cuentas/contables?companyId={companyId}", ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<CuentaContableLookupDto>>(ct);
        return result ?? new List<CuentaContableLookupDto>();
    }

    public async Task<IReadOnlyList<BancoCuentaConciliacionDto>> ImportarConciliacionBancoAsync(
        IBrowserFile file,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(10 * 1024 * 1024);
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        var response = await httpClient.PostAsync("api/bancos/cuentas/conciliacion/importar", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible importar el archivo.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoCuentaConciliacionDto>>(ct);
        return result ?? new List<BancoCuentaConciliacionDto>();
    }

    public async Task ConciliarAsync(
        BancoCuentaConciliarDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/cuentas/conciliacion/confirmar", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible conciliar los movimientos.");
        }
    }

    public async Task<BancoCuentaEditDto?> GetByIdAsync(long cuentaId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cuentaId);

        var response = await httpClient.GetAsync($"api/bancos/cuentas/{cuentaId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<BancoCuentaEditDto>(ct);
    }

    public async Task<BancoCuentaEditDto> CreateAsync(BancoCuentaCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/cuentas", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible crear la cuenta bancaria.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoCuentaEditDto>(ct);
        return result ?? new BancoCuentaEditDto();
    }

    public async Task<BancoCuentaEditDto> UpdateAsync(long cuentaId, BancoCuentaEditDto dto, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cuentaId);
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PutAsJsonAsyncWithAuthCheck($"api/bancos/cuentas/{cuentaId}", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible actualizar la cuenta bancaria.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoCuentaEditDto>(ct);
        return result ?? dto;
    }

    public async Task DeleteAsync(long cuentaId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cuentaId);

        var response = await httpClient.DeleteAsync($"api/bancos/cuentas/{cuentaId}", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized || IsLoginRedirect(response))
        {
            throw new UnauthorizedAccessException("Su sesion ha expirado. Por favor, inicie sesion nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible eliminar la cuenta bancaria.");
        }
    }

    private static bool IsLoginRedirect(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Moved or HttpStatusCode.RedirectKeepVerb)
        {
            var location = response.Headers.Location;
            if (location is not null && location.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && finalUri.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (response.Content?.Headers?.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }

    private static bool TryParseDateTime(JsonElement element, out DateTime value)
    {
        value = default;

        if (element.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(element.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
