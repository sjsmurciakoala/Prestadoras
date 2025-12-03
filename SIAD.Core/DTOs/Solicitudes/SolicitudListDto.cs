
namespace SIAD.Core.DTOs.Solicitudes;

public record SolicitudListDto(
    int Id,
    string IdentificacionCliente,
    string NombreCliente,
    int CategoriaServicioId,
    string CategoriaServicioNombre,
    DateTime Fecha,
    bool Estado);
