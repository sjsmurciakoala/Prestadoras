using System.Net.Http.Json;
using SIAD.Core.DTOs.Facturacion;

namespace apc.Client.Services.Facturacion;

/// <summary>
/// Emisión de facturas de lectura desde el portal.
/// </summary>
public class EmisionLecturaClient
{
    private const string Base = "api/facturacion/emision-lectura";

    private readonly HttpClient _http;

    public EmisionLecturaClient(HttpClient http) => _http = http;

    /// <summary>Bloque de folios del portal; nulo si todavía no hay CAI vigente configurado.</summary>
    public async Task<BloqueCaiPortalDto?> ObtenerBloqueAsync(CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"{Base}/bloque", ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        return await respuesta.Content.ReadFromJsonAsync<BloqueCaiPortalDto>(cancellationToken: ct);
    }

    /// <summary>Calcula lo que saldría en el papel, sin emitir.</summary>
    public async Task<PreviewFacturaLecturaDto> PrevisualizarAsync(
        EmitirFacturaLecturaRequest request, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync($"{Base}/preview", request, ct);

        var preview = respuesta.IsSuccessStatusCode
            ? await respuesta.Content.ReadFromJsonAsync<PreviewFacturaLecturaDto>(cancellationToken: ct)
            : null;

        return preview ?? new PreviewFacturaLecturaDto
        {
            Encontrado = false,
            Mensaje = await ObtenerMensajeAsync(respuesta, ct),
        };
    }

    /// <summary>
    /// Emite la factura. Un rechazo de negocio no es una excepción: vuelve en el resultado con
    /// su código y su mensaje, que es lo que la pantalla muestra.
    /// </summary>
    public async Task<EmitirFacturaLecturaResultado> EmitirAsync(
        EmitirFacturaLecturaRequest request, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync(Base, request, ct);

        var resultado = respuesta.IsSuccessStatusCode
            ? await respuesta.Content.ReadFromJsonAsync<EmitirFacturaLecturaResultado>(cancellationToken: ct)
            : null;

        return resultado ?? new EmitirFacturaLecturaResultado
        {
            Success = false,
            Codigo = "ERROR_RED",
            Mensaje = await ObtenerMensajeAsync(respuesta, ct),
        };
    }

    private static async Task<string> ObtenerMensajeAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return "El servidor respondió sin resultado.";
        }

        try
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
            if (cuerpo is not null && cuerpo.TryGetValue("error", out var detalle) && !string.IsNullOrWhiteSpace(detalle))
            {
                return detalle;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or NotSupportedException)
        {
            // El cuerpo no traía el JSON esperado; queda el código de estado, que ya dice algo.
        }

        return $"No se pudo emitir la factura ({(int)respuesta.StatusCode}).";
    }
}
