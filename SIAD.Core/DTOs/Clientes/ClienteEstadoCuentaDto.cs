using System;

namespace SIAD.Core.DTOs.Clientes;

public record ClienteEstadoCuentaDto(
    decimal? SaldoActual,
    DateTime? FechaUltimoPago,
    decimal? UltimoPagoMonto,
    decimal? ConsumoPromedio,
    int? MesesPendientes);
