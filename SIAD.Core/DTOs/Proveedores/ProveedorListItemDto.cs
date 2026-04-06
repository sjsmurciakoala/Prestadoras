namespace SIAD.Core.DTOs.Proveedores;

public record ProveedorListItemDto(
    string Codigo,
    string Nombre,
    string? Direccion,
    string? Rtn,
    string? Telefono,
    bool Activo);
