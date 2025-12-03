using System.Net.Http.Json;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.CaptacionPagos;

public class CaptacionPagosClient
{
    private readonly HttpClient _http;

    public CaptacionPagosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CajaDto>> GetCajasAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<CajaDto>>("api/captacionpagos/cajas", ct);
        return result ?? new List<CajaDto>();
    }

    public async Task<IReadOnlyList<ArqueoDto>> GetArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildArqueoQuery(filtro);
        var url = string.IsNullOrWhiteSpace(query) ? "api/captacionpagos/arqueos" : $"api/captacionpagos/arqueos?{query}";
        var result = await _http.GetFromJsonAsync<List<ArqueoDto>>(url, ct);
        return result ?? new List<ArqueoDto>();
    }

    public Task<CaptacionPagoResponseDto?> GetPagoAsync(string numFactura, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<CaptacionPagoResponseDto>($"api/captacionpagos/{Uri.EscapeDataString(numFactura)}", ct);

    public async Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/reverso", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<IReadOnlyList<ReciboMiscelaneoDto>> GetMiscelaneosAsync(string? clienteClave, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(clienteClave)
            ? "api/captacionpagos/miscelaneos"
            : $"api/captacionpagos/miscelaneos?clienteClave={Uri.EscapeDataString(clienteClave)}";

        var result = await _http.GetFromJsonAsync<List<ReciboMiscelaneoDto>>(url, ct);
        return result ?? new List<ReciboMiscelaneoDto>();
    }

    private static async Task<ResponseModelDto> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ResponseModelDto? payload = null;

        if (response.Content is not null)
        {
            try
            {
                payload = await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
            }
            catch
            {
                // Ignored: el contenido no pudo deserializarse, continuar con payload por defecto.
            }
        }

        payload ??= new ResponseModelDto
        {
            Success = response.IsSuccessStatusCode,
            Message = response.ReasonPhrase ?? string.Empty
        };

        payload.Success = payload.Success && response.IsSuccessStatusCode;

        if (!payload.Success && string.IsNullOrWhiteSpace(payload.Message))
        {
            payload.Message = "La operación no pudo completarse.";
        }

        return payload;
    }

    private static string BuildArqueoQuery(CaptacionArqueoFilterDto? filtro)
    {
        if (filtro is null)
        {
            return string.Empty;
        }

        var parametros = new List<string>();

        if (filtro.CajaId.HasValue && filtro.CajaId > 0)
        {
            parametros.Add($"CajaId={filtro.CajaId}");
        }

        if (filtro.FechaInicio.HasValue)
        {
            parametros.Add($"FechaInicio={Uri.EscapeDataString(filtro.FechaInicio.Value.ToString("yyyy-MM-dd"))}");
        }

        if (filtro.FechaFin.HasValue)
        {
            parametros.Add($"FechaFin={Uri.EscapeDataString(filtro.FechaFin.Value.ToString("yyyy-MM-dd"))}");
        }

        return string.Join("&", parametros);
    }
}
