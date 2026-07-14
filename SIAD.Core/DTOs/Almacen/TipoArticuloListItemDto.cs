namespace SIAD.Core.DTOs.Almacen;

public sealed class TipoArticuloListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }

    /// <summary>false = los artículos de este tipo no llevan existencias ni kardex (ej. Servicios).</summary>
    public bool ManejaInventario { get; init; } = true;

    public bool Activo { get; init; }
}
