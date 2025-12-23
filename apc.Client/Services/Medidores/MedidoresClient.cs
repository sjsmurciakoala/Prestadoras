using System.Net.Http.Json;
using SIAD.Core.DTOs.Medidores;

namespace apc.Client.Services.Medidores;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con medidores.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class MedidoresClient
{
    private readonly HttpClient http;

    public MedidoresClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene la lista de medidores con filtros opcionales.
    /// </summary>
    /// <param name="clienteClave">Filtro: código del cliente</param>
    /// <param name="asignado">Filtro: true=asignados, false=disponibles</param>
    public async Task<List<MedidorListDto>> ObtenerListaAsync(
        string? clienteClave = null,
        bool? asignado = null,
        CancellationToken ct = default)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(clienteClave))
            queryParts.Add($"ClienteClave={Uri.EscapeDataString(clienteClave)}");
        
        if (asignado.HasValue)
            queryParts.Add($"Asignado={asignado.Value.ToString().ToLowerInvariant()}");

        var query = queryParts.Count > 0 ? $"?{string.Join("&", queryParts)}" : string.Empty;
        
        return await http.GetFromJsonAsync<List<MedidorListDto>>($"api/medidores{query}", ct)
               ?? new List<MedidorListDto>();
    }

    /// <summary>
    /// Obtiene el detalle completo de un medidor por su ID.
    /// </summary>
    public async Task<MedidorDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<MedidorDetailDto?>($"api/medidores/{id}", ct);

    /// <summary>
    /// Asigna un medidor a un cliente.
    /// </summary>
    public async Task<HttpResponseMessage> AsignarAsync(int medidorId, int clienteId, CancellationToken ct = default)
    {
        var payload = new { MedidorId = medidorId, ClienteId = clienteId };
        return await http.PostAsJsonAsync("api/medidores/asignar", payload, cancellationToken: ct);
    }

    /// <summary>
    /// Registra una lectura para un cliente sin medidor.
    /// </summary>
    public async Task<HttpResponseMessage> RegistrarLecturaSinMedidorAsync(
        LecturaSinMedidorModel lectura,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        return await http.PostAsJsonAsync("api/medidores/lecturas-sin-medidor", lectura, cancellationToken: ct);
    }

    /// <summary>
    /// Modelo para registrar lecturas sin medidor asociado.
    /// </summary>
    public class LecturaSinMedidorModel
    {
        public string Clave { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Today;
        public decimal Lectura { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }
}
