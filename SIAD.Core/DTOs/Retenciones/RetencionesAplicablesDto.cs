namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Respuesta del lookup de retenciones aplicables a una fecha (F2): las retenciones activas con su %
/// vigente y cuenta por empresa, MÁS la tasa ISV general vigente que necesita la base SIN_ISV para
/// quitar el ISV al bruto. Se resuelve todo en una sola llamada para no encadenar round-trips en la UI.
/// </summary>
public sealed class RetencionesAplicablesDto
{
    /// <summary>
    /// % de la tasa ISV general vigente a la fecha (p. ej. 15.00), para la base SIN_ISV. null = no se
    /// encontró tasa ISV vigente ⇒ la base SIN_ISV no se reduce y la UI debe avisarlo.
    /// </summary>
    public decimal? TasaIsvGeneral { get; init; }

    public List<RetencionAplicableDto> Retenciones { get; init; } = new();
}
