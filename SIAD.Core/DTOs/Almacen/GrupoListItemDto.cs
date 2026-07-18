namespace SIAD.Core.DTOs.Almacen;

public sealed class GrupoListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int? TipoArticuloId { get; init; }
    public string? TipoArticuloNombre { get; init; }
    public bool Activo { get; init; }
}
