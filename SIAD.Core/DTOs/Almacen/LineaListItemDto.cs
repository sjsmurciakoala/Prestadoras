namespace SIAD.Core.DTOs.Almacen;

public sealed class LineaListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? CuentaContable { get; init; }
    public bool Activo { get; init; }
}
