namespace SIAD.Core.DTOs.Clientes;

public record ClienteDetailDto(
    int Id,
    string Codigo,
    string Nombre,
    string? Identidad,
    string? Rtn,
    string? Direccion,
    string? Telefono,
    string? Barrio,
    string? Ciclo,
    bool Activo);
