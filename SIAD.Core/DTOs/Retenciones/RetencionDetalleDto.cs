namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Una retención con todas sus tasas (vigentes e históricas) y la cuenta configurada para la
/// empresa actual (si hay).
/// </summary>
public sealed class RetencionDetalleDto
{
    public RetencionEditDto Retencion { get; init; } = new();
    public List<RetencionTasaDto> Tasas { get; init; } = new();

    /// <summary>Cuenta del pasivo configurada para la empresa actual. NULL si aún no se configuró.</summary>
    public RetencionCuentaDto? Cuenta { get; init; }
}
