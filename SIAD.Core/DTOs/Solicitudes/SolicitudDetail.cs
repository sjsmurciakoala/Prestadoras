namespace SIAD.Core.DTOs.Solicitudes;

public record SolicitudDetailDto(
    int Id,
    string IdentificacionCliente,
    string NombreCliente,
    int CategoriaServicioId,
    string Telefono,
    string Direccion,
    string? Correo,
    string? Observacion,
    DateTime Fecha,
    bool Estado,
    string? CategoriaServicioNombre = null,
    string? EmpresaNombre = null,
    string? EmpresaTelefono = null,
    string? EmpresaDireccion = null,
    string? NegocioNombre = null,
    string? NegocioTelefono = null,
    string? NegocioClaveCatastral = null);
