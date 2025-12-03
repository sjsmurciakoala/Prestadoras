namespace SIAD.Core.DTOs.Clientes;

public record ClienteListItemDto(
    int Id,
    string Codigo,
    string Nombre,
    string? Barrio,
    bool Activo);
