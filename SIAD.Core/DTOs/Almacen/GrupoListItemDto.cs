namespace SIAD.Core.DTOs.Almacen;

public sealed class GrupoListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int? LineaId { get; init; }
    public string? LineaNombre { get; init; }
    public bool Activo { get; init; }
}
