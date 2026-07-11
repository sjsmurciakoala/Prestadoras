namespace SIAD.Core.DTOs.Almacen;

public sealed class GrupoLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int? LineaId { get; init; }
    public string Display => $"{Codigo} - {Nombre}";
}
