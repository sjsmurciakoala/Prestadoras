namespace SIAD.Core.DTOs.Rutas;

public record RutaFilterDto
{
    public int? CodCiclo { get; set; }
    public string? CodRuta { get; set; }
    public bool? Activo { get; set; }
}
