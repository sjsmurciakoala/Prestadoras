namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Retención con su tasa vigente a una fecha, para desplegables y para el motor de cálculo
/// (lo consumirá F2: al procesar/abonar un compromiso con la tasa que regía en la fecha del pago).
/// </summary>
public sealed class RetencionTasaLookupDto
{
    public int RetencionId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Base de cálculo: TOTAL | SIN_ISV.</summary>
    public string BaseCalculo { get; init; } = string.Empty;

    /// <summary>Impuesto que retiene: ISR | ISV.</summary>
    public string TipoImpuesto { get; init; } = string.Empty;

    public int TasaId { get; init; }
    public decimal Porcentaje { get; init; }
    public DateOnly VigenciaDesde { get; init; }
    public DateOnly? VigenciaHasta { get; init; }

    public string Display => $"{Codigo} - {Nombre} ({Porcentaje:0.##}%)";
}
