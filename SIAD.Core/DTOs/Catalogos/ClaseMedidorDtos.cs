namespace SIAD.Core.DTOs.Catalogos;

public record ClaseMedidorDto(string Codigo, string Descripcion, bool Estado);
public record ClaseMedidorLookupDto(string Codigo, string Descripcion);
public record ClaseMedidorCreateDto(string Codigo, string Descripcion);
public record ClaseMedidorUpdateDto(string Descripcion, bool Estado);
