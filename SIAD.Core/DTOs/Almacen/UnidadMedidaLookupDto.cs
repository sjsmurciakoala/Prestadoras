namespace SIAD.Core.DTOs.Almacen;

/// <summary>Unidad activa para poblar dropdowns (form de artículos, selección de base).</summary>
public sealed class UnidadMedidaLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Abreviatura { get; init; }

    /// <summary>Categoría (tipo) de la unidad: FK a alm_categoria_unidad. Null si no clasificada.</summary>
    public int? CategoriaId { get; init; }

    /// <summary>Nombre de la categoría (para mostrar y agrupar).</summary>
    public string? CategoriaNombre { get; init; }

    /// <summary>"CODIGO - Nombre" para mostrar en el combo.</summary>
    public string Display => string.IsNullOrWhiteSpace(Abreviatura)
        ? $"{Codigo} - {Nombre}"
        : $"{Codigo} - {Nombre} ({Abreviatura})";
}
