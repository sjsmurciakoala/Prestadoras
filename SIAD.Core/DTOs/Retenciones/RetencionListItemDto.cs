namespace SIAD.Core.DTOs.Retenciones;

public sealed class RetencionListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }

    /// <summary>Base de cálculo: TOTAL | SIN_ISV.</summary>
    public string BaseCalculo { get; init; } = string.Empty;

    /// <summary>Impuesto que retiene: ISR | ISV.</summary>
    public string TipoImpuesto { get; init; } = string.Empty;

    public bool Activo { get; init; }

    /// <summary>Cuántas tasas tiene registradas (histórico incluido).</summary>
    public int TotalTasas { get; init; }

    /// <summary>Cuántas de esas tasas siguen vigentes hoy (vigencia_hasta NULL o futura).</summary>
    public int TasasVigentes { get; init; }

    /// <summary>Porcentaje vigente hoy. NULL si la retención aún no tiene tasa (p. ej. ISV-RET pendiente).</summary>
    public decimal? PorcentajeVigente { get; init; }

    /// <summary>true si la empresa actual ya configuró la cuenta contable del pasivo para esta retención.</summary>
    public bool CuentaConfigurada { get; init; }

    /// <summary>Código de la cuenta configurada para la empresa actual (si hay).</summary>
    public string? CuentaCodigo { get; init; }

    /// <summary>Nombre de la cuenta configurada para la empresa actual (si hay).</summary>
    public string? CuentaNombre { get; init; }
}
