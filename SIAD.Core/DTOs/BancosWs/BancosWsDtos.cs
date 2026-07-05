using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.BancosWs;

/// <summary>
/// Credencial autenticada del canal bancario (F8). Resuelve el tenant y la
/// cuenta bancaria destino de los depósitos del banco.
/// </summary>
public sealed class BancosWsCredencialDto
{
    public long CompanyId { get; init; }
    public string Banco { get; init; } = string.Empty;
    public long? BancoCuentaId { get; init; }
    public DateOnly? Vigencia { get; init; }
}

public enum BancosWsConsultaResultado
{
    Ok = 0,
    SinRegistro = 1,
    SinPendientes = 2,
    Vencidas = 3,
}

/// <summary>Resultado de la consulta de saldos del WS bancario (contrato SIMAFI).</summary>
public sealed class BancosWsConsultaDto
{
    public BancosWsConsultaResultado Resultado { get; init; }
    public string Clave { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public decimal Total { get; init; }
    /// <summary>'S' si la factura vigente está vencida, 'N' si no (regla SIMAFI).</summary>
    public string FechaVence { get; init; } = "N";
    public IReadOnlyList<BancosWsDetalleDto> Detalles { get; init; } = Array.Empty<BancosWsDetalleDto>();
}

public sealed class BancosWsDetalleDto
{
    public long Id { get; init; }
    public string CodigoConcepto { get; init; } = string.Empty;
    public string Concepto { get; init; } = string.Empty;
    public decimal Valor { get; init; }
}

public sealed class BancosWsPagoRequestDto
{
    public string Clave { get; init; } = string.Empty;
    public string Referencia { get; init; } = string.Empty;
    public string Banco { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public TimeOnly? HoraRegistro { get; init; }
    public DateOnly? FechaEfectiva { get; init; }
    public string? Sucursal { get; init; }
    public string? Cajero { get; init; }
    /// <summary>'S' = servicios (valida monto == total), 'O' = otros.</summary>
    public string Tipo { get; init; } = "S";
    public bool ValidarMonto { get; init; } = true;
}

/// <summary>Códigos que devuelve sp_ban_ws_pagar (el controller los mapea al XML del contrato).</summary>
public sealed class BancosWsPagoResultDto
{
    public string Status { get; init; } = string.Empty;   // OK | IDEMPOTENTE | REFERENCIA_REVERSADA | SIN_REGISTRO | SIN_PENDIENTES | MONTO_NO_COINCIDE
    public long? PagoId { get; init; }
    public long? PolizaId { get; init; }
    public long? BanKardexId { get; init; }
    public decimal? TotalPendiente { get; init; }
}

/// <summary>Códigos que devuelve sp_ban_ws_reversar.</summary>
public sealed class BancosWsReversionResultDto
{
    public string Status { get; init; } = string.Empty;   // OK | NO_EXISTE | YA_REVERSADA
    public long? PagoId { get; init; }
    public long? PolizaReversoId { get; init; }
    public long? BanKardexReversoId { get; init; }
}
