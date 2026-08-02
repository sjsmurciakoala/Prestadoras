namespace SIAD.Core.DTOs.Bancos;

/// <summary>
/// Resultado de registrar un movimiento bancario. Incluye el cheque_id emitido (si el
/// tipo de transaccion emite cheque) para poder imprimirlo desde el cliente.
/// </summary>
public sealed class BanTransaccionResultadoDto
{
    public long BanKardexId { get; set; }

    public decimal SaldoResultante { get; set; }

    /// <summary>cheque_id emitido por el movimiento (para imprimir); null si no emitio cheque.</summary>
    public long? ChequeId { get; set; }
}
