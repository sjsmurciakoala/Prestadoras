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

    /// <summary>
    /// Id de la partida contable (con_partida_hdr.poliza_id) asociada a la transacción.
    /// NULL cuando la transacción no tiene partida registrada (p.ej. movimientos migrados).
    /// </summary>
    public long? PolizaId { get; set; }

    public List<BanTransaccionContraLineaDto> ContraCuentas { get; set; } = new();
}
