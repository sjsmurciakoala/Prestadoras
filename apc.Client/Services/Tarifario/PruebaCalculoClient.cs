using System.Net.Http.Json;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class PruebaCalculoClient
{
    private readonly HttpClient http;

    public PruebaCalculoClient(HttpClient http) => this.http = http;

    public async Task<PruebaCalculoResultDto?> CalcularAsync(PruebaCalculoRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/prueba-calculo/calcular", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Error al calcular: {error}");
        }

        return await response.Content.ReadFromJsonAsync<PruebaCalculoResultDto>(cancellationToken: ct);
    }
}
