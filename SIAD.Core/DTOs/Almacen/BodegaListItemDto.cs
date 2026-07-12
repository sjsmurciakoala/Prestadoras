namespace SIAD.Core.DTOs.Almacen;

public sealed class BodegaListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Responsable { get; init; }
    public bool Activo { get; init; }
}
