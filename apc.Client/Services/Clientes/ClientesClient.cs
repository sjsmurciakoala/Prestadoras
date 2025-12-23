using System.Net.Http.Json;
using SIAD.Core.DTOs.Clientes;

namespace apc.Client.Services.Clientes;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con clientes.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class ClientesClient
{
    private readonly HttpClient http;

    public ClientesClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene los datos completos de un cliente por su ID.
    /// </summary>
    public async Task<ClienteDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteDetailDto?>($"api/clientes/{id}", ct);

    /// <summary>
    /// Obtiene todas las tarifas asociadas a un cliente.
    /// </summary>
    public async Task<ClienteTarifaDto[]> ObtenerTarifasAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteTarifaDto[]>($"api/clientes/{clienteId}/tarifas", ct)
           ?? Array.Empty<ClienteTarifaDto>();

    /// <summary>
    /// Obtiene el estado de cuenta (resumen financiero) de un cliente.
    /// </summary>
    public async Task<ClienteEstadoCuentaDto?> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteEstadoCuentaDto?>($"api/clientes/{clienteId}/estado-cuenta", ct);

    /// <summary>
    /// Obtiene todos los movimientos (transacciones) de un cliente.
    /// </summary>
    public async Task<ClienteMovimientoDto[]> ObtenerMovimientosAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteMovimientoDto[]>($"api/clientes/{clienteId}/movimientos", ct)
           ?? Array.Empty<ClienteMovimientoDto>();
}