namespace SIAD.Core.DTOs.Medidores;

public record MedidorHistorialDto(
    DateOnly? Fecha,
    decimal? LecturaActual,
    decimal? LecturaAnterior,
    decimal? Consumo,
    string? Condicion,
    string? Observaciones,
    string? Usuario);
