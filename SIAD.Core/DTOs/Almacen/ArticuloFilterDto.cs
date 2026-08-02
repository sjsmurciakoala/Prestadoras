namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloFilterDto
{
    public string? Search { get; set; }

    /// <summary>Filtra por tipo de artículo (alm_tipo_articulo). Null = todos.</summary>
    public int? TipoArticuloId { get; set; }

    /// <summary>Filtra por categoría (alm_grupo). Null = todas.</summary>
    public int? GrupoId { get; set; }

    /// <summary>Filtra a los artículos con una ubicación activa en la bodega indicada. Null = todas.</summary>
    public int? BodegaId { get; set; }

    /// <summary>
    /// Estado de existencia. Valores válidos en <see cref="EstadoStockFiltro"/>
    /// (con_stock, bajo_minimo, sin_stock, negativo). Null/vacío = todos.
    /// </summary>
    public string? EstadoStock { get; set; }

    /// <summary>true = sólo artículos sin unidad de medida asignada (para saneo). Null/false = todos.</summary>
    public bool? SinUnidad { get; set; }

    /// <summary>
    /// Soft-delete: por defecto (null/false) el maestro lista SOLO artículos activos.
    /// true = incluye también los descontinuados (para consultarlos o reactivarlos).
    /// </summary>
    public bool? IncluirDescontinuados { get; set; }

    /// <summary>true = sólo los descontinuados (implica incluirlos). Para revisarlos en bloque.</summary>
    public bool? SoloDescontinuados { get; set; }

    /// <summary>
    /// true = sólo artículos cuya existencia de cabecera difiere de la suma de sus
    /// ubicaciones activas (descuadre a sanear). Null/false = todos.
    /// </summary>
    public bool? ConDescuadre { get; set; }
}

/// <summary>Valores admitidos por <see cref="ArticuloFilterDto.EstadoStock"/>. Categorías disjuntas.</summary>
public static class EstadoStockFiltro
{
    /// <summary>Existencia positiva y sobre el mínimo (o sin mínimo definido).</summary>
    public const string ConStock = "con_stock";

    /// <summary>Existencia positiva pero por debajo del mínimo definido.</summary>
    public const string BajoMinimo = "bajo_minimo";

    /// <summary>Existencia exactamente en cero.</summary>
    public const string SinStock = "sin_stock";

    /// <summary>Existencia negativa (descuadre a corregir).</summary>
    public const string Negativo = "negativo";
}
