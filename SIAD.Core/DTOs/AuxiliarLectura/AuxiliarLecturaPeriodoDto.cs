namespace SIAD.Core.DTOs.AuxiliarLectura;

public record AuxiliarLecturaPeriodoDto(
    int Anio,
    int Mes,
    string? Ciclo,
    bool EstaAbierto,
    DateTime? FechaCierre);
