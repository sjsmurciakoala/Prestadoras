using SIAD.Core.DTOs.Solicitudes;

namespace SIAD.Services.Solicitudes;

public interface ISolicitudesService
{
    Task<IReadOnlyList<SolicitudListDto>> GetSolicitudesAsync(string? clienteIdentidad, CancellationToken ct = default);
    Task<SolicitudDetailDto?> GetSolicitudAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SolicitudCategoriaDto>> GetCategoriasAsync(CancellationToken ct = default);
    Task<int> CreateSolicitudAsync(SolicitudDetailDto dto, CancellationToken ct = default); // opcional
}
