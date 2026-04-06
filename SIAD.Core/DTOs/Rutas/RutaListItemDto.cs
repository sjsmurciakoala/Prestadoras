namespace SIAD.Core.DTOs.Rutas;

public record RutaListItemDto(
    int Id,
    int CodCiclo,
    string CodRuta,
    string? Descripcion,
    bool Activo);
