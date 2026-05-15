namespace SIAD.Core.DTOs.Clientes;

public record ClienteListItemDto(
    int Id,
    string Codigo,
    string Nombre,
    string? Identidad,
    string? Barrio,
    bool Activo,
    string? CicloCodigo,
    string? Ruta);
