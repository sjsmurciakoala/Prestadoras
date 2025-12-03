namespace SIAD.Core.DTOs.Medidores;

public record MedidorFilterDto(
    string? Numero,
    string? Marca,
    bool? Estado,
    bool? Asignado,
    string? ClienteClave);
