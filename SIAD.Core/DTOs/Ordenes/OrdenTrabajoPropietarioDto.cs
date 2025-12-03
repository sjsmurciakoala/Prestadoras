namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoPropietarioDto(
    string Clave,
    string Nombre,
    string? Direccion,
    bool Activo);
