namespace SIAD.Core.DTOs.Almacen;

/// <summary>Totales del libro de movimientos de bodega para el período/filtro consultado.</summary>
public sealed class MovimientosBodegaResumenDto
{
    /// <summary>Cantidad de asientos (líneas de kardex) del filtro.</summary>
    public int TotalMovimientos { get; init; }

    /// <summary>Σ de las cantidades que entraron a la bodega en el período.</summary>
    public decimal TotalIngresos { get; init; }

    /// <summary>Σ de las cantidades que salieron de la bodega en el período.</summary>
    public decimal TotalSalidas { get; init; }

    /// <summary>Valor total movido (Σ alm_kardex.total de los asientos del período).</summary>
    public decimal ValorMovido { get; init; }
}
