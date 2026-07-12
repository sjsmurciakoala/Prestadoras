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
}
