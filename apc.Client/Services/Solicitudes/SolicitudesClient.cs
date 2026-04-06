using System.Net.Http.Json;
using SIAD.Core.DTOs.Solicitudes;

namespace apc.Client.Services.Solicitudes;

/// <summary>
/// Cliente HTTP para operaciones completas relacionadas con solicitudes de servicio.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class SolicitudesClient
{
    private readonly HttpClient http;

    public SolicitudesClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene listado de solicitudes, opcionalmente filtradas por identidad del cliente.
    /// </summary>
    public async Task<List<SolicitudListDto>> ObtenerAsync(string? clienteIdentidad = null, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(clienteIdentidad)
            ? string.Empty
            : $"?clienteIdentidad={Uri.EscapeDataString(clienteIdentidad)}";
        
        return await http.GetFromJsonAsync<List<SolicitudListDto>>($"api/solicitudes{query}", cancellationToken: ct)
               ?? new List<SolicitudListDto>();
    }

    /// <summary>
    /// Obtiene el detalle completo de una solicitud por su ID.
    /// </summary>
    public async Task<SolicitudDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<SolicitudDetailDto?>($"api/solicitudes/{id}", cancellationToken: ct);

    /// <summary>
    /// Obtiene todas las categorías de solicitud activas.
    /// </summary>
    public async Task<List<SolicitudCategoriaDto>> ObtenerCategoriasAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<SolicitudCategoriaDto>>("api/solicitudes/categorias", cancellationToken: ct)
           ?? new List<SolicitudCategoriaDto>();

    /// <summary>
    /// Crea una nueva solicitud de servicio.
    /// </summary>
    public async Task<int> CrearAsync(SolicitudCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        
        var response = await http.PostAsJsonAsync("api/solicitudes", dto, cancellationToken: ct);
        response.EnsureSuccessStatusCode();

        // Extrae el ID de la ubicación (Location header)
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        if (int.TryParse(location.Split('/').LastOrDefault(), out int id))
            return id;

        throw new HttpRequestException("No se pudo obtener el ID de la solicitud creada.");
    }

    /// <summary>
    /// Actualiza una solicitud de servicio existente.
    /// </summary>
    public async Task UpdateAsync(int id, SolicitudUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        
        var response = await http.PutAsJsonAsync($"api/solicitudes/{id}", dto, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Inactiva una solicitud (cambia estado a false).
    /// </summary>
    public async Task InactivarAsync(int id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/solicitudes/{id}", cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Marca una solicitud como asignada.
    /// </summary>
    public async Task AsignarAsync(int id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"api/solicitudes/{id}/asignar", null, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Desasigna una solicitud (marca como no asignada).
    /// </summary>
    public async Task DesasignarAsync(int id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"api/solicitudes/{id}/desasignar", null, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }
}
