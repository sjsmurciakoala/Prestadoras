namespace SIAD.Core.DTOs.Almacen;

public sealed class GrupoLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    /// <summary>Tipo de artículo al que pertenece la categoría (para la cascada tipo→categoría).</summary>
    public int? TipoArticuloId { get; init; }
    public string Display => $"{Codigo} - {Nombre}";
}
