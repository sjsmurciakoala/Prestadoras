using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Clientes;

/// <summary>
/// Fila del desglose por servicio: Deuda = cargos vigentes del ítem antes de
/// repartir los abonos; Porcentaje = % de distribución aplicado (null si el ítem
/// no participa del reparto); Saldo = deuda menos el abono distribuido.
/// </summary>
public record SaldoServicioDto(
    string Servicio,
    decimal Saldo,
    int MesesPendientes,
    decimal Deuda = 0m,
    decimal? Porcentaje = null);

public record ClienteEstadoCuentaDto(
    decimal? SaldoActual,
    DateTime? FechaUltimoPago,
    decimal? UltimoPagoMonto,
    decimal? ConsumoPromedio,
    int? MesesPendientes,
    IReadOnlyList<SaldoServicioDto> SaldoPorServicio);
