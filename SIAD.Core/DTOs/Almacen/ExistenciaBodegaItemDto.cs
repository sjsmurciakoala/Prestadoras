namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Una línea del reporte de existencias por bodega: el stock de un artículo en una bodega,
/// valorizado al costo promedio de esa bodega. El reporte se agrupa por bodega.
/// </summary>
public sealed class ExistenciaBodegaItemDto
{
    /// <summary>PK de alm_articulo_bodega: clave única de fila (un par artículo/bodega) para el grid.</summary>
    public int Id { get; init; }

    public int BodegaId { get; init; }
    public string BodegaCodigo { get; init; } = string.Empty;
    public string BodegaNombre { get; init; } = string.Empty;

    /// <summary>Etiqueta de agrupación por bodega ("código — nombre").</summary>
    public string BodegaDisplay =>
        string.IsNullOrWhiteSpace(BodegaCodigo) ? BodegaNombre : $"{BodegaCodigo} — {BodegaNombre}";

    public int ArticuloId { get; init; }
    public string? ArticuloCodigo { get; init; }
    public string ArticuloDescripcion { get; init; } = string.Empty;

    /// <summary>Abreviatura/código de la unidad del catálogo (vacío si el artículo no está enlazado).</summary>
    public string? UnidadMedida { get; init; }

    /// <summary>Nombre del tipo de artículo.</summary>
    public string? TipoArticulo { get; init; }

    public decimal Existencia { get; init; }
    public decimal ExistenciaMinima { get; init; }

    /// <summary>Costo promedio ponderado del artículo en esta bodega (alm_articulo_bodega.costo_promedio).</summary>
    public decimal CostoPromedio { get; init; }

    /// <summary>Valor del inventario de esta línea: existencia × costo promedio (lo calcula el servicio).</summary>
    public decimal Valor { get; init; }

    /// <summary>true si hay mínimo definido en la bodega y la existencia cayó por debajo.</summary>
    public bool BajoMinimo => ExistenciaMinima > 0 && Existencia < ExistenciaMinima;
}
