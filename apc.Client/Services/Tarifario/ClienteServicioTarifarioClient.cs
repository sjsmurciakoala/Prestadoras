using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class ClienteServicioTarifarioClient
{
    private readonly HttpClient http;

    public ClienteServicioTarifarioClient(HttpClient http) => this.http = http;

    public async Task<ClienteServicioItemDto[]> ObtenerServiciosAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteServicioItemDto[]>(
               $"api/clientes/{clienteId}/servicios-tarifario", ct)
           ?? Array.Empty<ClienteServicioItemDto>();

    public async Task<ClienteServicioCatalogosDto?> ObtenerCatalogosAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteServicioCatalogosDto>(
               $"api/clientes/{clienteId}/servicios-tarifario/catalogos", ct);

    public async Task<ResponseModelDto?> GuardarAsync(int clienteId, ClienteServicioSaveRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/clientes/{clienteId}/servicios-tarifario", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<ResponseModelDto?> DesactivarAsync(int clienteId, ClienteServicioDesactivarRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/clientes/{clienteId}/servicios-tarifario/desactivar", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }
}
