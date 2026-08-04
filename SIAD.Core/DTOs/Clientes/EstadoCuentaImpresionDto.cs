using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Clientes;

/// <summary>
/// Datos del estado de cuenta del cliente para el documento imprimible
/// (pruebas operativas ago-2026: solo existía en pantalla). Los movimientos
/// son los mismos de la pestaña Movimientos: histórico unificado con saldo
/// corrido real, incluidas NC/ND.
/// </summary>
public sealed record EstadoCuentaImpresionDto
{
    public string ClienteClave { get; init; } = string.Empty;
    public string ClienteNombre { get; init; } = string.Empty;
    public string? ClienteDireccion { get; init; }

    public string? EmpresaNombre { get; init; }
    public string? EmpresaRtn { get; init; }
    public string? EmpresaDireccion { get; init; }

    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    /// <summary>Saldo corrido de la última fila visible (al corte del reporte).</summary>
    public decimal SaldoFinal { get; init; }

    public IReadOnlyList<ClienteMovimientoDto> Movimientos { get; init; } = Array.Empty<ClienteMovimientoDto>();
}
