namespace SIAD.Core.DTOs.Rutas;

public record RutaDetailDto(
    int Id,
    int CodCiclo,
    string CodRuta,
    string? Descripcion,
    string? CicloDescripcion);
