namespace SIAD.Core.DTOs.AuxiliarLectura;

public record AuxiliarLecturaPagedResponseDto(int TotalCount, IReadOnlyList<AuxiliarLecturaDto> Items);
