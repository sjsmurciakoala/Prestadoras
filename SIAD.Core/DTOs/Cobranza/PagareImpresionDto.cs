using System;

namespace SIAD.Core.DTOs.Cobranza;

/// <summary>
/// Datos del pagaré a la vista que respalda el convenio de pago. El documento
/// replica el formulario preimpreso de la unidad de cobranza, así que aquí solo
/// viajan los valores que se escriben sobre las líneas del formato; el texto
/// legal vive en la plantilla <c>Rpt_Dev_Pagare</c>.
/// </summary>
public sealed record PagareImpresionDto
{
    public int PlanId { get; init; }
    public string? Correlativo { get; init; }

    /// <summary>Fecha del convenio; es la fecha en que se firma el pagaré.</summary>
    public DateTime FechaSuscripcion { get; init; }

    // El municipio y el departamento no viajan: son fijos del formato y están
    // en la plantilla.

    // --- Deudor (quien firma) ---
    public string DeudorNombre { get; init; } = string.Empty;
    public string? DeudorIdentidad { get; init; }
    /// <summary>
    /// Titular de la cuenta cuando quien firma es su representante. Vacío
    /// significa que firma el titular en nombre propio, y el pagaré lo declara
    /// obligándose en su condición personal.
    /// </summary>
    public string? TitularRepresentado { get; init; }
    /// <summary>Telefono de contacto de quien firma; vacio no se imprime.</summary>
    public string? ContactoRepresentante { get; init; }
    /// <summary>Número que va al pie, bajo la identidad: correlativo del convenio y año.</summary>
    public string NumeroCuenta { get; init; } = string.Empty;

    // --- Acreedor ---
    public string? EmpresaNombre { get; init; }
    /// <summary>Quién firma por la unidad de cobranza; vacío deja el rótulo genérico.</summary>
    public string? FirmanteCobranza { get; init; }

    // --- Obligación ---
    /// <summary>Total del pagaré: el monto financiado del convenio.</summary>
    public decimal MontoTotal { get; init; }
    public int CantidadMeses { get; init; }
    public DateTime? FechaDesde { get; init; }
    public DateTime? FechaHasta { get; init; }
}
