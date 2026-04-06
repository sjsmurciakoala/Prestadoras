namespace SIAD.Core.DTOs.Bancos;

public class BanTransaccionListDto
{
    public long BanKardexId { get; set; }

    public long BancoCuentaId { get; set; }

    public string? BancoNombre { get; set; }

    public string CuentaNombre { get; set; } = string.Empty;

    public string NumeroCuenta { get; set; } = string.Empty;

    public string? MonedaCodigo { get; set; }

    public string IdTipoTransaccion { get; set; } = string.Empty;

    public DateOnly FechaMovimiento { get; set; }

    public DateTime FechaRegistro { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string? Referencia { get; set; }

    public string? ReferenciaAnulacion { get; set; }

    public decimal Monto { get; set; }

    public decimal SaldoResultante { get; set; }

    public string Estado { get; set; } = "ACTIVA";

    public string CreadoPor { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; }

    public string? ActualizadoPor { get; set; }

    public DateTime? ActualizadoEn { get; set; }
}
