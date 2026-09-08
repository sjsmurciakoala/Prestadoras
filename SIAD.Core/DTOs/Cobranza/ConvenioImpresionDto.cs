using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Cobranza;

/// <summary>
/// Datos del convenio de pago para su documento imprimible, el "Compromiso de
/// pago" que firma el cliente en la unidad de cobranza.
/// </summary>
public sealed record ConvenioImpresionDto
{
    public int PlanId { get; init; }
    public string? Correlativo { get; init; }
    /// <summary>Código del compromiso: correlativo a 7 dígitos y año, como "0000039-2026".</summary>
    public string Codigo { get; init; } = string.Empty;
    public string EstadoTexto { get; init; } = string.Empty;
    public DateTime? FechaCreacion { get; init; }
    public DateTime? FechaPrimerPago { get; init; }

    public string ClienteClave { get; init; } = string.Empty;
    public string ClienteNombre { get; init; } = string.Empty;
    public string? ClienteDireccion { get; init; }
    public string? Representante { get; init; }
    public string? DocRepresentante { get; init; }

    public decimal MontoTotal { get; init; }
    public decimal Prima { get; init; }
    public decimal MontoFinanciado { get; init; }
    /// <summary>Tasa de financiamiento; hoy el convenio no cobra intereses.</summary>
    public decimal Tasa { get; init; }
    public int Meses { get; init; }
    public decimal ValorCuota { get; init; }
    /// <summary>Observación que escribe la cajera al suscribir el convenio.</summary>
    public string? Comentario { get; init; }

    public string? EmpresaNombre { get; init; }
    public string? EmpresaRtn { get; init; }
    public string? EmpresaDireccion { get; init; }

    /// <summary>Deuda trasladada al convenio, abierta por concepto facturado.</summary>
    public IReadOnlyList<ConvenioConceptoDto> Conceptos { get; init; } = Array.Empty<ConvenioConceptoDto>();

    public IReadOnlyList<ConvenioCuotaImpresionDto> Cuotas { get; init; } = Array.Empty<ConvenioCuotaImpresionDto>();

    public decimal SaldoPendiente { get; init; }

    /// <summary>Quién firma por la unidad de cobranza; vacío deja el rótulo genérico.</summary>
    public string? FirmanteCobranza { get; init; }

    public string? ElaboradoPor { get; init; }
    public DateTime? FechaElaboracion { get; init; }
}

/// <summary>Una línea del desglose de la deuda: "Agua Potable", "Alcantarillado Sanitario"...</summary>
public sealed record ConvenioConceptoDto
{
    public string Descripcion { get; init; } = string.Empty;
    public decimal Valor { get; init; }
}

public sealed record ConvenioCuotaImpresionDto
{
    public int Numero { get; init; }
    public DateTime? FechaVencimiento { get; init; }
    public decimal Monto { get; init; }
    public decimal Saldo { get; init; }
    public string EstadoTexto { get; init; } = string.Empty;
}
