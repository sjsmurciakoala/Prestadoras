namespace SIAD.Core.DTOs.Almacen;

/// <summary>Niveles de alerta de existencias, de más a menos urgente.</summary>
public static class StockSeveridad
{
    /// <summary>Existencia negativa (inconsistencia grave que exige revisión).</summary>
    public const string Negativa = "Negativa";

    /// <summary>Existencia en cero.</summary>
    public const string SinStock = "SinStock";

    /// <summary>Existencia por debajo del mínimo definido.</summary>
    public const string BajoMinimo = "BajoMinimo";

    /// <summary>
    /// Clasifica un par (existencia, mínimo) en su severidad, o <c>null</c> si está "en orden".
    /// Misma regla que <c>ArticulosService.GetAlertasStockAsync</c>, para uso en memoria (p. ej. la
    /// detección de cruce en el motor de posteo). Prioridad: negativa &gt; sin stock &gt; bajo mínimo.
    /// </summary>
    public static string? Clasificar(decimal existencia, decimal minimo)
    {
        if (existencia < 0m) return Negativa;
        if (existencia == 0m) return SinStock;
        if (minimo > 0m && existencia < minimo) return BajoMinimo;
        return null;
    }
}
