using System.Net.Http.Json;
using SIAD.Core.DTOs.Solicitudes;

namespace apc.Client.Services.Solicitudes;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con solicitudes de servicio.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class SolicitudesClient
{
    private readonly HttpClient http;

    public SolicitudesClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene todas las solicitudes de un cliente por su identidad (cédula/RTN).
    /// </summary>
    public async Task<List<SolicitudListDto>> ObtenerPorClienteAsync(string? clienteIdentidad, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(clienteIdentidad)
            ? string.Empty
            : $"?clienteIdentidad={Uri.EscapeDataString(clienteIdentidad)}";
        
        return await http.GetFromJsonAsync<List<SolicitudListDto>>($"api/solicitudes{query}", ct)
               ?? new List<SolicitudListDto>();
    }

    /// <summary>
    /// Obtiene el detalle completo de una solicitud por su ID.
    /// </summary>
    public async Task<SolicitudDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<SolicitudDetailDto?>($"api/solicitudes/{id}", ct);

    /// <summary>
    /// Crea una nueva solicitud de servicio.
    /// </summary>
    public async Task<HttpResponseMessage> CrearAsync(SolicitudDetailDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return await http.PostAsJsonAsync("api/solicitudes", dto, cancellationToken: ct);
    }

    /// <summary>
    /// Obtiene todas las categorías de solicitud activas.
    /// </summary>
    public async Task<List<SolicitudCategoriaDto>> ObtenerCategoriasAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<SolicitudCategoriaDto>>("api/solicitudes/categorias", ct)
           ?? new List<SolicitudCategoriaDto>();
}
