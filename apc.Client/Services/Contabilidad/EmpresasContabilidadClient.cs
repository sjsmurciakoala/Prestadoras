using System.Net.Http.Json;
using System.Text.Json;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

public sealed class EmpresasContabilidadClient
{
    private readonly HttpClient http;

    public EmpresasContabilidadClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<CompanyCreationDto> CrearAsync(CompanyCreationDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/contabilidad/empresas", dto, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(string.IsNullOrWhiteSpace(mensaje)
                ? "No fue posible crear la empresa."
                : mensaje);
        }

        var resultado = await response.Content.ReadFromJsonAsync<CompanyCreationDto>(cancellationToken: ct);
        if (resultado is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return resultado;
    }

    private static async Task<string?> ObtenerMensajeErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var contenido = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(contenido);
            var root = document.RootElement;

            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }
        }
        catch (JsonException)
        {
            return contenido;
        }

        return contenido;
    }
}
