using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Cobranza;

/// <summary>
/// Datos del convenio de pago (plan de cuotas) para el documento imprimible
/// (pruebas operativas ago-2026: no existía PDF del convenio).
/// </summary>
public sealed record ConvenioImpresionDto
{
    public int PlanId { get; init; }
    public string? Correlativo { get; init; }
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
    public int Meses { get; init; }
    public string? Comentario { get; init; }

    public string? EmpresaNombre { get; init; }
    public string? EmpresaRtn { get; init; }
    public string? EmpresaDireccion { get; init; }

    public IReadOnlyList<ConvenioCuotaImpresionDto> Cuotas { get; init; } = Array.Empty<ConvenioCuotaImpresionDto>();

    public decimal SaldoPendiente { get; init; }
}

public sealed record ConvenioCuotaImpresionDto
{
    public int Numero { get; init; }
    public DateTime? FechaVencimiento { get; init; }
    public decimal Monto { get; init; }
    public decimal Saldo { get; init; }
    public string EstadoTexto { get; init; } = string.Empty;
}
