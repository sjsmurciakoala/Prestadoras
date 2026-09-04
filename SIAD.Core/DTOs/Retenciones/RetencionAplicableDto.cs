namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Una retención del catálogo, resuelta para APLICARLA a la fecha de un compromiso (F2): su % vigente a
/// esa fecha (o null = sin tasa vigente) y la cuenta del pasivo configurada EN LA EMPRESA ACTUAL (o null
/// = no configurada). A diferencia de <see cref="RetencionTasaLookupDto"/>, incluye a propósito las
/// retenciones SIN tasa vigente (p. ej. ISV-RET): el combo debe mostrarlas y, al elegirlas, avisar "sin
/// tasa" sin inventar un porcentaje.
/// </summary>
public sealed class RetencionAplicableDto
{
    public int RetencionId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Base de cálculo: TOTAL | SIN_ISV.</summary>
    public string BaseCalculo { get; init; } = string.Empty;

    /// <summary>Impuesto que retiene: ISR | ISV.</summary>
    public string TipoImpuesto { get; init; } = string.Empty;

    /// <summary>% vigente a la fecha; null = sin tasa vigente (no autocalcular, no inventar %).</summary>
    public decimal? Porcentaje { get; init; }

    /// <summary>Id de la tasa vigente aplicada (null si no hay tasa vigente).</summary>
    public int? TasaId { get; init; }

    /// <summary>Cuenta del pasivo ("retenciones por pagar") configurada en la empresa actual (null si no).</summary>
    public long? CuentaAccountId { get; init; }
    public string? CuentaCodigo { get; init; }
    public string? CuentaNombre { get; init; }

    public bool TieneTasaVigente => Porcentaje.HasValue;
    public bool CuentaConfigurada => CuentaAccountId.HasValue;

    /// <summary>Texto del desplegable. Sin tasa vigente se marca explícitamente.</summary>
    public string Display => Porcentaje is { } pct
        ? $"{Codigo} - {Nombre} ({pct:0.##}%)"
        : $"{Codigo} - {Nombre} (sin tasa)";
}
