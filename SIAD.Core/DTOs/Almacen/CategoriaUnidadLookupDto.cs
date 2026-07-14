namespace SIAD.Core.DTOs.Almacen;

/// <summary>Categoría de unidad para poblar combos (form de unidades, filtro).</summary>
public sealed class CategoriaUnidadLookupDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
}
