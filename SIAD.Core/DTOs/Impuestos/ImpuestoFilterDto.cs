namespace SIAD.Core.DTOs.Impuestos;

public sealed class ImpuestoFilterDto
{
    /// <summary>Busca por código o nombre.</summary>
    public string? Search { get; set; }

    /// <summary>null = todos; true = solo activos; false = solo inactivos.</summary>
    public bool? Activo { get; set; }
}
