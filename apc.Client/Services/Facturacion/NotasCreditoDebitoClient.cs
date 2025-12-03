using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;

namespace apc.Client.Services.Facturacion;

public class NotasCreditoDebitoClient
{
    private readonly HttpClient _http;

    public NotasCreditoDebitoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/facturacion/notas/clientes"
            : $"api/facturacion/notas/clientes?query={Uri.EscapeDataString(query)}";

        var result = await _http.GetFromJsonAsync<List<NotaClienteLookupDto>>(url, ct);
        return result ?? new List<NotaClienteLookupDto>();
    }

    public Task<NotaClienteLookupDto?> ObtenerClienteAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Task.FromResult<NotaClienteLookupDto?>(null);
        }

        return _http.GetFromJsonAsync<NotaClienteLookupDto>($"api/facturacion/notas/clientes/{Uri.EscapeDataString(clave)}", ct);
    }

    public Task<NotaClienteConfiguracionDto?> ObtenerConfiguracionAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Task.FromResult<NotaClienteConfiguracionDto?>(null);
        }

        return _http.GetFromJsonAsync<NotaClienteConfiguracionDto>($"api/facturacion/notas/clientes/{Uri.EscapeDataString(clave)}/configuracion", ct);
    }

    public async Task<IReadOnlyList<NotaMotivoDto>> ListarMotivosAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<NotaMotivoDto>>("api/facturacion/notas/motivos", ct);
        return result ?? new List<NotaMotivoDto>();
    }

    public async Task<ResponseModelDto> RegistrarNotaAsync(NotaCrearRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/notas", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
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
                // Ignorar: contenido no parseable.
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
}
