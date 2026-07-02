using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Clientes;

public record SaldoServicioDto(
    string Servicio,
    decimal Saldo,
    int MesesPendientes);

public record ClienteEstadoCuentaDto(
    decimal? SaldoActual,
    DateTime? FechaUltimoPago,
    decimal? UltimoPagoMonto,
    decimal? ConsumoPromedio,
    int? MesesPendientes,
    IReadOnlyList<SaldoServicioDto> SaldoPorServicio);
