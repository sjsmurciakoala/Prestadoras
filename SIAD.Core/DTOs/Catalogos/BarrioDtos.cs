namespace SIAD.Core.DTOs.Catalogos;

public record BarrioDto(string Codigo, string Descripcion, bool Estado);

public record BarrioCreateDto(string Codigo, string Descripcion);

public record BarrioUpdateDto(string Descripcion, bool Estado);
