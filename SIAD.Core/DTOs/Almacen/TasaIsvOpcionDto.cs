namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Opción del selector "ISV en compras" del tipo de artículo: una tasa del ISV
/// (<c>cfg_impuesto_tasa</c>) disponible para asignar. El backend solo ofrece las tasas
/// del impuesto ISV que están activas y vigentes hoy, para que el usuario elija entre
/// "gravado" (el ISV se suma al costo) y "exento" (no registra ISV).
/// </summary>
public sealed class TasaIsvOpcionDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    /// <summary>GRAVADO | EXENTO | EXONERADO (de <c>cfg_impuesto_tasa.tipo</c>).</summary>
    public string Tipo { get; init; } = string.Empty;

    public decimal Porcentaje { get; init; }

    /// <summary>true si la tasa cobra impuesto (GRAVADO con % &gt; 0): las compras registran ISV.</summary>
    public bool EsGravada { get; init; }

    /// <summary>Etiqueta para el combo: "ISV 15% (gravado)", "Exento".</summary>
    public string Display { get; init; } = string.Empty;
}
