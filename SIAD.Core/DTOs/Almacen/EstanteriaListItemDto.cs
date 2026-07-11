namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteriaListItemDto
{
    public int Id { get; init; }
    public int BodegaId { get; init; }
    public string? BodegaNombre { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string? Nombre { get; init; }
    public bool Activo { get; init; }
}
