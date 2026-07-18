namespace SIAD.Core.DTOs.Almacen;

public sealed class AlertaStockDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string? UnidadMedida { get; init; }

    /// <summary>Nombre del tipo de artículo (alm_tipo_articulo), si está asignado.</summary>
    public string? TipoArticuloNombre { get; init; }

    /// <summary>Bodega donde se detecta la alerta (existencia y mínimo son por bodega).</summary>
    public int BodegaId { get; init; }
    public string? BodegaNombre { get; init; }
    public decimal Existencia { get; init; }
    public decimal ExistenciaMinima { get; init; }
    public decimal ValorUnitario { get; init; }

    /// <summary>
    /// "Negativa" | "SinStock" | "BajoMinimo". Resuelta en el servidor a partir
    /// de la existencia y el mínimo.
    /// </summary>
    public string Severidad { get; init; } = string.Empty;

    /// <summary>Unidades sugeridas para volver al mínimo (0 si no hay mínimo definido).</summary>
    public decimal CantidadSugerida { get; init; }

    /// <summary>Costo estimado de reponer la cantidad sugerida.</summary>
    public decimal ValorReposicion => CantidadSugerida * ValorUnitario;
}
