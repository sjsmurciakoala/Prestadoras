namespace SIAD.Core.DTOs.AuxiliarLectura;

public record AuxiliarLecturaDto(
    string Clave,
    string Cliente,
    string? Ruta,
    string? Contador,
    decimal? LecturaActual,
    decimal? LecturaAnterior,
    decimal? Consumo,
    string? Condicion,
    DateTime? FechaLectura,
    string? Usuario);
