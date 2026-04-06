namespace SIAD.Core.DTOs.Bancos;

public sealed class BanTransaccionDetalleDto
{
    public long BanKardexId { get; set; }

    public long BancoCuentaId { get; set; }

    public string IdTipoTransaccion { get; set; } = string.Empty;

    public DateOnly FechaMovimiento { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string? Referencia { get; set; }

    public decimal Monto { get; set; }

    public decimal TasaCambio { get; set; } = 1m;

    public List<BanTransaccionContraLineaDto> ContraCuentas { get; set; } = new();
}
