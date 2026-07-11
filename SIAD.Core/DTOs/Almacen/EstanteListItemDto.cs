namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteListItemDto
{
    public int Id { get; init; }
    public int EstanteriaId { get; init; }
    public string? EstanteriaCodigo { get; init; }
    public string? BodegaNombre { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public bool Activo { get; init; }
}
