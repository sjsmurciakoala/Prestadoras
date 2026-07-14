namespace SIAD.Core.DTOs.Almacen;

public sealed class CategoriaUnidadListItemDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public bool Activo { get; init; }
}
