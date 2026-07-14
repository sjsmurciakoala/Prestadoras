namespace SIAD.Core.DTOs.Impuestos;

public sealed class ImpuestoListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public bool Activo { get; init; }

    /// <summary>Cuántas tasas tiene registradas (histórico incluido).</summary>
    public int TotalTasas { get; init; }

    /// <summary>Cuántas de esas tasas siguen vigentes hoy (vigencia_hasta NULL o futura).</summary>
    public int TasasVigentes { get; init; }
}
