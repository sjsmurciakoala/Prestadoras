using System.Net;
using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class ConfiguracionTransaccionesClient
{
    private readonly HttpClient httpClient;

    public ConfiguracionTransaccionesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BancoConfiguracionTransaccionListDto>> GetAsync(
        BancoConfiguracionTransaccionFilterDto? filtro = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro?.Search))
        {
            query.Add($"search={Uri.EscapeDataString(filtro.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro?.EntraSale))
        {
            query.Add($"entraSale={Uri.EscapeDataString(filtro.EntraSale)}");
        }

        var url = query.Count > 0
            ? $"api/bancos/configuracion-transacciones?{string.Join("&", query)}"
            : "api/bancos/configuracion-transacciones";

        var response = await httpClient.GetAsync(url, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoConfiguracionTransaccionListDto>>(ct);
        return result ?? new List<BancoConfiguracionTransaccionListDto>();
    }

    public async Task<BancoConfiguracionTransaccionEditDto?> GetByIdAsync(string tipoTransaccionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            return null;
        }

        var encoded = Uri.EscapeDataString(tipoTransaccionId);
        var response = await httpClient.GetAsync($"api/bancos/configuracion-transacciones/{encoded}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<BancoConfiguracionTransaccionEditDto>(ct);
    }

    public async Task<BancoConfiguracionTransaccionEditDto> CreateAsync(BancoConfiguracionTransaccionEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos/configuracion-transacciones", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible crear la configuracion de transaccion.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoConfiguracionTransaccionEditDto>(ct);
        return result ?? dto;
    }

    public async Task<BancoConfiguracionTransaccionEditDto> UpdateAsync(string tipoTransaccionId, BancoConfiguracionTransaccionEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            throw new ArgumentException("El codigo es obligatorio.", nameof(tipoTransaccionId));
        }

        var encoded = Uri.EscapeDataString(tipoTransaccionId);
        var response = await httpClient.PutAsJsonAsyncWithAuthCheck($"api/bancos/configuracion-transacciones/{encoded}", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible actualizar la configuracion de transaccion.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoConfiguracionTransaccionEditDto>(ct);
        return result ?? dto;
    }

    public async Task DeleteAsync(string tipoTransaccionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            return;
        }

        var encoded = Uri.EscapeDataString(tipoTransaccionId);
        var response = await httpClient.DeleteAsync($"api/bancos/configuracion-transacciones/{encoded}", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized || IsLoginRedirect(response))
        {
            throw new UnauthorizedAccessException("Su sesion ha expirado. Por favor, inicie sesion nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible eliminar la configuracion de transaccion.");
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
}
