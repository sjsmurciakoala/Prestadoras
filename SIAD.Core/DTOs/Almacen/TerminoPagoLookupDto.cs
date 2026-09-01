namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Término de pago para poblar el combo de la factura de compra. Incluye los días para que la
/// pantalla autocalcule el vencimiento y la marca de predeterminado para preseleccionarlo.
/// </summary>
public sealed class TerminoPagoLookupDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int Dias { get; init; }
    public bool EsDefault { get; init; }

    /// <summary>"Contado" / "Crédito 30 días (30 d)" para el combo.</summary>
    public string Display => Dias > 0 ? $"{Nombre} ({Dias} d)" : Nombre;
}
