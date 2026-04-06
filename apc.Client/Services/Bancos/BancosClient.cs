using System.Net;
using apc.Client.Services;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public sealed class BancosClient
{
    private readonly HttpClient httpClient;

    public BancosClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<BancoListDto>> GetAsync(BancoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var url = BuildUrl(filtro);
        var response = await httpClient.GetAsync(url, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<List<BancoListDto>>(ct);
        return result ?? new List<BancoListDto>();
    }

    public async Task<BancoEditDto?> GetByIdAsync(long bancoId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/bancos/{bancoId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<BancoEditDto>(ct);
    }

    public async Task<BancoEditDto> CreateAsync(BancoCreateDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsyncWithAuthCheck("api/bancos", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoEditDto>(ct);
        return result ?? new BancoEditDto();
    }

    public async Task<BancoEditDto> UpdateAsync(long bancoId, BancoEditDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsyncWithAuthCheck($"api/bancos/{bancoId}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<BancoEditDto>(ct);
        return result ?? dto;
    }

    public async Task DeleteAsync(long bancoId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/bancos/{bancoId}", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized || IsLoginRedirect(response))
        {
            throw new UnauthorizedAccessException("Su sesion ha expirado. Por favor, inicie sesion nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? "No fue posible eliminar el banco.");
        }
    }

    private static string BuildUrl(BancoFilterDto? filtro)
    {
        if (filtro is null)
        {
            return "api/bancos";
        }

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
        {
            parts.Add($"nombre={Uri.EscapeDataString(filtro.Nombre)}");
        }

        if (filtro.Activo.HasValue)
        {
            parts.Add($"activo={filtro.Activo.Value.ToString().ToLowerInvariant()}");
        }

        return parts.Count == 0 ? "api/bancos" : $"api/bancos?{string.Join("&", parts)}";
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
