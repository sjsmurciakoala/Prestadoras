using System;

namespace SIAD.Core.DTOs.Clientes;

public record ClienteMovimientoDto(
    int MovimientoId,
    DateTime Fecha,
    string Tipo,
    string? Descripcion,
    decimal Monto,
    decimal SaldoInline,
    decimal? NumRecibo,
    string? NumFactura);
