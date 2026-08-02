namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Totales del catálogo de artículos (KPIs de las tarjetas), calculados en el servidor
/// sobre el mismo filtro aplicado al grid — no sobre la página visible del grid.
/// </summary>
public sealed class ArticulosResumenDto
{
    /// <summary>Cantidad de artículos que cumplen el filtro.</summary>
    public int Total { get; init; }

    /// <summary>De esos, cuántos tienen existencia positiva.</summary>
    public int ConStock { get; init; }

    /// <summary>De esos, cuántos están por debajo del mínimo definido.</summary>
    public int BajoMinimo { get; init; }

    /// <summary>Valor de inventario (Σ existencia × valor unitario) del universo filtrado.</summary>
    public decimal ValorInventario { get; init; }

    /// <summary>De esos, cuántos tienen la existencia de cabecera descuadrada de la Σ de bodegas activas.</summary>
    public int ConDescuadre { get; init; }
}
