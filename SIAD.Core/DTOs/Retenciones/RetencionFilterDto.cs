namespace SIAD.Core.DTOs.Retenciones;

public sealed class RetencionFilterDto
{
    /// <summary>Busca por código o nombre.</summary>
    public string? Search { get; set; }

    /// <summary>null = todos; true = solo activos; false = solo inactivos.</summary>
    public bool? Activo { get; set; }

    /// <summary>null = todos; ISR | ISV = filtra por impuesto retenido.</summary>
    public string? TipoImpuesto { get; set; }
}
