namespace SIAD.Core.DTOs.Bancos;

/// <summary>
/// Estado de cuenta bancario para un período: saldo anterior, movimientos con
/// saldo corrido, totales y saldo final. Base para la exportación a Excel.
/// </summary>
public class EstadoCuentaDto
{
    public long BancoCuentaId { get; set; }

    public string? BancoNombre { get; set; }

    public string CuentaNombre { get; set; } = string.Empty;

    public string NumeroCuenta { get; set; } = string.Empty;

    public string? MonedaCodigo { get; set; }

    public DateOnly FechaDesde { get; set; }

    public DateOnly FechaHasta { get; set; }

    /// <summary>Saldo corrido justo antes de <see cref="FechaDesde"/> (0 si no hay historial previo).</summary>
    public decimal SaldoAnterior { get; set; }

    /// <summary>Suma de cargos/egresos del período (montos negativos, en valor absoluto).</summary>
    public decimal TotalCargos { get; set; }

    /// <summary>Suma de abonos/ingresos del período (montos positivos).</summary>
    public decimal TotalAbonos { get; set; }

    /// <summary>SaldoAnterior + TotalAbonos - TotalCargos.</summary>
    public decimal SaldoFinal { get; set; }

    public List<BanTransaccionListDto> Movimientos { get; set; } = new();
}
