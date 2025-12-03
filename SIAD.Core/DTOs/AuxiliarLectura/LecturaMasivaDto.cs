using System.Collections.Generic;

namespace SIAD.Core.DTOs.AuxiliarLectura;

public record LecturaMasivaDto(
    int Anio,
    int Mes,
    string? Ciclo,
    IReadOnlyList<LecturaMasivaItemDto> Lecturas);

public record LecturaMasivaItemDto(
    string Clave,
    string Contador,
    decimal LecturaActual,
    decimal LecturaAnterior,
    string Usuario);
