using SIAD.Core.DTOs.Solicitudes;

namespace SIAD.Services.Solicitudes;

/// <summary>
/// Interfaz para gestión completa de solicitudes de servicio.
/// </summary>
public interface ISolicitudesService
{
    /// <summary>
    /// Obtiene listado de solicitudes, opcionalmente filtradas por identidad del cliente.
    /// </summary>
    Task<IReadOnlyList<SolicitudListDto>> GetSolicitudesAsync(string? clienteIdentidad = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el detalle completo de una solicitud por ID.
    /// </summary>
    Task<SolicitudDetailDto?> GetSolicitudAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene listado de categorías de servicio activas.
    /// </summary>
    Task<IReadOnlyList<SolicitudCategoriaDto>> GetCategoriasAsync(CancellationToken ct = default);

    /// <summary>
    /// Crea una nueva solicitud de servicio.
    /// </summary>
    Task<int> CreateSolicitudAsync(SolicitudCreateDto dto, string usuarioCreacion, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una solicitud de servicio existente.
    /// </summary>
    Task UpdateSolicitudAsync(SolicitudUpdateDto dto, string usuarioModificacion, CancellationToken ct = default);

    /// <summary>
    /// Inactiva una solicitud (cambia estado a false).
    /// </summary>
    Task InactivateSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);

    /// <summary>
    /// Marca una solicitud como asignada.
    /// </summary>
    Task AsignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);

    /// <summary>
    /// Desasigna una solicitud (marca como no asignada).
    /// </summary>
    Task DesasignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);
}
