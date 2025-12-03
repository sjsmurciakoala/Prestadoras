using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;

namespace apc.Client.Services.Facturacion;

public class FacturacionMiscelaneosClient
{
    private readonly HttpClient _http;

    public FacturacionMiscelaneosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ClienteLookupDto>> SearchClientesAsync(string? query, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/facturacion/miscelaneos/clientes"
            : $"api/facturacion/miscelaneos/clientes?query={Uri.EscapeDataString(query)}";

        var result = await _http.GetFromJsonAsync<List<ClienteLookupDto>>(url, ct);
        return result ?? new List<ClienteLookupDto>();
    }

    public Task<ClienteMiscelaneoDto?> GetClienteAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Task.FromResult<ClienteMiscelaneoDto?>(null);
        }

        return _http.GetFromJsonAsync<ClienteMiscelaneoDto>($"api/facturacion/miscelaneos/clientes/{Uri.EscapeDataString(clave)}", ct);
    }

    public async Task<IReadOnlyList<MiscelaneoCatalogoDto>> GetCatalogoAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MiscelaneoCatalogoDto>>("api/facturacion/miscelaneos/categorias", ct);
        return result ?? new List<MiscelaneoCatalogoDto>();
    }

    public async Task<ResponseModelDto> CrearReciboAsync(FacturaMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/miscelaneos/recibos", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public Task<FacturaMiscelaneoResponseDto?> GetReciboAsync(int numeroRecibo, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<FacturaMiscelaneoResponseDto>($"api/facturacion/miscelaneos/recibos/{numeroRecibo}", ct);

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
                // Contenido no parseable, continuar con payload por defecto.
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
