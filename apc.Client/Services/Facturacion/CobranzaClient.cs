using System.Globalization;
using System.Net.Http.Json;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.Facturacion;

public class CobranzaClient
{
    private readonly HttpClient _http;

    public CobranzaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CobranzaSaldoDetalleDto>> ObtenerSaldosAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<CobranzaSaldoDetalleDto>();
        }

        var url = $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/saldos";
        var result = await _http.GetFromJsonAsync<List<CobranzaSaldoDetalleDto>>(url, ct);
        return result ?? new List<CobranzaSaldoDetalleDto>();
    }

    public async Task<bool> EstaBloqueadoAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return false;
        }

        var url = $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/bloqueo";
        var result = await _http.GetFromJsonAsync<BloqueoResponse>(url, ct);
        return result?.Bloqueado ?? false;
    }

    public async Task<string?> NumeroALetrasAsync(decimal valor, CancellationToken ct = default)
    {
        var url = $"api/cobranza/numero-letras?valor={valor.ToString(CultureInfo.InvariantCulture)}";
        var result = await _http.GetFromJsonAsync<NumeroLetrasResponse>(url, ct);
        return result?.Texto;
    }

    public async Task<CobranzaPlanPreviewDto> CalcularPlanAsync(CobranzaPlanPreviewRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/planes/calcular", dto, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CobranzaPlanPreviewDto>(cancellationToken: ct) ?? new CobranzaPlanPreviewDto();
    }

    public async Task<ResponseModelDto> GuardarPlanAsync(CobranzaPlanGuardarDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/planes", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<IReadOnlyList<CobranzaPlanResumenDto>> ListarPlanesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<CobranzaPlanResumenDto>>("api/cobranza/planes", ct);
        return result ?? new List<CobranzaPlanResumenDto>();
    }

    public Task<CobranzaPlanDetalleDto?> ObtenerPlanAsync(string correlativo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlativo))
        {
            return Task.FromResult<CobranzaPlanDetalleDto?>(null);
        }

        return _http.GetFromJsonAsync<CobranzaPlanDetalleDto>($"api/cobranza/planes/{Uri.EscapeDataString(correlativo)}", ct);
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
                // Ignorar contenido no parseable
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

    private sealed class BloqueoResponse
    {
        public bool Bloqueado { get; set; }
    }

    private sealed class NumeroLetrasResponse
    {
        public decimal Valor { get; set; }
        public string? Texto { get; set; }
    }
}
